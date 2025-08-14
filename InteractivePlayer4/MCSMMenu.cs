using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using System.Threading;

namespace InteractivePlayer
{
    internal class MCSMMenu : Form
    {
        private readonly string menuJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "general", "menu.json");

        public string SelectedEpisodeFolder { get; private set; }

        private readonly Image[] layers = new Image[4];
        private Image[] nextLayers = null;
        private Point mousePos = new Point(0, 0);

        private Cursor customCursorNormal = null;
        private Cursor customCursorHovered = null;

        private bool[] folderAvailable = new bool[6];
        private bool[] folderGrayscaled = new bool[6];

        private LibVLC _libVLC;
        private MediaPlayer _hoverPlayer;
        private MediaPlayer _selectPlayer;
        private MediaPlayer _leftArrowSelectPlayer;
        private MediaPlayer _rightArrowSelectPlayer;
        private string leftArrowSelectSoundPath;
        private string rightArrowSelectSoundPath;
        private string hoverSoundPath;
        private string selectSoundPath;

        private Image creditLeftArrow, creditLeftArrowHover, creditRightArrow, creditRightArrowHover;
        private Image installNormal, installHovered, installSelected;
        private Image trailerNormal, trailerHovered, trailerSelected;
        private Image tutorialNormal, tutorialHovered, tutorialSelected;
        private Image continueNormal, continueHovered, continueSelected;
        private Image restartNormal, restartHovered, restartSelected;
        private Image playNormal, playHovered, playSelected, titleImage;
        private Image nextTitleImage = null;
        private Image leftArrow, rightArrow;
        private bool playButtonPressed = false;
        private bool continueButtonPressed = false;
        private bool restartButtonPressed = false;
        private bool playButtonHovered = false;

        private Size titleScaledSize;
        private Size nextTitleScaledSize;
        private Rectangle playButtonRect;
        private Rectangle leftArrowRect, rightArrowRect;

        private Rectangle continueButtonRect;
        private Rectangle restartButtonRect;
        private bool continueButtonHovered = false;
        private bool restartButtonHovered = false;

        private Rectangle tutorialButtonRect;
        private Rectangle trailerButtonRect;
        private bool tutorialButtonHovered = false;
        private bool trailerButtonHovered = false;
        private bool tutorialButtonPressed = false;
        private bool trailerButtonPressed = false;

        private readonly string[] folders = { "credits", "one", "two", "three", "four", "five" };
        private int currentFolderIndex = 1;

        private bool isAnimating = false;
        private int animationDirection = 0; // -1 for left, 1 for right
        private int animationOffset = 0;
        private System.Windows.Forms.Timer animationTimer;
        private Stopwatch animationWatch = new Stopwatch();
        private int animationDuration = 400;

        private Bitmap[] cachedLayers = new Bitmap[4];
        private Bitmap[] cachedNextLayers = null;

        private Image leftArrowHover, rightArrowHover;
        private bool leftArrowHovered = false, rightArrowHovered = false;

        private void LoadCustomCursors()
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "general");
            string normalPath = Path.Combine(basePath, "MCSMCursorNormal.png");
            string hoveredPath = Path.Combine(basePath, "MCSMCursorHovered.png");

            // Load normal cursor
            if (File.Exists(normalPath))
            {
                try
                {
                    using (var bmp = new Bitmap(normalPath))
                    {
                        IntPtr iconHandle = bmp.GetHicon();
                        customCursorNormal = new Cursor(iconHandle);
                    }
                }
                catch
                {
                    customCursorNormal = Cursors.Default;
                }
            }
            else
            {
                customCursorNormal = Cursors.Default;
            }

            // Load hovered cursor
            if (File.Exists(hoveredPath))
            {
                try
                {
                    using (var bmp = new Bitmap(hoveredPath))
                    {
                        IntPtr iconHandle = bmp.GetHicon();
                        customCursorHovered = new Cursor(iconHandle);
                    }
                }
                catch
                {
                    customCursorHovered = customCursorNormal ?? Cursors.Default;
                }
            }
            else
            {
                customCursorHovered = customCursorNormal ?? Cursors.Default;
            }
        }

        private void SaveLastMenu()
        {
            var obj = new JObject
            {
                ["lastMenu"] = folders[currentFolderIndex]
            };
            File.WriteAllText(menuJsonPath, obj.ToString());
        }

        private void LoadLastMenu()
        {
            if (File.Exists(menuJsonPath))
            {
                try
                {
                    var obj = JObject.Parse(File.ReadAllText(menuJsonPath));
                    var lastMenu = (string)obj["lastMenu"];
                    int idx = Array.IndexOf(folders, lastMenu);
                    if (idx >= 0)
                        currentFolderIndex = idx;
                }
                catch { }
            }
        }

        public MCSMMenu()
        {
            LoadingForm loadingForm = null;
            Thread loadingThread = new Thread(() =>
            {
                LoadingForm.ForceMCSMLoading = true;
                loadingForm = new LoadingForm();
                loadingForm.ShowDialog();
            });
            loadingThread.SetApartmentState(ApartmentState.STA);
            loadingThread.Start();

            Core.Initialize();
            _libVLC = new LibVLC();

            LoadLastMenu();
            CheckFolders();
            LoadFolderImages();

            LoadCustomCursors();
            Cursor = customCursorNormal;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1400, 788);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = "Interactive Player";

            string generalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "general");

            hoverSoundPath = Path.Combine(generalPath, "hovered.m4a");
            selectSoundPath = Path.Combine(generalPath, "selected.m4a");
            leftArrowSelectSoundPath = Path.Combine(generalPath, "Left_Select.wav");
            rightArrowSelectSoundPath = Path.Combine(generalPath, "Right_Select.wav");

            if (File.Exists(hoverSoundPath))
            {
                var hoverMedia = new Media(_libVLC, new Uri(hoverSoundPath));
                _hoverPlayer = new MediaPlayer(hoverMedia);
            }
            if (File.Exists(selectSoundPath))
            {
                var selectMedia = new Media(_libVLC, new Uri(selectSoundPath));
                _selectPlayer = new MediaPlayer(selectMedia);
            }
            if (File.Exists(leftArrowSelectSoundPath))
            {
                var leftArrowMedia = new Media(_libVLC, new Uri(leftArrowSelectSoundPath));
                _leftArrowSelectPlayer = new MediaPlayer(leftArrowMedia);
            }
            if (File.Exists(rightArrowSelectSoundPath))
            {
                var rightArrowMedia = new Media(_libVLC, new Uri(rightArrowSelectSoundPath));
                _rightArrowSelectPlayer = new MediaPlayer(rightArrowMedia);
            }

            leftArrow = Image.FromFile(Path.Combine(generalPath, "Left_Arrow.png"));
            rightArrow = Image.FromFile(Path.Combine(generalPath, "Right_Arrow.png"));
            leftArrowHover = Image.FromFile(Path.Combine(generalPath, "Left_Hover.png"));
            rightArrowHover = Image.FromFile(Path.Combine(generalPath, "Right_Hover.png"));

            creditLeftArrow = Image.FromFile(Path.Combine(generalPath, "Credit_Left_Arrow.png"));
            creditLeftArrowHover = Image.FromFile(Path.Combine(generalPath, "Credit_Left_Hover.png"));
            creditRightArrow = Image.FromFile(Path.Combine(generalPath, "Credit_Right_Arrow.png"));
            creditRightArrowHover = Image.FromFile(Path.Combine(generalPath, "Credit_Right_Hover.png"));

            playNormal = Image.FromFile(Path.Combine(generalPath, "PlayNormal.png"));
            playHovered = Image.FromFile(Path.Combine(generalPath, "PlayHovered.png"));
            playSelected = Image.FromFile(Path.Combine(generalPath, "PlaySelected.png"));

            installNormal = Image.FromFile(Path.Combine(generalPath, "InstallNormal.png"));
            installHovered = Image.FromFile(Path.Combine(generalPath, "InstallHovered.png"));
            installSelected = Image.FromFile(Path.Combine(generalPath, "InstallSelected.png"));

            continueNormal = Image.FromFile(Path.Combine(generalPath, "ContinueNormal.png"));
            continueHovered = Image.FromFile(Path.Combine(generalPath, "ContinueHovered.png"));
            continueSelected = Image.FromFile(Path.Combine(generalPath, "ContinueSelected.png"));

            trailerNormal = Image.FromFile(Path.Combine(generalPath, "trailerNormal.png"));
            trailerHovered = Image.FromFile(Path.Combine(generalPath, "trailerHovered.png"));
            trailerSelected = Image.FromFile(Path.Combine(generalPath, "trailerSelected.png"));

            tutorialNormal = Image.FromFile(Path.Combine(generalPath, "tutorialNormal.png"));
            tutorialHovered = Image.FromFile(Path.Combine(generalPath, "tutorialHovered.png"));
            tutorialSelected = Image.FromFile(Path.Combine(generalPath, "tutorialSelected.png"));

            restartNormal = Image.FromFile(Path.Combine(generalPath, "RestartNormal.png"));
            restartHovered = Image.FromFile(Path.Combine(generalPath, "RestartHovered.png"));
            restartSelected = Image.FromFile(Path.Combine(generalPath, "RestartSelected.png"));

            LoadFolderImages();

            MouseMove += MCSMMenu_MouseMove;
            MouseDown += MCSMMenu_MouseDown;
            MouseUp += MCSMMenu_MouseUp;

            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 1;
            animationTimer.Tick += AnimationTimer_Tick;

            LoadingForm.ForceMCSMLoading = false;
            loadingForm.Invoke((MethodInvoker)(() => loadingForm.Close()));
        }

        private Bitmap GrayscaleImage(Bitmap src)
        {
            Bitmap grayBmp = new Bitmap(src.Width, src.Height);
            using (Graphics g = Graphics.FromImage(grayBmp))
            {
                var colorMatrix = new System.Drawing.Imaging.ColorMatrix(
                    new float[][]
                    {
                        new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                        new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                        new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                        new float[] {0,      0,      0,      1, 0},
                        new float[] {0,      0,      0,      0, 1}
                    });
                var attributes = new System.Drawing.Imaging.ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                    0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attributes);
            }
            return grayBmp;
        }

        private void CheckFolders()
        {
            string mcsmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM");
            for (int i = 1; i < 6; i++)
            {
                string folderName = $"Minecraft Story Mode Ep{i}";
                string folderPath = Path.Combine(mcsmPath, folderName);
                folderAvailable[i] = Directory.Exists(folderPath);
                folderGrayscaled[i] = !folderAvailable[i];
            }

            folderAvailable[0] = true;
            folderGrayscaled[0] = false;
        }

        private void PlayHoverSound()
        {
            try
            {
                if (_hoverPlayer != null)
                {
                    _hoverPlayer.Stop();
                    _hoverPlayer.Play();
                }
            }
            catch { }
        }

        private void PlaySelectSound()
        {
            try
            {
                if (_selectPlayer != null)
                {
                    _selectPlayer.Stop();
                    _selectPlayer.Play();
                }
            }
            catch { }
        }

        private void PlayLeftArrowSelectSound()
        {
            try
            {
                if (!string.IsNullOrEmpty(leftArrowSelectSoundPath) && File.Exists(leftArrowSelectSoundPath))
                {
                    var media = new Media(_libVLC, new Uri(leftArrowSelectSoundPath));
                    var player = new MediaPlayer(media);
                    player.Play();
                    player.EndReached += (s, e) => { player.Dispose(); media.Dispose(); };
                }
            }
            catch { }
        }

        private void PlayRightArrowSelectSound()
        {
            try
            {
                if (!string.IsNullOrEmpty(rightArrowSelectSoundPath) && File.Exists(rightArrowSelectSoundPath))
                {
                    var media = new Media(_libVLC, new Uri(rightArrowSelectSoundPath));
                    var player = new MediaPlayer(media);
                    player.Play();
                    player.EndReached += (s, e) => { player.Dispose(); media.Dispose(); };
                }
            }
            catch { }
        }

        private void LoadFolderImages()
        {
            string generalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "general");
            string folder = folders[currentFolderIndex];
            string folderPath = Path.Combine(generalPath, folder);

            // Dispose old images
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i]?.Dispose();
                layers[i] = null;
                cachedLayers[i]?.Dispose();
                cachedLayers[i] = null;
            }
            titleImage?.Dispose();
            titleImage = null;

            // Parallax layers
            layers[0] = Image.FromFile(Path.Combine(folderPath, "background.png"));
            layers[1] = Image.FromFile(Path.Combine(folderPath, "layer1.png"));
            bool useFemaleLayer2 = false;
            bool useFemaleLayer3 = false;
            string epSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "Minecraft Story Mode Ep1", "save.json");
            if (File.Exists(epSavePath))
            {
                var saveJson = File.ReadAllText(epSavePath);
                if (saveJson.Contains("\"Gender\": \"Female\""))
                {
                    if (currentFolderIndex == 1)
                    {
                        string femLayer1Path = Path.Combine(folderPath, "femlayer1.png");
                        if (File.Exists(femLayer1Path))
                        {
                            layers[1]?.Dispose();
                            layers[1] = Image.FromFile(femLayer1Path);
                        }
                    }
                    if (currentFolderIndex == 0 || currentFolderIndex == 3)
                    {
                        string femLayerPath = Path.Combine(folderPath, "femlayer2.png");
                        if (File.Exists(femLayerPath))
                        {
                            layers[2] = Image.FromFile(femLayerPath);
                            useFemaleLayer2 = true;
                        }
                    }
                    if (currentFolderIndex == 4 || currentFolderIndex == 5)
                    {
                        string femLayer3Path = Path.Combine(folderPath, "femlayer3.png");
                        if (File.Exists(femLayer3Path))
                        {
                            layers[3] = Image.FromFile(femLayer3Path);
                            useFemaleLayer3 = true;
                        }
                    }
                }
            }
            if (!useFemaleLayer2)
            {
                layers[2] = Image.FromFile(Path.Combine(folderPath, "layer2.png"));
            }
            if (!useFemaleLayer3)
            {
                layers[3] = Image.FromFile(Path.Combine(folderPath, "layer3.png"));
            }

            // Cache scaled layers
            CacheScaledLayers(layers, cachedLayers, currentFolderIndex);

            // Title image
            titleImage = Image.FromFile(Path.Combine(folderPath, "Title.png"));
            titleScaledSize = new Size((int)(titleImage.Width / 1.5), (int)(titleImage.Height / 1.5));
        }

        private void LoadNextFolderImages(int nextIndex)
        {
            string generalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "general");
            string folder = folders[nextIndex];
            string folderPath = Path.Combine(generalPath, folder);

            // Dispose old next images
            if (nextLayers != null)
            {
                for (int i = 0; i < nextLayers.Length; i++)
                {
                    nextLayers[i]?.Dispose();
                    nextLayers[i] = null;
                    cachedNextLayers?[i]?.Dispose();
                    if (cachedNextLayers != null) cachedNextLayers[i] = null;
                }
            }
            nextTitleImage?.Dispose();
            nextTitleImage = null;

            // Parallax layers
            nextLayers = new Image[4];
            nextLayers[0] = Image.FromFile(Path.Combine(folderPath, "background.png"));
            nextLayers[1] = Image.FromFile(Path.Combine(folderPath, "layer1.png"));
            bool useFemaleLayer2 = false;
            bool useFemaleLayer3 = false;
            string epSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "Minecraft Story Mode Ep1", "save.json");
            if (File.Exists(epSavePath))
            {
                var saveJson = File.ReadAllText(epSavePath);
                if (saveJson.Contains("\"Gender\": \"Female\""))
                {
                    if (nextIndex == 1)
                    {
                        string femLayer1Path = Path.Combine(folderPath, "femlayer1.png");
                        if (File.Exists(femLayer1Path))
                        {
                            nextLayers[1]?.Dispose();
                            nextLayers[1] = Image.FromFile(femLayer1Path);
                        }
                    }
                    if (nextIndex == 0 || nextIndex == 3)
                    {
                        string femLayerPath = Path.Combine(folderPath, "femlayer2.png");
                        if (File.Exists(femLayerPath))
                        {
                            nextLayers[2] = Image.FromFile(femLayerPath);
                            useFemaleLayer2 = true;
                        }
                    }
                    if (nextIndex == 4 || nextIndex == 5)
                    {
                        string femLayer3Path = Path.Combine(folderPath, "femlayer3.png");
                        if (File.Exists(femLayer3Path))
                        {
                            nextLayers[3] = Image.FromFile(femLayer3Path);
                            useFemaleLayer3 = true;
                        }
                    }
                }
            }
            if (!useFemaleLayer2)
            {
                nextLayers[2] = Image.FromFile(Path.Combine(folderPath, "layer2.png"));
            }
            if (!useFemaleLayer3)
            {
                nextLayers[3] = Image.FromFile(Path.Combine(folderPath, "layer3.png"));
            }

            // Cache scaled next layers
            cachedNextLayers = new Bitmap[4];
            CacheScaledLayers(nextLayers, cachedNextLayers, nextIndex);

            // Title image
            nextTitleImage = Image.FromFile(Path.Combine(folderPath, "Title.png"));
            nextTitleScaledSize = new Size((int)(nextTitleImage.Width / 1.5), (int)(nextTitleImage.Height / 1.5));
        }

        private void CacheScaledLayers(Image[] src, Bitmap[] dest, int folderIndex)
        {
            int windowWidth = ClientSize.Width;
            int windowHeight = ClientSize.Height;
            float baseScale = Math.Min(windowWidth / 1920f, windowHeight / 1080f);

            bool grayscale = folderGrayscaled[folderIndex];

            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == null) continue;
                Image img = src[i];
                float scale = (img.Width > 1920 || img.Height > 1080)
                    ? baseScale
                    : Math.Min((float)windowWidth / img.Width, (float)windowHeight / img.Height);

                int imgWidth = (int)(img.Width * scale);
                int imgHeight = (int)(img.Height * scale);
                if (i == 0) imgHeight += 1;

                Bitmap bmp = new Bitmap(imgWidth, imgHeight);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(img, 0, 0, imgWidth, imgHeight);
                }
                if (grayscale)
                {
                    Bitmap grayBmp = GrayscaleImage(bmp);
                    bmp.Dispose();
                    dest[i] = grayBmp;
                }
                else
                {
                    dest[i] = bmp;
                }
            }
        }

        private void MCSMMenu_MouseMove(object sender, MouseEventArgs e)
        {
            if (isAnimating)
                return; // Ignore mouse movement during animation

            mousePos = e.Location;
            bool needInvalidate = false;

            bool anyButtonHovered = false;

            // Play button hover
            bool wasHovered = playButtonHovered;
            playButtonHovered = playButtonRect.Contains(mousePos);
            if (playButtonHovered != wasHovered)
            {
                needInvalidate = true;
                if (playButtonHovered)
                    PlayHoverSound();
            }
            if (playButtonHovered) anyButtonHovered = true;

            // Arrow hover
            bool wasLeftArrowHovered = leftArrowHovered;
            bool wasRightArrowHovered = rightArrowHovered;
            leftArrowHovered = leftArrowRect.Contains(mousePos) && currentFolderIndex > 0 && !isAnimating;
            rightArrowHovered = rightArrowRect.Contains(mousePos) && currentFolderIndex < folders.Length - 1 && !isAnimating;
            if (leftArrowHovered != wasLeftArrowHovered)
            {
                needInvalidate = true;
                if (leftArrowHovered)
                    PlayHoverSound();
            }
            if (rightArrowHovered != wasRightArrowHovered)
            {
                needInvalidate = true;
                if (rightArrowHovered)
                    PlayHoverSound();
            }
            if (leftArrowHovered || rightArrowHovered) anyButtonHovered = true;

            bool wasContinueHovered = continueButtonHovered;
            bool wasRestartHovered = restartButtonHovered;
            continueButtonHovered = continueButtonRect.Contains(mousePos);
            restartButtonHovered = restartButtonRect.Contains(mousePos);
            if (continueButtonHovered != wasContinueHovered || restartButtonHovered != wasRestartHovered)
            {
                needInvalidate = true;
                if (continueButtonHovered || restartButtonHovered)
                    PlayHoverSound();
            }
            if (continueButtonHovered || restartButtonHovered) anyButtonHovered = true;

            if (currentFolderIndex == 0)
            {
                bool wasTutorialHovered = tutorialButtonHovered;
                bool wasTrailerHovered = trailerButtonHovered;
                tutorialButtonHovered = tutorialButtonRect.Contains(mousePos);
                trailerButtonHovered = trailerButtonRect.Contains(mousePos);
                if (tutorialButtonHovered != wasTutorialHovered || trailerButtonHovered != wasTrailerHovered)
                {
                    needInvalidate = true;
                    if (tutorialButtonHovered || trailerButtonHovered)
                        PlayHoverSound();
                }
                if (tutorialButtonHovered || trailerButtonHovered) anyButtonHovered = true;
            }

            Cursor = anyButtonHovered ? customCursorHovered : customCursorNormal;

            if (needInvalidate)
                Invalidate();
            else
                Invalidate();
        }

        private void MCSMMenu_MouseDown(object sender, MouseEventArgs e)
        {
            if (currentFolderIndex == 0)
            {
                if (tutorialButtonRect.Contains(e.Location))
                {
                    tutorialButtonPressed = true;
                    Invalidate();
                }
                else if (trailerButtonRect.Contains(e.Location))
                {
                    trailerButtonPressed = true;
                    Invalidate();
                }
            }

            if (e.Button == MouseButtons.Left && playButtonRect.Contains(e.Location))
            {
                playButtonPressed = true;
                Invalidate();
            }
        }

        private void MCSMMenu_MouseUp(object sender, MouseEventArgs e)
        {
            if (playButtonPressed)
            {
                playButtonPressed = false;
                Invalidate();
                if (playButtonRect.Contains(e.Location))
                {
                    if (folderGrayscaled[currentFolderIndex])
                    {
                        // Open InteractiveDetailsMenu for the episode
                        string mcsmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM");
                        string folderName = $"Minecraft Story Mode Ep{currentFolderIndex}";
                        string folderPath = Path.Combine(mcsmPath, folderName);
                        PlaySelectSound();
                        InteractiveDetailsMenu.ShowInteractiveDetailsMenu(folderPath);
                    }
                    else
                    {
                        string mcsmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM");
                        string folderName = $"Minecraft Story Mode Ep{currentFolderIndex}";
                        string folderPath = Path.Combine(mcsmPath, folderName);

                        string packsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Packs");

                        if (Utilities.CheckAndPromptForUpdate(folderPath, packsDirectory))
                            return;

                        SelectedEpisodeFolder = folderPath;
                        PlaySelectSound();
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
            }

            if (currentFolderIndex == 0)
            {
                if (tutorialButtonPressed)
                {
                    tutorialButtonPressed = false;
                    Invalidate();
                    if (tutorialButtonRect.Contains(e.Location))
                    {
                        PlaySelectSound();
                        this.Hide();

                        LoadingForm loadingForm = null;
                        var loadingThread = new Thread(() =>
                        {
                            LoadingForm.ForceMCSMLoading = true;
                            loadingForm = new LoadingForm();
                            loadingForm.ShowDialog();
                        });
                        loadingThread.SetApartmentState(ApartmentState.STA);
                        loadingThread.Start();

                        string trailerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "general", "extras", "tutorial.mp4");

                        if (File.Exists(trailerPath))
                        {
                            Core.Initialize();
                            using (var libVLC = new LibVLC())
                            using (var media = new Media(libVLC, new Uri(trailerPath)))
                            using (var player = new MediaPlayer(media))
                            {
                                var videoFinished = new ManualResetEvent(false);
                                player.EndReached += (s, ev) => videoFinished.Set();
                                player.Play();

                                if (loadingForm != null)
                                {
                                    loadingForm.Invoke((MethodInvoker)(() => loadingForm.Close()));
                                }

                                videoFinished.WaitOne();
                            }
                        }
                        else
                        {
                            if (loadingForm != null)
                            {
                                loadingForm.Invoke((MethodInvoker)(() => loadingForm.Close()));
                            }
                            MessageBox.Show("Trailer video not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        this.Invoke((MethodInvoker)(() =>
                        {
                            this.Show();
                        }));
                        return;
                    }
                }
                if (trailerButtonPressed)
                {
                    trailerButtonPressed = false;
                    Invalidate();
                    if (trailerButtonRect.Contains(e.Location))
                    {
                        PlaySelectSound();
                        this.Hide();

                        LoadingForm loadingForm = null;
                        var loadingThread = new Thread(() =>
                        {
                            LoadingForm.ForceMCSMLoading = true;
                            loadingForm = new LoadingForm();
                            loadingForm.ShowDialog();
                        });
                        loadingThread.SetApartmentState(ApartmentState.STA);
                        loadingThread.Start();

                        string trailerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", "general", "extras", "trailer.mp4");

                        if (File.Exists(trailerPath))
                        {
                            Core.Initialize();
                            using (var libVLC = new LibVLC())
                            using (var media = new Media(libVLC, new Uri(trailerPath)))
                            using (var player = new MediaPlayer(media))
                            {
                                var videoFinished = new ManualResetEvent(false);
                                player.EndReached += (s, ev) => videoFinished.Set();
                                player.Play();

                                if (loadingForm != null)
                                {
                                    loadingForm.Invoke((MethodInvoker)(() => loadingForm.Close()));
                                }

                                videoFinished.WaitOne();
                            }
                        }
                        else
                        {
                            if (loadingForm != null)
                            {
                                loadingForm.Invoke((MethodInvoker)(() => loadingForm.Close()));
                            }
                            MessageBox.Show("Trailer video not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        this.Invoke((MethodInvoker)(() =>
                        {
                            this.Show();
                        }));
                        return;
                    }
                }
            }

            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", $"Minecraft Story Mode Ep{currentFolderIndex}", "save.json");
            if (continueButtonRect.Contains(e.Location))
            {
                if (File.Exists(savePath))
                {
                    // Continue: just close and set SelectedEpisodeFolder
                    string mcsmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM");
                    string folderName = $"Minecraft Story Mode Ep{currentFolderIndex}";
                    string folderPath = Path.Combine(mcsmPath, folderName);

                    string packsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Packs");

                    if (Utilities.CheckAndPromptForUpdate(folderPath, packsDirectory))
                        return;

                    SelectedEpisodeFolder = folderPath;
                    PlaySelectSound();
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }
            else if (restartButtonRect.Contains(e.Location))
            {
                if (File.Exists(savePath))
                {
                    // Restart: delete save and close
                    try { File.Delete(savePath); } catch { }
                    string mcsmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM");
                    string folderName = $"Minecraft Story Mode Ep{currentFolderIndex}";
                    string folderPath = Path.Combine(mcsmPath, folderName);

                    string packsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Packs");

                    if (Utilities.CheckAndPromptForUpdate(folderPath, packsDirectory))
                        return;

                    SelectedEpisodeFolder = folderPath;
                    PlaySelectSound();
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }

            // Left arrow click
            if (!isAnimating && leftArrowRect.Contains(e.Location) && currentFolderIndex > 0)
            {
                PlayLeftArrowSelectSound();
                StartSwooshAnimation(-1);
            }
            // Right arrow click
            else if (!isAnimating && rightArrowRect.Contains(e.Location) && currentFolderIndex < folders.Length - 1)
            {
                PlayRightArrowSelectSound();
                StartSwooshAnimation(1);
            }
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - (float)Math.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private void StartSwooshAnimation(int direction)
        {
            int nextIndex = currentFolderIndex + direction;
            if (nextIndex < 0 || nextIndex >= folders.Length) return;

            LoadNextFolderImages(nextIndex);
            animationDirection = direction;
            animationOffset = 0;
            isAnimating = true;
            animationWatch.Restart();
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            int windowWidth = ClientSize.Width;
            float t = Math.Min(1f, (float)animationWatch.ElapsedMilliseconds / animationDuration);
            float easedT = EaseInOutCubic(t);
            animationOffset = (int)(windowWidth * easedT);

            if (t >= 1f)
            {
                animationTimer.Stop();
                animationOffset = 0;
                isAnimating = false;

                for (int i = 0; i < layers.Length; i++)
                {
                    layers[i]?.Dispose();
                    layers[i] = nextLayers[i];
                    nextLayers[i] = null;
                }
                titleImage?.Dispose();
                titleImage = nextTitleImage;
                titleScaledSize = nextTitleScaledSize;

                nextTitleImage = null;
                nextTitleScaledSize = Size.Empty;
                nextLayers = null;

                currentFolderIndex += animationDirection;

                // Re-cache the new current folder's images
                CacheScaledLayers(layers, cachedLayers, currentFolderIndex);

                SaveLastMenu();
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            // Parallax factors for each layer
            float[] factors = { 0f, 0.02f, 0.04f, 0.08f };
            int windowWidth = ClientSize.Width;
            int windowHeight = ClientSize.Height;
            int centerX = windowWidth / 2;
            int centerY = windowHeight / 2;
            int bottomY = windowHeight;
            int buttonMargin = 40;

            float baseScale = Math.Min(windowWidth / 1920f, windowHeight / 1080f);

            int parallaxRefX = centerX;
            int parallaxRefY = (currentFolderIndex == 0) ? centerY : bottomY;

            Point parallaxOrigin = new Point(parallaxRefX, parallaxRefY);
            Point parallaxMouse;
            if (Utilities.LowEndHardware)
                parallaxMouse = parallaxOrigin;
            else
                parallaxMouse = isAnimating ? parallaxOrigin : mousePos;

            for (int set = 0; set < (isAnimating ? 2 : 1); set++)
            {
                Bitmap[] drawLayers = set == 0 ? cachedLayers : cachedNextLayers;
                Image drawTitle = set == 0 ? titleImage : nextTitleImage;
                Size drawTitleSize = set == 0 ? titleScaledSize : nextTitleScaledSize;

                int xOffset = 0;

                int folderIdx = set == 0 ? currentFolderIndex : currentFolderIndex + animationDirection;
                bool grayscale = folderGrayscaled[Math.Max(0, Math.Min(folderIdx, 4))];

                if (isAnimating)
                {
                    xOffset = (set == 0)
                        ? -animationOffset * animationDirection
                        : (windowWidth - animationOffset) * animationDirection;
                }

                for (int i = 0; i < drawLayers.Length; i++)
                {
                    if (drawLayers == null || drawLayers[i] == null) continue;

                    Bitmap bmp = drawLayers[i];
                    float baseX = (windowWidth - bmp.Width) / 2f;
                    float baseY = (windowHeight - bmp.Height) / 2f;

                    float offsetX = grayscale ? 0 : (parallaxMouse.X - parallaxRefX) * factors[i];
                    float offsetY = grayscale ? 0 : (parallaxMouse.Y - parallaxRefY) * factors[i];

                    e.Graphics.DrawImage(
                        bmp,
                        baseX + offsetX + xOffset,
                        baseY + offsetY,
                        bmp.Width,
                        bmp.Height
                    );
                }

                if (drawTitle != null)
                {
                    int buttonWidth = playNormal.Width;
                    int buttonHeight = playNormal.Height;
                    int buttonY = windowHeight - buttonHeight - buttonMargin;
                    int titleX = (windowWidth - drawTitleSize.Width) / 2 + xOffset;
                    int titleY = buttonY - drawTitleSize.Height - 15;
                    e.Graphics.DrawImage(drawTitle, new Rectangle(titleX, titleY, drawTitleSize.Width, drawTitleSize.Height));
                }
            }

            // Draw left/right arrows
            int arrowMargin = 30;
            int arrowY = (windowHeight - leftArrow.Height) / 2;
            leftArrowRect = new Rectangle(arrowMargin, arrowY, leftArrow.Width, leftArrow.Height);
            rightArrowRect = new Rectangle(windowWidth - rightArrow.Width - arrowMargin, arrowY, rightArrow.Width, rightArrow.Height);

            if (!isAnimating)
            {
                if (currentFolderIndex == 0)
                {
                    e.Graphics.DrawImage(rightArrowHovered ? creditRightArrowHover : creditRightArrow, rightArrowRect);
                }
                else if (currentFolderIndex == 1)
                {
                    e.Graphics.DrawImage(leftArrowHovered ? creditLeftArrowHover : creditLeftArrow, leftArrowRect);
                    if (currentFolderIndex < folders.Length - 1)
                        e.Graphics.DrawImage(rightArrowHovered ? rightArrowHover : rightArrow, rightArrowRect);
                }
                else
                {
                    if (currentFolderIndex > 0)
                        e.Graphics.DrawImage(leftArrowHovered ? leftArrowHover : leftArrow, leftArrowRect);
                    if (currentFolderIndex < folders.Length - 1)
                        e.Graphics.DrawImage(rightArrowHovered ? rightArrowHover : rightArrow, rightArrowRect);
                }
            }

            if (currentFolderIndex == 0)
            {
                // Draw tutorial and trailer buttons
                int buttonSpacing = 30;

                Image tutorialImage = tutorialButtonPressed ? tutorialSelected :
                                      tutorialButtonHovered ? tutorialHovered : tutorialNormal;
                Image trailerImage = trailerButtonPressed ? trailerSelected :
                                     trailerButtonHovered ? trailerHovered : trailerNormal;

                int tutorialWidth = tutorialImage.Width;
                int tutorialHeight = tutorialImage.Height;
                int trailerWidth = trailerImage.Width;
                int trailerHeight = trailerImage.Height;

                int totalWidth = tutorialWidth + buttonSpacing + trailerWidth;
                int baseY = windowHeight - Math.Max(tutorialHeight, trailerHeight) - buttonMargin;
                int baseX = (windowWidth - totalWidth) / 2;

                tutorialButtonRect = new Rectangle(baseX, baseY, tutorialWidth, tutorialHeight);
                trailerButtonRect = new Rectangle(baseX + tutorialWidth + buttonSpacing, baseY, trailerWidth, trailerHeight);

                if (!isAnimating)
                {
                    e.Graphics.DrawImage(tutorialImage, tutorialButtonRect);
                    e.Graphics.DrawImage(trailerImage, trailerButtonRect);
                }
                return; // Skip drawing other buttons
            }

            if (currentFolderIndex > 0)
            {
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MCSM", $"Minecraft Story Mode Ep{currentFolderIndex}", "save.json");
                bool hasSave = File.Exists(savePath);

                int buttonSpacing = 30; // Space between continue and restart buttons

                if (!hasSave)
                {
                    // Draw play button
                    Image buttonImage;
                    bool isGrayscaled = folderGrayscaled[currentFolderIndex];
                    if (isGrayscaled)
                    {
                        if (playButtonPressed)
                            buttonImage = installSelected;
                        else if (playButtonHovered)
                            buttonImage = installHovered;
                        else
                            buttonImage = installNormal;
                    }
                    else
                    {
                        if (playButtonPressed)
                            buttonImage = playSelected;
                        else if (playButtonHovered)
                            buttonImage = playHovered;
                        else
                            buttonImage = playNormal;
                    }

                    int playButtonWidth = buttonImage.Width;
                    int playButtonHeight = buttonImage.Height;
                    int playButtonX = (windowWidth - playButtonWidth) / 2;
                    int playButtonY = windowHeight - playButtonHeight - buttonMargin;

                    playButtonRect = new Rectangle(playButtonX, playButtonY, playButtonWidth, playButtonHeight);

                    if (!isAnimating)
                        e.Graphics.DrawImage(buttonImage, playButtonRect);
                }
                else
                {
                    // Draw Continue and Restart buttons
                    Image continueImage;
                    Image restartImage;

                    if (continueButtonPressed)
                        continueImage = continueSelected;
                    else if (continueButtonHovered)
                        continueImage = continueHovered;
                    else
                        continueImage = continueNormal;

                    if (restartButtonPressed)
                        restartImage = restartSelected;
                    else if (restartButtonHovered)
                        restartImage = restartHovered;
                    else
                        restartImage = restartNormal;

                    int continueWidth = continueImage.Width;
                    int continueHeight = continueImage.Height;
                    int restartWidth = restartImage.Width;
                    int restartHeight = restartImage.Height;

                    int totalWidth = continueWidth + buttonSpacing + restartWidth;
                    int baseY = windowHeight - Math.Max(continueHeight, restartHeight) - buttonMargin;
                    int baseX = (windowWidth - totalWidth) / 2;

                    continueButtonRect = new Rectangle(baseX, baseY, continueWidth, continueHeight);
                    restartButtonRect = new Rectangle(baseX + continueWidth + buttonSpacing, baseY, restartWidth, restartHeight);

                    if (!isAnimating)
                    {
                        e.Graphics.DrawImage(continueImage, continueButtonRect);
                        e.Graphics.DrawImage(restartImage, restartButtonRect);
                    }
                }
            }
            /*
            string creditText = "Recreated by Eveep23, Art by Pox1016";
            using (Font font = new Font("Segoe UI", 24, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                SizeF textSize = e.Graphics.MeasureString(creditText, font);
                float textX = (windowWidth - textSize.Width) / 2;
                float textY = windowHeight - textSize.Height - 10;
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0)))
                {
                    e.Graphics.DrawString(creditText, font, shadowBrush, textX + 2, textY + 2);
                }
                e.Graphics.DrawString(creditText, font, brush, textX, textY);
            }
            */
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var img in layers)
                    img?.Dispose();
                if (nextLayers != null)
                {
                    foreach (var img in nextLayers)
                        img?.Dispose();
                }
                playNormal?.Dispose();
                playHovered?.Dispose();
                playSelected?.Dispose();
                installNormal?.Dispose();
                installHovered?.Dispose();
                installSelected?.Dispose();
                trailerNormal?.Dispose();
                trailerHovered?.Dispose();
                trailerSelected?.Dispose();
                tutorialNormal?.Dispose();
                tutorialHovered?.Dispose();
                tutorialSelected?.Dispose();
                continueNormal?.Dispose();
                continueHovered?.Dispose();
                continueSelected?.Dispose();
                restartNormal?.Dispose();
                restartHovered?.Dispose();
                restartSelected?.Dispose();
                titleImage?.Dispose();
                nextTitleImage?.Dispose();
                leftArrow?.Dispose();
                rightArrow?.Dispose();
                leftArrowHover?.Dispose();
                rightArrowHover?.Dispose();
                _hoverPlayer?.Dispose();
                _selectPlayer?.Dispose();
                _leftArrowSelectPlayer?.Dispose();
                _rightArrowSelectPlayer?.Dispose();
                _libVLC?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}