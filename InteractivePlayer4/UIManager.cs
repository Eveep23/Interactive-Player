using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using LibVLCSharp.Shared;
using System.Diagnostics;
using SharpDX.XInput;
using System.Threading.Tasks;
public static class UIManager
{
    private static readonly string ConfigFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

    public static void ShowNotificationUI(string notificationText, string movieFolder, string videoId, int displayDurationMs)
    {
        int formWidth = 1900;

        Form notificationForm = new Form
        {
            Text = "Notification",
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(41, 41, 41),
            TransparencyKey = Color.FromArgb(41, 41, 41),
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            ShowInTaskbar = false,
            Width = formWidth,
            Height = 200
        };

        notificationForm.Load += (sender, e) =>
        {
            int exStyle = GetWindowLong(notificationForm.Handle, GWL_EXSTYLE);
            SetWindowLong(notificationForm.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        };

        AlignNotificationWithVideoPlayer(notificationForm, videoId);

        // Calculate scaling factor based on the resized form
        double scaleFactor = (double)notificationForm.Width / formWidth;

        // Load settings
        var settings = LoadSettings();

        // Determine the notification background image based on the notification text and settings
        string notificationImagePath;
        if (notificationText == "Your story is changing." && settings.CustomStoryChangingNotification)
        {
            notificationImagePath = FindTexturePath(movieFolder, "changing_notification_2x.png");
        }
        else
        {
            notificationImagePath = FindTexturePath(movieFolder, "notification_2x.png");
        }

        Bitmap notificationImage = LoadBitmap(notificationImagePath);

        if (notificationImage == null)
        {
            Console.WriteLine("Notification image not found.");
            return;
        }

        int notificationWidth = (int)(notificationImage.Width * scaleFactor);
        int notificationHeight = (int)(notificationImage.Height * scaleFactor);

        var notificationPanel = new Panel
        {
            Size = new Size(notificationWidth, notificationHeight),
            Location = new System.Drawing.Point((notificationForm.Width - notificationWidth) / 2, (notificationForm.Height - notificationHeight) / 2),
            BackgroundImage = new Bitmap(notificationImage, new Size(notificationWidth, notificationHeight)),
            BackgroundImageLayout = ImageLayout.Stretch,
            BackColor = Color.Transparent,
            Padding = new Padding(10)
        };

        var textLabel = new Label
        {
            Text = notificationText,
            AutoSize = true,
            Font = new Font("Arial", (float)(26 * scaleFactor)),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };

        notificationPanel.Controls.Add(textLabel);

        int offsetX = (int)(17 * scaleFactor);
        textLabel.Location = new System.Drawing.Point((notificationPanel.Width - textLabel.Width) / 2 + offsetX, (notificationPanel.Height - textLabel.Height) / 2);

        notificationForm.Controls.Add(notificationPanel);

        // Load and play notification sound
        string notificationSoundPath = FindTexturePath(movieFolder, "sfx_notification.m4a");
        MediaPlayer notificationPlayer = null;
        if (File.Exists(notificationSoundPath))
        {
            Core.Initialize();
            var libVLC = new LibVLC();
            notificationPlayer = new MediaPlayer(new Media(libVLC, notificationSoundPath, FromType.FromPath));
            notificationPlayer.Play();
        }
        else
        {
            Console.WriteLine("Notification sound not found.");
        }

        // Set initial position above the VLC window
        IntPtr videoPlayerHandle = FindWindow(null, "VLC (Direct3D11 output)");
        if (videoPlayerHandle != IntPtr.Zero)
        {
            GetWindowRect(videoPlayerHandle, out RECT rect);
            int centerX = rect.Left;
            int initialY = rect.Top - notificationForm.Height;
            int targetY = rect.Top + 30;

            notificationForm.Location = new System.Drawing.Point(centerX, initialY);

            System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer { Interval = 10 };
            bool movingDown = true;
            bool delayCompleted = false;
            int delayCounter = 0;

            animationTimer.Tick += (sender, e) =>
            {
                if (movingDown)
                {
                    if (notificationForm.Location.Y < targetY)
                    {
                        notificationForm.Location = new System.Drawing.Point(notificationForm.Location.X, notificationForm.Location.Y + 10);
                    }
                    else
                    {
                        movingDown = false;
                        delayCounter = 0;
                    }
                }
                else if (!delayCompleted)
                {
                    delayCounter += animationTimer.Interval;
                    if (delayCounter >= displayDurationMs)
                    {
                        delayCompleted = true;
                    }
                }
                else
                {
                    if (notificationForm.Location.Y > initialY)
                    {
                        notificationForm.Location = new System.Drawing.Point(notificationForm.Location.X, notificationForm.Location.Y - 10);
                    }
                    else
                    {
                        animationTimer.Stop();
                        notificationForm.Close();
                    }
                }
            };

            animationTimer.Start();
            notificationForm.ShowDialog();
        }

        // Dispose of the MediaPlayer after the notification is closed
        notificationPlayer?.Dispose();
    }
    private static string FindTexturePath(string folder, string textureName)
    {
        var files = Directory.GetFiles(folder, textureName, SearchOption.AllDirectories);
        if (files.Length > 0)
        {
            return files[0];
        }
        return null;
    }

    private static void AlignNotificationWithVideoPlayer(Form notificationForm, string videoId)
    {
        IntPtr videoPlayerHandle = FindWindow(null, "VLC (Direct3D11 output)");
        if (videoPlayerHandle != IntPtr.Zero)
        {
            GetWindowRect(videoPlayerHandle, out RECT rect);

            int playerWidth = rect.Right - rect.Left;
            int playerHeight = rect.Bottom - rect.Top;

            notificationForm.Width = playerWidth;

            notificationForm.Height = (int)(playerHeight * 0.10);

            int centerX = rect.Left;
            int topY = rect.Top + 30;

            notificationForm.Location = new System.Drawing.Point(centerX, topY);
            SetWindowLong(notificationForm.Handle, GWL_HWNDPARENT, videoPlayerHandle);
        }
    }

    private static bool soundPlayed = false;
    private static int correctAnswersCount = 0;

    public static void ShowTutorialWindow(string headerText, string bodyText, int tutorialDurationMs, string videoId, string movieFolder)
    {
        if (videoId == "81481556")
        {
            return;
        }

        if (headerText == "Get ready to click!" && IsControllerConnected())
        {
            headerText = "Get ready to press!";
        }

        int baseWidth = 700;
        int baseHeight = 100;

        double scaleFactor = 1.0;
        int formWidth = baseWidth;
        int formHeight = baseHeight;
        int vlcX = 0, vlcY = 0;

        IntPtr vlcHandle = FindWindow(null, "VLC (Direct3D11 output)");
        if (vlcHandle != IntPtr.Zero && GetWindowRect(vlcHandle, out RECT rect))
        {
            int playerWidth = rect.Right - rect.Left;
            int playerHeight = rect.Bottom - rect.Top;
            scaleFactor = Math.Max(0.5, Math.Min(2.0, playerWidth / (double)baseWidth));
            formWidth = (int)(baseWidth * scaleFactor);
            formHeight = (int)(baseHeight * scaleFactor);

            if (videoId == "81271335")
            {
                vlcX = rect.Left;
                vlcY = rect.Top + (int)(24 * scaleFactor);
            }
            else
            {
                vlcX = rect.Left;
                vlcY = rect.Bottom - formHeight - (int)(32 * scaleFactor);
            }
        }

        Form tutorialForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            TopMost = true,
            StartPosition = FormStartPosition.Manual,
            BackColor = Color.FromArgb(41, 41, 41),
            TransparencyKey = Color.FromArgb(41, 41, 41),
            Width = formWidth,
            Height = formHeight,
            MaximizeBox = false,
            MinimizeBox = false,
            Opacity = 0
        };
        if (videoId == "81271335")
        {
            scaleFactor *= 0.37;

            string liveIndicatorsPath = FindTexturePath(movieFolder, "live_indicators_2x_v2.png");
            if (!string.IsNullOrEmpty(liveIndicatorsPath) && File.Exists(liveIndicatorsPath))
            {
                Bitmap liveIndicatorsSheet = new Bitmap(liveIndicatorsPath);
                int spriteCount = 4;
                int spriteHeight = liveIndicatorsSheet.Height / spriteCount;
                Bitmap spriteDead = liveIndicatorsSheet.Clone(new Rectangle(0, 0, liveIndicatorsSheet.Width, spriteHeight), liveIndicatorsSheet.PixelFormat);
                Bitmap sprite2 = liveIndicatorsSheet.Clone(new Rectangle(0, 1 * spriteHeight, liveIndicatorsSheet.Width, spriteHeight), liveIndicatorsSheet.PixelFormat);
                Bitmap sprite3 = liveIndicatorsSheet.Clone(new Rectangle(0, 2 * spriteHeight, liveIndicatorsSheet.Width, spriteHeight), liveIndicatorsSheet.PixelFormat);
                Bitmap sprite4 = liveIndicatorsSheet.Clone(new Rectangle(0, 3 * spriteHeight, liveIndicatorsSheet.Width, spriteHeight), liveIndicatorsSheet.PixelFormat);

                int indicatorSpacing = (int)(16 * scaleFactor);
                int indicatorWidth = (int)(liveIndicatorsSheet.Width * scaleFactor);
                int indicatorHeight = (int)(spriteHeight * scaleFactor);

                int startX = (int)(100 * scaleFactor);
                int y = (int)(10 * scaleFactor);

                string saveFilePath = Path.Combine(movieFolder, "save.json");
                var saveData = SaveManager.LoadSaveData(saveFilePath);
                var globalState = saveData?.GlobalState ?? new Dictionary<string, object>();
                var persistentState = saveData?.PersistentState ?? new Dictionary<string, object>();
                string infoJsonFile = Path.Combine(movieFolder, "info.json");
                int livesRemaining = PreconditionChecker.GetPreconditionValue("livesRemaining", globalState, persistentState, infoJsonFile) - 1;

                PictureBox[] indicators = new PictureBox[3];
                for (int i = 0; i < 3; i++)
                {
                    indicators[i] = new PictureBox
                    {
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Size = new Size(indicatorWidth, indicatorHeight),
                        BackColor = Color.Transparent,
                        Location = new Point(startX + i * (indicatorWidth + indicatorSpacing), y)
                    };
                    tutorialForm.Controls.Add(indicators[i]);
                    indicators[i].BringToFront();
                }
                int displayLives = Math.Min(livesRemaining + 1, 3);
                Action<int> updateIndicators = (lives) =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Bitmap newImage;
                        if (lives == 3)
                        {
                            newImage = sprite4;
                        }
                        else if (lives == 2)
                        {
                            newImage = (i < 1) ? spriteDead : sprite3;
                        }
                        else if (lives == 1)
                        {
                            newImage = (i < 2) ? spriteDead : sprite2;
                        }
                        else
                        {
                            newImage = spriteDead;
                        }

                        // Detect if this indicator just changed to dead
                        bool wasAlive = indicators[i].Image != null && indicators[i].Image != spriteDead;
                        bool nowDead = newImage == spriteDead;

                        indicators[i].Image = newImage;

                        if (wasAlive && nowDead)
                        {
                            var pb = indicators[i];
                            var originalSize = pb.Size;
                            var originalLocation = pb.Location;
                            int animDuration = 180; // ms
                            int animSteps = 12;
                            int step = 0;
                            System.Windows.Forms.Timer bounceTimer = new System.Windows.Forms.Timer { Interval = animDuration / animSteps };
                            bounceTimer.Tick += (s, e) =>
                            {
                                step++;
                                double animT = step / (double)animSteps;
                                double scaleY = 1.0 + 0.35 * Math.Sin(Math.PI * animT); // 1.0 -> 1.35 -> 1.0
                                double scaleX = 1.0 - 0.15 * Math.Sin(Math.PI * animT); // 1.0 -> 0.85 -> 1.0

                                int newW = (int)(originalSize.Width * scaleX);
                                int newH = (int)(originalSize.Height * scaleY);
                                pb.Size = new Size(newW, newH);
                                pb.Location = new Point(
                                    originalLocation.X + (originalSize.Width - newW) / 2,
                                    originalLocation.Y + (originalSize.Height - newH) / 2
                                );

                                if (step >= animSteps)
                                {
                                    bounceTimer.Stop();
                                    pb.Size = originalSize;
                                    pb.Location = originalLocation;
                                }
                            };
                            bounceTimer.Start();
                        }
                    }
                };

                updateIndicators(displayLives);

                tutorialForm.Shown += (s, e) =>
                {
                    var liveTimer = new System.Windows.Forms.Timer { Interval = 2000 };
                    liveTimer.Tick += (sender2, e2) =>
                    {
                        liveTimer.Stop();
                        updateIndicators(livesRemaining);
                    };
                    liveTimer.Start();
                };
            }
        }
        else
        {
            string audioPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "reengagement_notification.m4a");
            MediaPlayer tutorialPlayer = null;
            if (File.Exists(audioPath))
            {
                Core.Initialize();
                var libVLC = new LibVLC();
                tutorialPlayer = new MediaPlayer(new Media(libVLC, audioPath, FromType.FromPath));
                tutorialPlayer.Play();
            }

            string cursorIconPath;
            if (IsControllerConnected())
            {
                cursorIconPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Controller_icon.png");
            }
            else
            {
                cursorIconPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Cursor_icon.png");
            }
            int iconSize = (int)(100 * scaleFactor);
            PictureBox cursorPictureBox = new PictureBox
            {
                Image = File.Exists(cursorIconPath) ? Image.FromFile(cursorIconPath) : null,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(iconSize, iconSize),
                BackColor = Color.Transparent,
                Location = new Point((int)(16 * scaleFactor), (formHeight - iconSize) / 2)
            };

            int textAreaLeft = (int)(iconSize + 32 * scaleFactor);
            int textAreaWidth = formWidth - textAreaLeft - (int)(16 * scaleFactor);

            Label headerLabel = new ShadowLabel
            {
                Text = headerText,
                Font = new Font("Arial", (float)(20 * scaleFactor), FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = textAreaWidth,
                Height = (int)(36 * scaleFactor),
                Location = new Point(textAreaLeft, (int)(24 * scaleFactor)),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0)
            };

            Label bodyLabel = new ShadowLabel
            {
                Text = bodyText,
                Font = new Font("Arial", (float)(14 * scaleFactor), FontStyle.Regular),
                ForeColor = Color.WhiteSmoke,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = textAreaWidth,
                Height = (int)(formHeight - headerLabel.Bottom - (12 * scaleFactor)),
                Location = new Point(textAreaLeft, headerLabel.Bottom + (int)(6 * scaleFactor)),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0)
            };

            tutorialForm.Controls.Add(cursorPictureBox);
            tutorialForm.Controls.Add(headerLabel);
            tutorialForm.Controls.Add(bodyLabel);
        }

        if (vlcHandle != IntPtr.Zero)
        {
            if (videoId == "81271335")
            {
                tutorialForm.Location = new Point(vlcX, vlcY);
            }
            else
            {
                tutorialForm.Location = new Point(vlcX + (int)(24 * scaleFactor), vlcY);
            }  
        }
        else
        {
            tutorialForm.StartPosition = FormStartPosition.CenterScreen;
        }

        // Fade out
        bool isFadingOut = false;
        tutorialForm.FormClosing += (s, e) =>
        {
            if (!isFadingOut && tutorialForm.Opacity > 0)
            {
                e.Cancel = true;
                isFadingOut = true;
                var fadeOutTimer = new System.Windows.Forms.Timer { Interval = 15 };
                fadeOutTimer.Tick += (sender, args) =>
                {
                    if (tutorialForm.Opacity > 0)
                    {
                        tutorialForm.Opacity = Math.Max(0, tutorialForm.Opacity - 0.05);
                    }
                    else
                    {
                        fadeOutTimer.Stop();
                        tutorialForm.Close();
                    }
                };
                fadeOutTimer.Start();
            }
        };

        System.Threading.Thread t = new System.Threading.Thread(() =>
        {
            tutorialForm.Shown += async (s, e) =>
            {
                // Fade in
                var fadeTimer = new System.Windows.Forms.Timer { Interval = 15 };
                fadeTimer.Tick += (sender, args) =>
                {
                    if (tutorialForm.Opacity < 1.0)
                    {
                        tutorialForm.Opacity = Math.Min(1.0, tutorialForm.Opacity + 0.05);
                    }
                    else
                    {
                        fadeTimer.Stop();
                    }
                };
                fadeTimer.Start();

                await Task.Delay(tutorialDurationMs);
                if (tutorialForm.IsHandleCreated)
                {
                    tutorialForm.Invoke(new Action(() => tutorialForm.Close()));
                }
            };
            Application.Run(tutorialForm);
        });
        t.IsBackground = true;
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
    }

    public static (string segmentId, string choiceId) ShowChoiceUI(List<Choice> choices, List<Bitmap> buttonSprites, List<Bitmap> buttonIcons, int timeLimitMs, string movieFolder, string videoId, Segment segment, string headerText = null)
    {
        string selectedSegmentId = null;
        string selectedChoiceId = null;
        bool inputCaptured = false;

        correctAnswersCount = 0;

        soundPlayed = false;

        int formWidth = 1900;

        Form choiceForm = new Form
        {
            Text = "Make a Choice",
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = (videoId == "81131714" && segment.LayoutType == "l6") ? Color.Magenta :
                (videoId == "80149064" ? Color.FromArgb(15, 15, 15) :
                (videoId == "80151644" ? Color.FromArgb(125, 125, 125) :
                (videoId == "81004016" || videoId == "80988062" || videoId == "81271335" && segment.LayoutType == "l1" ? Color.Black :
                (videoId == "81131714" ? Color.FromArgb(247, 233, 95) :
                Color.FromArgb(41, 41, 41))))),
            TransparencyKey = (videoId == "81131714" && segment.LayoutType == "l6") ? Color.Magenta :
                      ((videoId == "81004016" || videoId == "80988062" || videoId == "81131714" || videoId == "81271335" && segment.LayoutType == "l1") ? Color.Empty :
                      (videoId == "80149064" ? Color.FromArgb(15, 15, 15) :
                      (videoId == "80151644" ? Color.FromArgb(125, 125, 125) :
                      Color.FromArgb(41, 41, 41)))),
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            ShowInTaskbar = false,
            Width = formWidth,
            Height = 450,
            Opacity = 0
        };

        choiceForm.Load += (sender, e) =>
        {
            int exStyle = GetWindowLong(choiceForm.Handle, GWL_EXSTYLE);
            SetWindowLong(choiceForm.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        };

        AlignWithVideoPlayer(choiceForm, videoId, segment);

        if (videoId == "81271335" && segment.LayoutType == "l1")
        {
            string backgroundFileName = "lvl1_2x.png"; // Default
            if (segment != null && !string.IsNullOrEmpty(segment.Id))
            {
                if (segment.Id.StartsWith("s1"))
                    backgroundFileName = "lvl1_2x.png";
                else if (segment.Id.StartsWith("s2"))
                    backgroundFileName = "lvl2_2x.png";
                else if (segment.Id.StartsWith("s3"))
                    backgroundFileName = "lvl3_2x.png";
                else if (segment.Id.StartsWith("s4"))
                    backgroundFileName = "lvl4_2x.png";
                else if (segment.Id.StartsWith("s5"))
                    backgroundFileName = "lvl5_2x.png";
                else if (segment.Id.StartsWith("s6"))
                    backgroundFileName = "lvl6_2x.png";
            }

            // Find the background path
            string backgroundPath = FindTexturePath(movieFolder, new[] { backgroundFileName });
            if (!string.IsNullOrEmpty(backgroundPath) && File.Exists(backgroundPath))
            {
                choiceForm.BackgroundImage = new Bitmap(backgroundPath);
                choiceForm.BackgroundImageLayout = ImageLayout.Stretch;
            }
        }

        // Load settings
        var settings = LoadSettings();

        // Calculate scaling factor based on the resized form
        double scaleFactor = (double)choiceForm.Width / formWidth;

        // Apply additional scaling for specific video ID
        if (videoId == "10000001" || videoId == "10000003" || videoId == "81481556" || videoId == "81251335" || videoId == "81271335" || videoId == "81287545" || videoId == "80149064" || videoId == "81260654" || videoId == "80994695" || videoId == "81328829" || videoId == "81058723" || videoId == "81054409" || videoId == "81108751" || videoId == "81004016" || videoId == "80988062" || videoId == "81131714" || videoId == "81205738" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205737" || videoId == "81054415" || videoId == "81175265" || videoId == "81019938" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
        {
            scaleFactor *= 0.75;
        }

        int buttonHeight = (int)(60 * scaleFactor);
        int horizontalSpacing = (int)(10 * scaleFactor);
        int buttonTopMargin = (int)(20 * scaleFactor);

        List<int> buttonWidths = new List<int>();
        List<Button> buttons = new List<Button>();

        // Add header image for video ID 81271335
        if (videoId == "81271335" && segment.HeaderImage != null && !string.IsNullOrEmpty(segment.HeaderImage.Url))
        {
            string headerImagePath = FindTexturePath(movieFolder, new[] { Path.GetFileName(new Uri(segment.HeaderImage.Url).LocalPath) });
            if (!string.IsNullOrEmpty(headerImagePath) && File.Exists(headerImagePath))
            {
                Bitmap headerImage = new Bitmap(headerImagePath);

                Panel headerPanel = new Panel
                {
                    Width = choiceForm.Width,
                    Height = (int)(headerImage.Height * ((double)choiceForm.Width / headerImage.Width)),
                    BackColor = Color.Transparent
                };

                PictureBox headerPictureBox = new PictureBox
                {
                    Image = headerImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent
                };

                headerPanel.Controls.Add(headerPictureBox);
                choiceForm.Controls.Add(headerPanel);

                headerPanel.SendToBack();

                headerPanel.Location = new Point(0, 0);

                buttonTopMargin += headerPanel.Height;
            }
        }

        if (!string.IsNullOrEmpty(headerText) && videoId == "81481556" && segment.LayoutType == "l2")
        {
            var headerLabel = new ShadowLabel
            {
                Text = headerText,
                AutoSize = false,
                Width = choiceForm.Width,
                Height = (int)(70 * scaleFactor),
                Font = new Font("Arial", (float)(35 * scaleFactor), FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding((int)(10 * scaleFactor)),
                ShadowColor = Color.Black,
                ShadowOffset = (int)(2 * scaleFactor)
            };
            headerLabel.Location = new Point(0, (int)(choiceForm.Height * 0.69));
            headerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            choiceForm.Controls.Add(headerLabel);
            headerLabel.BringToFront();

            using (var g = choiceForm.CreateGraphics())
            {
                SizeF textSize = g.MeasureString(headerLabel.Text, headerLabel.Font);
                headerLabel.Width = (int)textSize.Width + headerLabel.Padding.Left + headerLabel.Padding.Right;
                headerLabel.Left = (choiceForm.Width - headerLabel.Width) / 2;
            }
        }

        // Initialize VLC
        Core.Initialize();
        var libVLC = new LibVLC();

        // Load sound files
        string appearSoundPath = FindTexturePath(movieFolder, new[] { "sfx_appears_44100.m4a", "sfx_appears.m4a" });
        string hoverSoundPath = FindTexturePath(movieFolder, new[] { "CSD_Hover.m4a", "cap_focus.m4a", "focus_64.m4a", "sfx_focus.m4a", "sfx_focus_44100.m4a", "toggle.m4a", "sfx_focus.m4a", "IX_choicePointSound_tonal_focus_48k.m4a", "toggle.m4a", "sfx_triviaAnswerFocusHover.m4a" });
        string selectSoundPath = FindTexturePath(movieFolder, new[] { "CSD_Select.m4a", "cap_select.m4a", "selected_64.m4a", "sfx_select.m4a", "sfx_selected_44100.m4a", "select.m4a", "spirit_select_48.m4a", "sfx_buttonSelect.m4a", "IX_choicePointSound_tonal_select_48k.m4a", "sfx_select_44100.m4a", "select.m4a", "PIB_Choice_Ding.m4a" });
        string timeoutSoundPath = FindTexturePath(movieFolder, new[] { "sfx_timeout_44100.m4a", "sfx_timeout.m4a", "IX_choicePointSound_tonal_timeout_48k.m4a", "timeout.m4a" });
        string tooltipImagePath = FindTexturePath(movieFolder, new[] {"tooltip_top_2x.png" });
        string correctSoundPath = FindTexturePath(movieFolder, new[] { "sfx_select_correct.m4a" });
        string incorrectSoundPath = FindTexturePath(movieFolder, new[] { "sfx_select_incorrect.m4a" });

        if (videoId == "81131714" && segment.LayoutType == "l6")
        {
            hoverSoundPath = null;
            selectSoundPath = null;
        }

        // Play appear sound
        if (File.Exists(appearSoundPath))
        {
            var appearPlayer = new MediaPlayer(new Media(libVLC, appearSoundPath, FromType.FromPath));
            appearPlayer.Play();
        }

        for (int i = 0; i < choices.Count; i++)
        {
            var spriteSheet = buttonSprites[i];
            if (spriteSheet != null)
            {
                Bitmap defaultSprite = ExtractSprite(spriteSheet, 0);
                buttonWidths.Add((int)(defaultSprite.Width * scaleFactor));
            }
            else
            {
                buttonWidths.Add((int)(300 * scaleFactor));
            }
        }

        int totalButtonsWidth = buttonWidths.Sum();
        int availableSpace = choiceForm.Width - totalButtonsWidth;
        int spacing = availableSpace / (choices.Count + 1);

        int currentX;

        // Adjust spacing and starting position for specific video ID
        if (videoId == "10000001")
        {
            spacing /= 4;
            currentX = (choiceForm.Width - totalButtonsWidth - spacing * (choices.Count - 1)) / 2;
        }
        else
        {
            currentX = spacing;
        }

        Bitmap tooltipImage = LoadBitmap(tooltipImagePath);

        // Add tooltip PictureBox to the choiceForm instead of the button
        PictureBox tooltipPictureBox = new PictureBox
        {
            Image = tooltipImage,
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Visible = false // Initially hidden
        };
        choiceForm.Controls.Add(tooltipPictureBox);

        for (int i = 0; i < choices.Count; i++)
        {
            var spriteSheet = buttonSprites[i];
            if (spriteSheet != null)
            {
                Bitmap defaultSprite, focusedSprite, selectedSprite, correctSprite = null, incorrectSprite = null;
                if (videoId == "81481556" && segment.LayoutType == "l2")
                {
                    defaultSprite = ExtractSprite(spriteSheet, 1, 6);
                    focusedSprite = ExtractSprite(spriteSheet, 2, 6);
                    selectedSprite = ExtractSprite(spriteSheet, 3, 6);
                    correctSprite = ExtractSprite(spriteSheet, 4, 6);
                    incorrectSprite = ExtractSprite(spriteSheet, 5, 6);
                }
                else if (videoId == "81481556" && segment.LayoutType == "l0")
                {
                    defaultSprite = ExtractSprite(spriteSheet, 1, 6);
                    focusedSprite = ExtractSprite(spriteSheet, 2, 6);
                    selectedSprite = ExtractSprite(spriteSheet, 3, 6);
                    correctSprite = ExtractSprite(spriteSheet, 4, 6);
                    incorrectSprite = ExtractSprite(spriteSheet, 5, 6);
                }
                else if (videoId == "81271335" && segment.LayoutType == "l1")
                {
                    defaultSprite = spriteSheet;
                    focusedSprite = spriteSheet;
                    selectedSprite = spriteSheet;
                }
                else
                {
                    defaultSprite = ExtractSprite(spriteSheet, 0);
                    focusedSprite = ExtractSprite(spriteSheet, 1);
                    selectedSprite = ExtractSprite(spriteSheet, 2);
                }

                int buttonWidth = buttonWidths[i];
                buttonHeight = (int)(defaultSprite.Height * scaleFactor);

                var button = new Button
                {
                    Text = (videoId == "81131714" && segment.LayoutType == "l6" || segment.LayoutType == "ReubenZone" || segment.LayoutType == "EnderconZone" || segment.LayoutType == "TempleZone" || segment.LayoutType == "Crafting" || segment.LayoutType == "EpisodeEnd" || segment.LayoutType == "RedstoniaZone" || segment.LayoutType == "MCSMThroneZone" || segment.LayoutType == "MCSMTownZone" || segment.LayoutType == "MCSMWoolLand" || segment.LayoutType == "MCSMLabZone" || segment.LayoutType == "MCSMGunZone" || segment.LayoutType == "IvorZone" || videoId == "81271335" && segment.LayoutType == "l1") ? string.Empty : (new[] { "80149064", "80135585", "81054409", "81287545", "81019938", "81260654", "81054415", "81058723", "80227815", "81250260", "81250261", "81250262", "81250263", "81250264", "81250265", "81250266", "81250267" }.Contains(videoId)) ? string.Empty : choices[i].Text,
                    Size = new Size(buttonWidth, buttonHeight),
                    Location = new System.Drawing.Point(0, 0), // Position within the panel
                    BackgroundImage = new Bitmap(defaultSprite, new Size(buttonWidth, buttonHeight)),
                    BackgroundImageLayout = ImageLayout.Stretch,
                    Tag = choices[i].SegmentId,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    UseVisualStyleBackColor = false,
                    TabStop = false,
                    Font = new Font("Arial", (float)(videoId == "10000001" ? 28 * scaleFactor : 22 * scaleFactor), videoId == "10000001" ? FontStyle.Regular : FontStyle.Bold),
                    ForeColor = (videoId == "81481556") ? Color.Black :
                                (videoId == "81328829") ? Color.Black :
                                (new[] { "80227804", "80227805", "80227800", "80227801", "80227802", "80227803", "80227699", "80227698" }.Contains(videoId)) ? ColorTranslator.FromHtml("#27170a") :
                                (videoId == "81131714" ? ColorTranslator.FromHtml("#7705ad") : Color.White),
                    TextAlign = (new[] { "81004016", "81205738", "81108751", "80151644", "80227804", "80227805", "80227800", "80227801", "80227802", "80227803", "80227699", "80227698", "81319137" }.Contains(videoId)) ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter,
                    Padding = (new[] { "81004016", "81205738", "81108751", "80151644", "80227804", "80227805", "80227800", "80227801", "80227802", "80227803", "80227699", "80227698", "81319137" }.Contains(videoId)) ? new Padding((int)(buttonWidth * 0.4), 0, 0, 0) : new Padding(0)
                };

                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseDownBackColor = Color.Transparent;
                button.FlatAppearance.MouseOverBackColor = Color.Transparent;
                button.FlatAppearance.CheckedBackColor = Color.Transparent;

                button.MouseEnter += (sender, e) =>
                {
                    if (button.Enabled)
                    {
                        if (videoId == "10000001" || videoId == "80988062")
                        {
                            EaseIntoFocusedSprite(button, defaultSprite, focusedSprite, 65);
                        }
                        else
                        {
                            button.BackgroundImage = new Bitmap(focusedSprite, new Size(buttonWidth, buttonHeight));
                        }

                        if (videoId == "81131714")
                        {
                            button.ForeColor = ColorTranslator.FromHtml("#dc007f");
                        }

                        if (File.Exists(hoverSoundPath))
                        {
                            var hoverPlayer = new MediaPlayer(new Media(libVLC, hoverSoundPath, FromType.FromPath));
                            hoverPlayer.Play();
                        }
                        // Show tooltip only if videoId is 80227815 and the choice has "subText"
                        if (videoId == "80227815999")
                        {
                            tooltipPictureBox.BringToFront();
                            tooltipPictureBox.Location = new Point(button.Parent.Location.X + button.Location.X + (button.Width - tooltipPictureBox.Width) / 2, button.Parent.Location.Y + button.Location.Y - tooltipPictureBox.Height);
                            tooltipPictureBox.Visible = true;
                        }
                    }
                };
                button.MouseLeave += (sender, e) =>
                {
                    if (button.Enabled)
                    {
                        if (videoId == "10000001" || videoId == "80988062")
                        {
                            EaseOutToDefaultSprite(button, defaultSprite, focusedSprite, 65);
                        }
                        else
                        {
                            button.BackgroundImage = new Bitmap(defaultSprite, new Size(buttonWidth, buttonHeight));
                        }

                        if (videoId == "81131714")
                        {
                            button.ForeColor = ColorTranslator.FromHtml("#7705ad");
                        }

                        tooltipPictureBox.Visible = false; // Hide the tooltip
                    }
                };
                button.MouseDown += (sender, e) =>
                {
                    if (button.Enabled)
                    {
                        button.BackgroundImage = new Bitmap(selectedSprite, new Size(buttonWidth, buttonHeight));
                    }
                };
                button.MouseUp += (sender, e) =>
                {
                    if (button.Enabled)
                    {
                        button.BackgroundImage = new Bitmap(focusedSprite, new Size(buttonWidth, buttonHeight));
                    }
                };

                button.Click += (sender, e) =>
                {
                    if (!inputCaptured)
                    {
                        selectedSegmentId = (string)((Button)sender).Tag;
                        var clickedButton = (Button)sender;
                        int clickedIndex = buttons.IndexOf(clickedButton);
                        if (clickedIndex >= 0 && clickedIndex < choices.Count)
                        {
                            selectedChoiceId = choices[clickedIndex].Id;
                        }
                        inputCaptured = true;

                        if (videoId == "81481556" && segment.LayoutType == "l2" && segment.CorrectIndex.HasValue)
                        {
                            if (clickedIndex == segment.CorrectIndex.Value)
                            {
                                button.BackgroundImage = new Bitmap(correctSprite, new Size(buttonWidth, buttonHeight));
                            }
                            else
                            {
                                button.BackgroundImage = new Bitmap(incorrectSprite, new Size(buttonWidth, buttonHeight));
                                var correctButton = buttons[segment.CorrectIndex.Value];
                                correctButton.BackgroundImage = new Bitmap(ExtractSprite(buttonSprites[segment.CorrectIndex.Value], 0, 6), new Size(buttonWidths[segment.CorrectIndex.Value], buttonHeight));
                            }
                        }
                        else
                        {
                            button.BackgroundImage = new Bitmap(selectedSprite, new Size(buttonWidth, buttonHeight));
                        }
                        button.Enabled = false;

                        if (videoId == "81481556" && segment.LayoutType == "l2" && segment.CorrectIndex.HasValue)
                        {
                            string soundPath = (clickedIndex == segment.CorrectIndex.Value) ? correctSoundPath : incorrectSoundPath;
                            if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                            {
                                var soundPlayer = new MediaPlayer(new Media(libVLC, soundPath, FromType.FromPath));
                                soundPlayer.Play();
                            }
                        }

                        if (videoId == "81271335" && segment.LayoutType == "l1")
                        { 
                            // Determine if the selected choice is correct
                            bool isCorrect = false;
                            if (segment.AnswerSequence != null && segment.AnswerSequence.Count > 0)
                            {
                                int choiceSetIndex = buttons.IndexOf(button) / segment.ChoiceSets[0].Count;
                                int correctIndex = segment.AnswerSequence.ElementAtOrDefault(choiceSetIndex);
                                isCorrect = buttons.IndexOf(button) % segment.ChoiceSets[0].Count == correctIndex;
                            }

                            Console.WriteLine(isCorrect ? "Correct choice selected." : "Incorrect choice selected.");

                            if (isCorrect)
                            {
                                correctAnswersCount++;
                            }

                            Console.WriteLine("Correct choice count: " + correctAnswersCount);

                            // Play the appropriate sound
                            string soundPath = isCorrect ? correctSoundPath : incorrectSoundPath;
                            if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                            {
                                var soundPlayer = new MediaPlayer(new Media(libVLC, soundPath, FromType.FromPath));
                                soundPlayer.Play();
                            }
                        }
                        else foreach (var btn in buttons)
                        {
                            if (btn != button)
                            {
                                btn.Enabled = false;
                            }
                        }

                        if (File.Exists(selectSoundPath))
                        {
                            var selectPlayer = new MediaPlayer(new Media(libVLC, selectSoundPath, FromType.FromPath));
                            selectPlayer.Play();
                        }

                        if (videoId == "80988062" && choices.Any(choice => choice.Text?.Equals("GO BACK", StringComparison.OrdinalIgnoreCase) == true) || videoId == "81481556" && segment.LayoutType == "l1" || videoId == "81481556" && segment.LayoutType == "l0" || videoId == "80988062" && choices.Any(choice => choice.Text?.Equals("EXIT TO CREDITS", StringComparison.OrdinalIgnoreCase) == true) || videoId == "81131714" && choices.Any(choice => choice.Text?.Equals("EXIT TO CREDITS", StringComparison.OrdinalIgnoreCase) == true) || videoId == "81131714" && segment.LayoutType == "l6" || videoId == "10000001" || videoId == "10000003" || videoId == "81251335" || videoId == "80994695" || videoId == "80135585" || videoId == "81328829" || videoId == "81205738" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205737" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
                        {
                            choiceForm.Close(); // Close the form immediately after a choice is made
                        }
                        else if (videoId == "81481556" && segment.LayoutType == "l2")
                        {
                            // Delay closing the form by about 2 seconds (2000 ms)
                            Task.Delay(2000).ContinueWith(_ =>
                            {
                                if (choiceForm.IsHandleCreated)
                                {
                                    choiceForm.Invoke(new Action(() => choiceForm.Close()));
                                }
                            });
                        }
                        else
                        {
                            if (videoId == "81271335" && segment.LayoutType == "l1")
                            {
                                inputCaptured = false;
                                button.Enabled = true;
                            }
                            else
                            {
                                choiceForm.ActiveControl = null;
                            } 
                        }
                    }
                };

                // Adjust height to accommodate text only if the video ID matches
                int panelHeight = (new[] { "81054409", "81287545", "81019938", "81260654", "81054415", "81058723" }.Contains(videoId) || segment.LayoutType == "ReubenZone" || segment.LayoutType == "EnderconZone" || segment.LayoutType == "TempleZone" || segment.LayoutType == "Crafting" || segment.LayoutType == "EpisodeEnd" || segment.LayoutType == "RedstoniaZone" || segment.LayoutType == "MCSMThroneZone" || segment.LayoutType == "MCSMTownZone" || segment.LayoutType == "MCSMWoolLand" || segment.LayoutType == "MCSMLabZone" || segment.LayoutType == "MCSMGunZone" || segment.LayoutType == "IvorZone") ? buttonHeight + (int)(50 * scaleFactor) : buttonHeight;

                var buttonPanel = new Panel
                {
                    Size = new Size(buttonWidth, panelHeight),
                    Location = new System.Drawing.Point(currentX, buttonTopMargin),
                    BackColor = Color.Transparent
                };

                if (videoId == "81271335" && segment.LayoutType == "l1")
                {
                    // Align the left button with the left side of the window
                    if (i == 0)
                    {
                        buttonPanel.Location = new System.Drawing.Point(0, buttonTopMargin);
                    }
                    // Align the right button with the right side of the window
                    else if (i == 1)
                    {
                        buttonPanel.Location = new System.Drawing.Point(choiceForm.Width - buttonPanel.Width, buttonTopMargin);
                    }
                    // Store the rest offscreen
                    else
                    {
                        buttonPanel.Location = new System.Drawing.Point(-buttonPanel.Width, buttonTopMargin);
                    }

                    button.Click += async (sender, e) =>
                    {
                        // Determine which button was clicked
                        int clickedIndex = buttons.IndexOf((Button)sender);

                        if (clickedIndex == 0 || clickedIndex == 1)
                        {
                            buttons[0].Parent.Location = new System.Drawing.Point(-buttons[0].Parent.Width, buttonTopMargin);
                            buttons[1].Parent.Location = new System.Drawing.Point(-buttons[1].Parent.Width, buttonTopMargin);

                            await Task.Delay(1000);

                            buttons[2].Parent.Location = new System.Drawing.Point(0, buttonTopMargin);
                            buttons[3].Parent.Location = new System.Drawing.Point(choiceForm.Width - buttons[3].Parent.Width, buttonTopMargin);
                        }
                        else if (clickedIndex == 2 || clickedIndex == 3)
                        {
                            buttons[2].Parent.Location = new System.Drawing.Point(-buttons[2].Parent.Width, buttonTopMargin);
                            buttons[3].Parent.Location = new System.Drawing.Point(-buttons[3].Parent.Width, buttonTopMargin);

                            await Task.Delay(1000);

                            buttons[4].Parent.Location = new System.Drawing.Point(0, buttonTopMargin);
                            buttons[5].Parent.Location = new System.Drawing.Point(choiceForm.Width - buttons[5].Parent.Width, buttonTopMargin);
                        }
                        else if (clickedIndex == 4 || clickedIndex == 5)
                        {
                            buttons[4].Parent.Location = new System.Drawing.Point(-buttonPanel.Width, buttonTopMargin);
                            buttons[5].Parent.Location = new System.Drawing.Point(-buttonPanel.Width, buttonTopMargin);
                        }

                        // Force the form to redraw to reflect the changes
                        choiceForm.Invalidate();
                    };
                }

                if (videoId == "81131714" && segment.LayoutType == "l6")
                {
                    if (choices[i].Text == "SKIP INTRO")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.8), (int)(choiceForm.Height * 0.1));
                    }
                }

                if (videoId == "81481556" && segment.LayoutType == "l2")
                {
                    int col = i % 2;
                    int row = i / 2;
                    double[] colFactors = { 0.10, 0.50 };
                    double[] rowFactors = { 0.74, 0.83 };
                    if (i < 4)
                    {
                        buttonPanel.Location = new System.Drawing.Point(
                            (int)(choiceForm.Width * colFactors[col]),
                            (int)(choiceForm.Height * rowFactors[row])
                        );
                    }
                }

                if (videoId == "81481556" && segment.LayoutType == "l1")
                {
                    int col = i % 2;
                    int row = i / 2;
                    double[] colFactors = { 0.22, 0.49 };
                    double[] rowFactors = { 0.74, 0.83 };
                    if (i < 4)
                    {
                        buttonPanel.Location = new System.Drawing.Point(
                            (int)(choiceForm.Width * colFactors[col]),
                            (int)(choiceForm.Height * rowFactors[row])
                        );
                    }
                }

                // Minecraft Story Mode Custom Positioning
                // Custom positioning for "MCSMTeamName"
                if (segment.LayoutType == "MCSMTeamName")
                {
                    if (choices[i].Text == "We're the Nether Maniacs.")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.15), (int)(choiceForm.Height * 0.71));
                    }
                    else if (choices[i].Text == "We're the Dead Enders.")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.335), (int)(choiceForm.Height * 0.78));
                    }
                    else if (choices[i].Text == "We're the Order of the Pig.")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.51), (int)(choiceForm.Height * 0.71));
                    }
                    else if (choices[i].Text == "TNT launcher")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.15), (int)(choiceForm.Height * 0.71));
                    }
                    else if (choices[i].Text == "Flying machine")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.335), (int)(choiceForm.Height * 0.78));
                    }
                    else if (choices[i].Text == "Rocket minecart")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.51), (int)(choiceForm.Height * 0.71));
                    }
                }

                // Custom positioning for "Crafting"
                if (segment.LayoutType == "Crafting")
                {
                    if (choices[i].Text == "Craft Lever")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.10), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Bow")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.10), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Sticky Piston")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.10), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Diamond Hoe")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.10), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Anvil")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.10), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Sword")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.76), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Fishing Pole")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.76), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Redstone Block")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.76), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Diamond Pickaxe")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.76), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Craft Boots")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.76), (int)(choiceForm.Height * 0.15));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    textLabel.Location = new System.Drawing.Point((buttonPanel.Width - textLabel.Width) / 2, buttonHeight + 10);
                }

                // Custom positioning for "ReubenZone"
                if (segment.LayoutType == "ReubenZone")
                {
                    if (choices[i].Text == "The Well")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.13), (int)(choiceForm.Height * 0.37));
                    }
                    else if (choices[i].Text == "Bush")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.31), (int)(choiceForm.Height * 0.44));
                    }
                    else if (choices[i].Text == "Smoke Trail")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.60), (int)(choiceForm.Height * 0.31));
                    }
                    else if (choices[i].Text == "Pigs")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.75), (int)(choiceForm.Height * 0.51));
                    }
                    else if (choices[i].Text == "Tall Grass")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.45), (int)(choiceForm.Height * 0.25));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "EnderconZone"
                if (segment.LayoutType == "EnderconZone")
                {
                    if (choices[i].Text == "Slime")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.22), (int)(choiceForm.Height * 0.26));
                    }
                    else if (choices[i].Text == "Chicken Machine")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.34), (int)(choiceForm.Height * 0.20));
                    }
                    else if (choices[i].Text == "Lukas")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.63), (int)(choiceForm.Height * 0.30));
                    }
                    else if (choices[i].Text == "Crafting Table")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.77), (int)(choiceForm.Height * 0.31));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "RedstoniaZone"
                if (segment.LayoutType == "RedstoniaZone")
                {
                    if (choices[i].Text == "Auto Farm")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.20), (int)(choiceForm.Height * 0.50));
                    }
                    else if (choices[i].Text == "Chest")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.32), (int)(choiceForm.Height * 0.41));
                    }
                    else if (choices[i].Text == "Crafting Table")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.43), (int)(choiceForm.Height * 0.55));
                    }
                    else if (choices[i].Text == "Intellectual")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.68), (int)(choiceForm.Height * 0.35));
                    }
                    else if (choices[i].Text == "Steal Repeator")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.745), (int)(choiceForm.Height * 0.47));
                    }
                    else if (choices[i].Text == "School Boy")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.77), (int)(choiceForm.Height * 0.47));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "TempleZone"
                if (segment.LayoutType == "TempleZone")
                {
                    if (choices[i].Text == "Axel")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.23), (int)(choiceForm.Height * 0.29));
                    }
                    else if (choices[i].Text == "Lukas")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.36), (int)(choiceForm.Height * 0.27));
                    }
                    else if (choices[i].Text == "Pedestal")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.50), (int)(choiceForm.Height * 0.36));
                    }
                    else if (choices[i].Text == "Olivia")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.64), (int)(choiceForm.Height * 0.32));
                    }
                    else if (choices[i].Text == "Levers")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.78), (int)(choiceForm.Height * 0.36));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "IvorZone"
                if (segment.LayoutType == "IvorZone")
                {
                    if (choices[i].Text == "Bookcase")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.175), (int)(choiceForm.Height * 0.21));
                    }
                    else if (choices[i].Text == "Gabriel")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.38), (int)(choiceForm.Height * 0.30));
                    }
                    else if (choices[i].Text == "Petra")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.38), (int)(choiceForm.Height * 0.28));
                    }
                    else if (choices[i].Text == "Chest")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.54), (int)(choiceForm.Height * 0.13));
                    }
                    else if (choices[i].Text == "Crafting Table")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.485), (int)(choiceForm.Height * 0.13));
                    }
                    else if (choices[i].Text == "Redstone Hole")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.775), (int)(choiceForm.Height * 0.185));
                    }
                    else if (choices[i].Text == "Lever")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.765), (int)(choiceForm.Height * 0.185));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "MCSMThroneZone"
                if (segment.LayoutType == "MCSMThroneZone")
                {
                    if (choices[i].Text == "Bookcase")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.10), (int)(choiceForm.Height * 0.37));
                    }
                    else if (choices[i].Text == "Cobblestone")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.215), (int)(choiceForm.Height * 0.545));
                    }
                    else if (choices[i].Text == "Crafting Table")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.425), (int)(choiceForm.Height * 0.545));
                    }
                    else if (choices[i].Text == "Dry Bush")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.55), (int)(choiceForm.Height * 0.535));
                    }
                    else if (choices[i].Text == "Strange Wall")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.745), (int)(choiceForm.Height * 0.47));
                    }
                    else if (choices[i].Text == "Lever Slot")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.740), (int)(choiceForm.Height * 0.475));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "MCSMTownZone"
                if (segment.LayoutType == "MCSMTownZone")
                {
                    if (choices[i].Text == "Crafting Table")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.14), (int)(choiceForm.Height * 0.23));
                    }
                    else if (choices[i].Text == "Garden")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.80), (int)(choiceForm.Height * 0.31));
                    }
                    else if (choices[i].Text == "Castle Guard")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.14), (int)(choiceForm.Height * 0.21));
                    }
                    else if (choices[i].Text == "Build Site")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.665), (int)(choiceForm.Height * 0.24));
                    }
                    else if (choices[i].Text == "Innkeeper")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.145), (int)(choiceForm.Height * 0.205));
                    }
                    else if (choices[i].Text == "Townspeople")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.60), (int)(choiceForm.Height * 0.23));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "MCSMWoolLand"
                if (segment.LayoutType == "MCSMWoolLand")
                {
                    if (choices[i].Text == "Lukas")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.23), (int)(choiceForm.Height * 0.38));
                    }
                    else if (choices[i].Text == "Fountain")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.36), (int)(choiceForm.Height * 0.22));
                    }
                    else if (choices[i].Text == "Reuben")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.59), (int)(choiceForm.Height * 0.305));
                    }
                    else if (choices[i].Text == "Lever")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.55), (int)(choiceForm.Height * 0.31));
                    }
                    else if (choices[i].Text == "Petra")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.766), (int)(choiceForm.Height * 0.57));
                    }
                    else if (choices[i].Text == "Gabriel")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.765), (int)(choiceForm.Height * 0.59));
                    }
                    else if (choices[i].Text == " Lever ")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.725), (int)(choiceForm.Height * 0.59));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "MCSMLabZone"
                if (segment.LayoutType == "MCSMLabZone")
                {
                    if (choices[i].Text == "Olivia")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.13), (int)(choiceForm.Height * 0.43));
                    }
                    else if (choices[i].Text == "Search Area 1")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.21), (int)(choiceForm.Height * 0.435));
                    }
                    else if (choices[i].Text == "Chest")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.335), (int)(choiceForm.Height * 0.37));
                    }
                    else if (choices[i].Text == "Search Area 2")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.57), (int)(choiceForm.Height * 0.44));
                    }
                    else if (choices[i].Text == "Search Upstairs")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.67), (int)(choiceForm.Height * 0.185));
                    }
                    else if (choices[i].Text == "Exit")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.865), (int)(choiceForm.Height * 0.51));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "MCSMGunZone"
                if (segment.LayoutType == "MCSMGunZone")
                {
                    if (choices[i].Text == "Olivia")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.30), (int)(choiceForm.Height * 0.18));
                    }
                    else if (choices[i].Text == "Button")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.435), (int)(choiceForm.Height * 0.255));
                    }
                    else if (choices[i].Text == "Lukas")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.60), (int)(choiceForm.Height * 0.15));
                    }
                    else if (choices[i].Text == "Chest")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.725), (int)(choiceForm.Height * 0.255));
                    }
                    else if (choices[i].Text == "Axel")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.79), (int)(choiceForm.Height * 0.49));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    int fixedOffset = 15;
                    int centerOffset = (buttonPanel.Width / 2) - (textLabel.Width / 2);
                    textLabel.Location = new System.Drawing.Point(centerOffset - fixedOffset, buttonHeight + 10);
                }

                // Custom positioning for "EpisodeEnd"
                if (segment.LayoutType == "EpisodeEnd")
                {
                    if (choices[i].Text == "Replay Episode")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.10), (int)(choiceForm.Height * 0.50));
                    }
                    else if (choices[i].Text == "Credits")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.75), (int)(choiceForm.Height * 0.50));
                    }

                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(26 * scaleFactor)),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    textLabel.Location = new System.Drawing.Point((buttonPanel.Width - textLabel.Width) / 2, buttonHeight + 10);
                }

                // Battle Kitty Episode 1 custom positioning
                // Custom positioning for Episode 1 Shore
                Console.WriteLine($"LayoutType: {segment.LayoutType}, VideoId: {videoId}");

                if (segment.LayoutType == "l0" && videoId == "80227815")
                {
                    if (choices[i].Text == "Zoom Back to Realm 1")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.075));
                    }
                    else if (choices[i].Text == "Orc Island")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.275), (int)(choiceForm.Height * 0.59));
                    }
                    else if (choices[i].Text == "First Monster")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.505), (int)(choiceForm.Height * 0.38));
                    }
                }

                // Custom positioning for Episode 1 Open Map
                if (segment.LayoutType == "l2" && videoId == "80227815")
                {
                    if (choices[i].Text == "[E2] Warrior Park")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.50));
                    }
                    else if (choices[i].Text == "Warrior Beach")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.305), (int)(choiceForm.Height * 0.70));
                    }
                    else if (choices[i].Text == "Guardian Gate 1")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.53), (int)(choiceForm.Height * 0.30));
                    }
                }

                // Custom positioning for Episode 1 Gate
                if (segment.LayoutType == "l1" && videoId == "80227815")
                {
                    if (choices[i].Text == "Zoom Back to Realm 1")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.075));
                    }
                    else if (choices[i].Text == "Statue Mystery")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.275), (int)(choiceForm.Height * 0.675));
                    }
                    else if (choices[i].Text == "To Next Realm")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.525), (int)(choiceForm.Height * 0.515));
                    }
                }

                // Battle Kitty Episode 2 custom positioning
                // Custom positioning for Episode 2 Open Map
                if (segment.LayoutType == "l4" && videoId == "81250260")
                {
                    if (choices[i].Text == "Warrior Park")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.50));
                    }
                    else if (choices[i].Text == "[E1] Warrior Beach")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.305), (int)(choiceForm.Height * 0.70));
                    }
                    else if (choices[i].Text == "Guardian Gate 1")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.53), (int)(choiceForm.Height * 0.30));
                    }
                }

                // Custom positioning for Episode 2 Gate
                if (segment.LayoutType == "l3" && videoId == "81250260")
                {
                    if (choices[i].Text == "Back to Realm 1")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.075));
                    }
                    else if (choices[i].Text == "Statue Mystery")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.275), (int)(choiceForm.Height * 0.675));
                    }
                    else if (choices[i].Text == "To Next Realm")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.525), (int)(choiceForm.Height * 0.515));
                    }
                }

                // Custom positioning for Episode 2 Map
                if (segment.LayoutType == "l0" && videoId == "81250260")
                {
                    if (choices[i].Text == "Back to Realm 1")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.075));
                    }
                    else if (choices[i].Text == "Racer Monster")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.275), (int)(choiceForm.Height * 0.575));
                    }
                    else if (choices[i].Text == "Submap 1 - Workout Zone")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.53), (int)(choiceForm.Height * 0.30));
                    }
                    else if (choices[i].Text == "Submap 2 - Power Plaza")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.76), (int)(choiceForm.Height * 0.50));
                    }
                }

                // Custom positioning for Episode 2 Workout Zone
                if (segment.LayoutType == "l1" && videoId == "81250260")
                {
                    if (choices[i].Text == "Back to Region Map")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.075));
                    }
                    else if (choices[i].Text == "Target Monster")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.275), (int)(choiceForm.Height * 0.38));
                    }
                    else if (choices[i].Text == "Kitty Walk")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.505), (int)(choiceForm.Height * 0.575));
                    }
                    else if (choices[i].Text == "Gym Day")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.735), (int)(choiceForm.Height * 0.38));
                    }
                }

                // Custom positioning for Episode 2 Power Plaza
                if (segment.LayoutType == "l2" && videoId == "81250260")
                {
                    if (choices[i].Text == "Back to Region Map")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.075), (int)(choiceForm.Height * 0.075));
                    }
                    else if (choices[i].Text == "Pool Emergency")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.275), (int)(choiceForm.Height * 0.575));
                    }
                    else if (choices[i].Text == "Boxing Monster")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.505), (int)(choiceForm.Height * 0.38));
                    }
                    else if (choices[i].Text == "Warrior Intros")
                    {
                        buttonPanel.Location = new System.Drawing.Point((int)(choiceForm.Width * 0.735), (int)(choiceForm.Height * 0.47));
                    }
                }

                buttonPanel.Controls.Add(button);

                if (buttonIcons[i] != null)
                {
                    int iconWidth = (int)(172 * scaleFactor);
                    int iconHeight = (int)(128 * scaleFactor);
                    var iconPictureBox = new PictureBox
                    {
                        Image = buttonIcons[i],
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Size = new Size(iconWidth, iconHeight),
                        Location = new System.Drawing.Point(0, (buttonHeight - iconHeight) / 2),
                        BackColor = Color.Transparent,
                        Enabled = false
                    };

                    button.Controls.Add(iconPictureBox);
                }

                // Add text label underneath the button
                if (new[] { "81054409", "81287545", "81019938", "81260654", "81054415", "81058723" }.Contains(videoId))
                {
                    var textLabel = new Label
                    {
                        Text = choices[i].Text,
                        AutoSize = true,
                        Font = new Font("Arial", (float)(22 * scaleFactor), FontStyle.Bold),
                        ForeColor = Color.White,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    buttonPanel.Controls.Add(textLabel);

                    textLabel.Location = new System.Drawing.Point((buttonPanel.Width - textLabel.Width) / 2, buttonHeight + 10);
                }

                buttons.Add(button);
                choiceForm.Controls.Add(buttonPanel);

                currentX += buttonWidth + spacing;
            }
        }        

        if (new[] { "80227815", "81250260", "81250261", "81250262", "81250263", "81250264", "81250265", "81250266", "81250267" }.Contains(videoId))
        {
            System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer { Interval = 10 };
            int elapsed = 0;
            int duration = 400; // Duration in milliseconds

            // Store the original sizes of the buttons
            List<Size> originalSizes = buttons.Select(button => button.Size).ToList();

            // Set initial size to very small
            foreach (var button in buttons)
            {
                button.Size = new Size(1, 1);
            }

            animationTimer.Tick += (sender, e) =>
            {
                elapsed += animationTimer.Interval;
                double progress = Math.Min(1.0, (double)elapsed / duration);
                double easedProgress = EaseOutElastic(progress);

                for (int i = 0; i < buttons.Count; i++)
                {
                    var button = buttons[i];
                    var originalSize = originalSizes[i];
                    int newWidth = (int)(originalSize.Width * easedProgress);
                    int newHeight = (int)(originalSize.Height * easedProgress);
                    button.Size = new Size(newWidth, newHeight);
                    button.Location = new System.Drawing.Point((button.Parent.Width - newWidth) / 2, (button.Parent.Height - newHeight) / 2);
                }

                if (progress >= 1.0)
                {
                    animationTimer.Stop();
                }
            };

            animationTimer.Start();
        }

        // Adjust the timer bar position to avoid overlapping with the buttons and labels
        int timerBarY;
        if (new[] { "80988062", "81131714" }.Contains(videoId))
        {
            timerBarY = 0;
        }
        else if (videoId == "81481556" && segment.LayoutType == "l2")
        {
            timerBarY = (int)(choiceForm.Height * 0.93);
        }
        else if (videoId == "81481556" && segment.LayoutType == "l1")
        {
            timerBarY = (int)(choiceForm.Height * 0.93);
        }
        else if (segment.LayoutType == "ReubenZone" || segment.LayoutType == "EnderconZone" || segment.LayoutType == "TempleZone" || segment.LayoutType == "MCSMTeamName" || segment.LayoutType == "Crafting" || segment.LayoutType == "EpisodeEnd" || segment.LayoutType == "RedstoniaZone" || segment.LayoutType == "MCSMThroneZone" || segment.LayoutType == "MCSMTownZone" || segment.LayoutType == "MCSMWoolLand" || segment.LayoutType == "MCSMLabZone" || segment.LayoutType == "MCSMGunZone" || segment.LayoutType == "IvorZone")
        {
            timerBarY = (int)(choiceForm.Height * 0.88);
        }
        else if (new[] { "80227815", "81250260" }.Contains(videoId))
        {
            timerBarY = (int)(choiceForm.Height * 0.92);
        }
        else if (new[] { "81054409", "81287545", "81019938", "81260654", "81054415", "81058723" }.Contains(videoId))
        {
            timerBarY = buttonTopMargin + buttonHeight + (int)(90 * scaleFactor);
        }
        else
        {
            timerBarY = buttonTopMargin + buttonHeight + (int)(40 * scaleFactor);
        }

        string FindTexturePath(string folder, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                var files = Directory.GetFiles(folder, name, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            return null; // Or handle the case where no file is found
        }

        // Check if a controller is connected
        var controller = new Controller(UserIndex.One);
        bool isControllerConnected = controller.IsConnected;

        if (videoId == "81271335" && segment.LayoutType == "l1")
        {
            string accessoryImagePath = FindTexturePath(movieFolder, new[] { "accessory_2x.png" });
            if (!string.IsNullOrEmpty(accessoryImagePath) && File.Exists(accessoryImagePath))
            {
                Bitmap accessorySpriteSheet = new Bitmap(accessoryImagePath);

                // Extract the top-right sprite (left arrow)
                int spriteWidth = accessorySpriteSheet.Width / 2;
                int spriteHeight = accessorySpriteSheet.Height / 2;
                Rectangle topRightRect = new Rectangle(spriteWidth, 0, spriteWidth, spriteHeight);
                Bitmap leftArrowSprite = accessorySpriteSheet.Clone(topRightRect, accessorySpriteSheet.PixelFormat);

                // Extract the bottom-left sprite (right arrow)
                Rectangle bottomLeftRect = new Rectangle(0, spriteHeight, spriteWidth, spriteHeight);
                Bitmap rightArrowSprite = accessorySpriteSheet.Clone(bottomLeftRect, accessorySpriteSheet.PixelFormat);

                // Extract the top-left sprite (correct arrow)
                Rectangle topLeftRect = new Rectangle(0, 0, spriteWidth, spriteHeight);
                Bitmap correctArrowSprite = accessorySpriteSheet.Clone(topLeftRect, accessorySpriteSheet.PixelFormat);

                // Extract the bottom-right sprite (incorrect arrow)
                Rectangle bottomRightRect = new Rectangle(spriteWidth, spriteHeight, spriteWidth, spriteHeight);
                Bitmap incorrectArrowSprite = accessorySpriteSheet.Clone(bottomRightRect, accessorySpriteSheet.PixelFormat);

                // Create PictureBox for the left arrow
                PictureBox leftArrowPictureBox = new PictureBox
                {
                    Image = leftArrowSprite,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size((int)(spriteWidth * scaleFactor), (int)(spriteHeight * scaleFactor)),
                    BackColor = Color.Transparent
                };

                // Create PictureBox for the right arrow
                PictureBox rightArrowPictureBox = new PictureBox
                {
                    Image = rightArrowSprite,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size((int)(spriteWidth * scaleFactor), (int)(spriteHeight * scaleFactor)),
                    BackColor = Color.Transparent
                };

                // Position the arrows in the middle of the choice window
                int arrowOffset = (int)(50 * scaleFactor);
                int verticalAdjustment = (int)(15 * scaleFactor);
                leftArrowPictureBox.Location = new Point(
                    (choiceForm.Width / 2) - leftArrowPictureBox.Width - arrowOffset,
                    (choiceForm.Height / 2) - (leftArrowPictureBox.Height / 2) - verticalAdjustment
                );
                rightArrowPictureBox.Location = new Point(
                    (choiceForm.Width / 2) + arrowOffset,
                    (choiceForm.Height / 2) - (rightArrowPictureBox.Height / 2) - verticalAdjustment
                );

                // Add the arrows to the choice form
                choiceForm.Controls.Add(leftArrowPictureBox);
                choiceForm.Controls.Add(rightArrowPictureBox);

                // Track the number of choices made
                int choicesMade = 0;

                // Handle choice selection
                foreach (var button in buttons)
                {
                    button.Click += (sender, e) =>
                    {
                        // Determine if the selected choice is correct
                        bool isCorrect = false;
                        if (segment.AnswerSequence != null && segment.AnswerSequence.Count > 0)
                        {
                            int choiceSetIndex = buttons.IndexOf(button) / segment.ChoiceSets[0].Count;
                            int correctIndex = segment.AnswerSequence.ElementAtOrDefault(choiceSetIndex);
                            isCorrect = buttons.IndexOf(button) % segment.ChoiceSets[0].Count == correctIndex;
                        }

                        // Determine if the clicked button corresponds to the left or right arrow
                        int buttonIndex = buttons.IndexOf(button);
                        bool isLeftArrow = buttonIndex % 2 == 0; // Even indices correspond to the left arrow

                        // Update the arrows based on the result
                        if (isCorrect)
                        {
                            if (isLeftArrow)
                            {
                                leftArrowPictureBox.Image = correctArrowSprite;
                                rightArrowPictureBox.Visible = false; // Hide the other arrow
                            }
                            else
                            {
                                rightArrowPictureBox.Image = correctArrowSprite;
                                leftArrowPictureBox.Visible = false; // Hide the other arrow
                            }
                        }
                        else
                        {
                            if (isLeftArrow)
                            {
                                leftArrowPictureBox.Image = incorrectArrowSprite;
                                rightArrowPictureBox.Visible = false; // Hide the other arrow
                            }
                            else
                            {
                                rightArrowPictureBox.Image = incorrectArrowSprite;
                                leftArrowPictureBox.Visible = false; // Hide the other arrow
                            }
                        }

                        // Increment the number of choices made
                        choicesMade++;

                        // Reset the arrows after 1 second, or hide them after the third choice
                        Task.Delay(1000).ContinueWith(_ =>
                        {
                            choiceForm.Invoke(new Action(() =>
                            {
                                if (choicesMade < 3)
                                {
                                    // Reset to the original two arrows
                                    leftArrowPictureBox.Image = leftArrowSprite;
                                    rightArrowPictureBox.Image = rightArrowSprite;
                                    leftArrowPictureBox.Visible = true;
                                    rightArrowPictureBox.Visible = true;
                                }
                                else
                                {
                                    // Hide the arrows after the third choice
                                    leftArrowPictureBox.Visible = false;
                                    rightArrowPictureBox.Visible = false;
                                }
                            }));
                        });
                    };
                }
            }
        }

        // Define possible names for each texture
        string timerFillPath, timerCapLPath, timerCapRPath, timerBottomPath, timerTopPath, webPath;

        if (videoId == "10000001")
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "timer.png" });
            timerCapLPath = null;
            timerCapRPath = null;
            timerBottomPath = null;
            timerTopPath = null;
            webPath = isControllerConnected ? FindTexturePath(movieFolder, new[] { "controller_2x.png" }) : FindTexturePath(movieFolder, new[] { "web_2x.png", "device_web_2x.png", "web_2x_v2.png", "web_3x.png", "web_icon_2x.png" });
        }
        else if (videoId == "81131714" && segment.LayoutType == "l6")
        {
            timerFillPath = null;
            timerCapLPath = null;
            timerCapRPath = null;
            timerBottomPath = null;
            timerTopPath = null;
            webPath = null;
        }
        else if (videoId == "81271335" && segment.LayoutType == "l1")
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "timer_sprite_2x_v2.png" });
            timerCapLPath = null;
            timerCapRPath = null;
            timerBottomPath = null;
            timerTopPath = null;
            webPath = null;
        }
        else if (videoId == "81271335" && segment.LayoutType == "l0")
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "timer_sprite_reengagement_2x.png" });
            timerCapLPath = null;
            timerCapRPath = null;
            timerBottomPath = null;
            timerTopPath = null;
            webPath = null;
        }
        else if (videoId == "10000003" && segment.LayoutType == "l1")
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "black_timer_fill_2x.png" });
            timerCapLPath = FindTexturePath(movieFolder, new[] { "black_timer_capL_2x.png" });
            timerCapRPath = FindTexturePath(movieFolder, new[] { "black_timer_capR_2x.png" });
            timerBottomPath = FindTexturePath(movieFolder, new[] { "black_timer_bottom_2x.png" });
            timerTopPath = FindTexturePath(movieFolder, new[] { "black_timer_top_2x.png" });
            webPath = FindTexturePath(movieFolder, new[] { "black_web_2x.png" });
        }
        else if (videoId == "81328829" && segment.LayoutType == "l0")
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "timer_neutral_fill_2x.png" });
            timerCapLPath = FindTexturePath(movieFolder, new[] { "timer_neutral_capL_2x.png" });
            timerCapRPath = FindTexturePath(movieFolder, new[] { "timer_neutral_capR_2x.png" });
            timerBottomPath = FindTexturePath(movieFolder, new[] { "timer_neutral_bottom_2x.png" });
            timerTopPath = null;
            webPath = null;
        }
        else if (videoId == "81328829" && segment.LayoutType == "l1")
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "timer_relax_fill_2x.png" });
            timerCapLPath = FindTexturePath(movieFolder, new[] { "timer_relax_capL_2x.png" });
            timerCapRPath = FindTexturePath(movieFolder, new[] { "timer_relax_capR_2x.png" });
            timerBottomPath = FindTexturePath(movieFolder, new[] { "timer_relax_bottom_2x.png" });
            timerTopPath = null;
            webPath = null;
        }
        else if (videoId == "81328829" && segment.LayoutType == "l2")
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "timer_sleep_fill_2x.png" });
            timerCapLPath = FindTexturePath(movieFolder, new[] { "timer_sleep_capL_2x.png" });
            timerCapRPath = FindTexturePath(movieFolder, new[] { "timer_sleep_capR_2x.png" });
            timerBottomPath = FindTexturePath(movieFolder, new[] { "timer_sleep_bottom_2x.png" });
            timerTopPath = null;
            webPath = null;
        }
        else
        {
            timerFillPath = FindTexturePath(movieFolder, new[] { "timer_fill_2x.png", "timer_fill_2x_v2.png", "timer_fill_3x.png" });
            timerCapLPath = FindTexturePath(movieFolder, new[] { "timer_capL_2x.png", "timer_capL_2x_v2.png", "timer_capL_3x.png" });
            timerCapRPath = FindTexturePath(movieFolder, new[] { "timer_capR_2x.png", "timer_capR_2x_v2.png", "timer_capR_3x.png" });
            timerBottomPath = FindTexturePath(movieFolder, new[] { "timer_bottom_2x.png", "timer_bottom_2x_v2.png", "timer_bottom_3x.png", "bottombar_2x.png" });
            timerTopPath = FindTexturePath(movieFolder, new[] { "timer_top_2x.png", "timer_top_2x_v2.png", "timer_top_3x.png" });
            webPath = isControllerConnected ? FindTexturePath(movieFolder, new[] { "controller_2x.png" }) : FindTexturePath(movieFolder, new[] { "web_2x.png", "device_web_2x.png", "web_2x_v2.png", "web_3x.png", "web_icon_2x.png" });
        }

        // Handle cases where a texture wasn't found
        if (webPath == null)
        {
            webPath = FindTexturePath(movieFolder, new[] { "web_2x.png", "web_2x_v2.png", "web_3x.png", "web_icon_2x.png" });
        }

        if (timerFillPath == null)
        {
            Console.WriteLine("Timer texture not found.");
        }

        Bitmap timerFillSprite = LoadBitmap(timerFillPath);
        Bitmap timerCapLSprite = LoadBitmap(timerCapLPath);
        Bitmap timerCapRSprite = LoadBitmap(timerCapRPath);
        Bitmap timerBottomSprite = LoadBitmap(timerBottomPath);
        Bitmap timerTopSprite = LoadBitmap(timerTopPath);
        Bitmap webSprite = LoadBitmap(webPath);

        int initialWidth = (int)(1700 * scaleFactor);
        int timerBarHeight = (int)((timerFillSprite?.Height ?? 20) * scaleFactor);
        int formCenterX = choiceForm.Width / 2;

        // Create a DoubleBufferedPanel
        DoubleBufferedPanel drawingPanel = new DoubleBufferedPanel
        {
            Location = new System.Drawing.Point(0, 0),
            Size = new Size(choiceForm.Width, choiceForm.Height),
            BackColor = Color.Transparent
        };

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        drawingPanel.Paint += (sender, e) =>
        {
            Graphics g = e.Graphics;

            int currentY = timerBarY;

            if (videoId == "10000001")
            {
                // Calculate the eased Y position
                double progress = Math.Min(1.0, (double)stopwatch.ElapsedMilliseconds / 370);
                double easedProgress = EaseOutQuad(progress);
                currentY = (int)(timerBarY + (choiceForm.Height - timerBarY) * (1 - easedProgress));
            }
            else if (new[] { "80227815", "81250260", "81250261", "81250262", "81250263", "81250264", "81250265", "81250266", "81250267", "81175265" }.Contains(videoId))
            {
                // Calculate the eased Y position
                double progress = Math.Min(1.0, (double)stopwatch.ElapsedMilliseconds / 400);
                double easedProgress = EaseOutQuad(progress);
                currentY = (int)(timerBarY + (choiceForm.Height - timerBarY) * (1 - easedProgress));
            }

            if (videoId == "10000001")
            {
                if (timerFillSprite != null)
                {
                    int frameHeight = timerFillSprite.Height / 22;
                    int currentFrame = (int)((double)stopwatch.ElapsedMilliseconds / timeLimitMs * 22);
                    currentFrame = Math.Min(currentFrame, 21);

                    Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, timerFillSprite.Width, frameHeight);
                    Rectangle destRect = new Rectangle((choiceForm.Width - (int)(timerFillSprite.Width * scaleFactor)) / 2, currentY, (int)(timerFillSprite.Width * scaleFactor), (int)(frameHeight * scaleFactor));

                    g.DrawImage(timerFillSprite, destRect, sourceRect, GraphicsUnit.Pixel);

                    // Draw overlay
                    if (webSprite != null)
                    {
                        int webY = currentY + ((int)(frameHeight * scaleFactor) / 2) - (int)(webSprite.Height * scaleFactor / 2);
                        g.DrawImage(webSprite, new Rectangle((choiceForm.Width - (int)(webSprite.Width * scaleFactor)) / 2, webY, (int)(webSprite.Width * scaleFactor), (int)(webSprite.Height * scaleFactor)));
                    }
                }
            }
            else if (videoId == "81271335" && segment.LayoutType == "l1")
            {
                if (timerFillSprite != null)
                {
                    int totalRows = 20; // Total rows in the sprite
                    int usedRows = 19;  // Rows used for countdown
                    int frameHeight = timerFillSprite.Height / totalRows;

                    // Calculate the current frame based on elapsed time
                    int currentFrame = (int)((double)stopwatch.ElapsedMilliseconds / timeLimitMs * usedRows);
                    currentFrame = Math.Min(currentFrame, usedRows - 1);

                    // Determine the 19th frame based on correctAnswersCount
                    if (currentFrame == 18) // 19th frame
                    {
                        currentFrame = correctAnswersCount == 3 ? 18 : 19;
                    }

                    if ((currentFrame == 18 || currentFrame == 19) && !soundPlayed)
                    {
                        string soundPath = null;

                        if (currentFrame == 18 && correctAnswersCount == 3)
                        {
                            // Play the success sound if all answers are correct
                            soundPath = FindTexturePath(movieFolder, new[] { "sfx_timer_end_pass.m4a" });
                        }
                        else if (currentFrame == 19 && correctAnswersCount != 3)
                        {
                            // Play the fail sound if not all answers are correct
                            soundPath = FindTexturePath(movieFolder, new[] { "sfx_timer_end_fail.m4a" });
                        }

                        if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                        {
                            var soundPlayer = new MediaPlayer(new Media(libVLC, soundPath, FromType.FromPath));
                            soundPlayer.Play();
                        }

                        // Mark the sound as played
                        soundPlayed = true;
                    }

                    // Define the source rectangle for the current frame
                    Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, timerFillSprite.Width, frameHeight);

                    // Define the destination rectangle for rendering
                    Rectangle destRect = new Rectangle(
                        (choiceForm.Width - (int)(timerFillSprite.Width * scaleFactor)) / 2,
                        timerBarY,
                        (int)(timerFillSprite.Width * scaleFactor),
                        (int)(frameHeight * scaleFactor)
                    );

                    // Draw the current frame
                    g.DrawImage(timerFillSprite, destRect, sourceRect, GraphicsUnit.Pixel);
                }
            }
            else if (videoId == "81271335" && segment.LayoutType == "l0")
            {
                if (timerFillSprite != null)
                {
                    int totalRows = 18; // Total rows in the sprite
                    int usedRows = 18;  // Rows used for countdown
                    int frameHeight = timerFillSprite.Height / totalRows;

                    // Calculate the current frame based on elapsed time
                    int currentFrame = (int)((double)stopwatch.ElapsedMilliseconds / timeLimitMs * usedRows);
                    currentFrame = Math.Min(currentFrame, usedRows - 1);

                    // Define the source rectangle for the current frame
                    Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, timerFillSprite.Width, frameHeight);

                    // Define the destination rectangle for rendering
                    Rectangle destRect = new Rectangle(
                        (choiceForm.Width - (int)(timerFillSprite.Width * scaleFactor)) / 2,
                        timerBarY,
                        (int)(timerFillSprite.Width * scaleFactor),
                        (int)(frameHeight * scaleFactor)
                    );

                    // Draw the current frame
                    g.DrawImage(timerFillSprite, destRect, sourceRect, GraphicsUnit.Pixel);
                }
            }
            else
            {
                // Draw timer bottom
                if (timerBottomSprite != null)
                {
                    g.DrawImage(timerBottomSprite, new Rectangle((choiceForm.Width - (int)(1800 * scaleFactor)) / 2, currentY, (int)(1800 * scaleFactor), (int)(50 * scaleFactor)));
                }

                // Draw timer fill
                if (timerFillSprite != null)
                {
                    // All this crap is due to System.Drawings applying a fade effect
                    int leftEdgeWidth = (int)(10 * scaleFactor);
                    int rightEdgeWidth = (int)(10 * scaleFactor);
                    int middleWidth = Math.Max(0, initialWidth - leftEdgeWidth - rightEdgeWidth);
                    int totalWidth = leftEdgeWidth + middleWidth + rightEdgeWidth;
                    int destX = (choiceForm.Width - totalWidth) / 2;
                    int destY = currentY;
                    int destHeight = timerBarHeight;

                    // Draw left edge
                    Rectangle leftSourceRect = new Rectangle(0, 0, leftEdgeWidth, timerFillSprite.Height);
                    Rectangle leftDestRect = new Rectangle(destX, destY, leftEdgeWidth, destHeight);
                    g.DrawImage(timerFillSprite, leftDestRect, leftSourceRect, GraphicsUnit.Pixel);

                    // Draw middle stretched portion
                    Rectangle middleSourceRect = new Rectangle(leftEdgeWidth, 0, timerFillSprite.Width - leftEdgeWidth - rightEdgeWidth, timerFillSprite.Height);
                    Rectangle middleDestRect = new Rectangle(destX + leftEdgeWidth, destY, middleWidth, destHeight);
                    g.DrawImage(timerFillSprite, middleDestRect, middleSourceRect, GraphicsUnit.Pixel);

                    // Draw right edge
                    Rectangle rightSourceRect = new Rectangle(timerFillSprite.Width - rightEdgeWidth, 0, rightEdgeWidth, timerFillSprite.Height);
                    Rectangle rightDestRect = new Rectangle(destX + leftEdgeWidth + middleWidth, destY, rightEdgeWidth, destHeight);
                    g.DrawImage(timerFillSprite, rightDestRect, rightSourceRect, GraphicsUnit.Pixel);
                }

                // Draw left cap
                if (timerCapLSprite != null)
                {
                    g.DrawImage(timerCapLSprite, new Rectangle((choiceForm.Width - initialWidth) / 2 - (int)(timerCapLSprite.Width * scaleFactor), currentY, (int)(timerCapLSprite.Width * scaleFactor), timerBarHeight));
                }

                // Draw right cap
                if (timerCapRSprite != null)
                {
                    g.DrawImage(timerCapRSprite, new Rectangle((choiceForm.Width + initialWidth) / 2, currentY, (int)(timerCapRSprite.Width * scaleFactor), timerBarHeight));
                }

                // Draw timer top
                if (timerTopSprite != null)
                {
                    g.DrawImage(timerTopSprite, new Rectangle((choiceForm.Width - (int)(1800 * scaleFactor)) / 2, currentY, (int)(1800 * scaleFactor), (int)(50 * scaleFactor)));
                }

                // Draw overlay
                if (webSprite != null)
                {
                    int webY = currentY + (timerBarHeight / 2) - (int)(webSprite.Height * scaleFactor / 2);
                    g.DrawImage(webSprite, new Rectangle((choiceForm.Width - (int)(webSprite.Width * scaleFactor)) / 2, webY, (int)(webSprite.Width * scaleFactor), (int)(webSprite.Height * scaleFactor)));
                }
            }
        };

        choiceForm.Controls.Add(drawingPanel);

        Task.Run(async () =>
        {
            while (stopwatch.ElapsedMilliseconds < timeLimitMs)
            {
                initialWidth = (int)((double)(1650 * scaleFactor) * (timeLimitMs - stopwatch.ElapsedMilliseconds) / timeLimitMs);
                drawingPanel.Invalidate();
                await Task.Delay(11); // Update approximately every 16ms (~60 FPS)
            }

            if (!inputCaptured && File.Exists(timeoutSoundPath))
            {
                var timeoutPlayer = new MediaPlayer(new Media(libVLC, timeoutSoundPath, FromType.FromPath));
                timeoutPlayer.Play();
            }

            choiceForm.Invoke(new Action(() => choiceForm.Close()));
        });

        if (new[] { "10000001", "80227815", "81250260", "81250261", "81250262", "81250263", "81250264", "81250265", "81250266", "81250267", "80227815", "81175265" }.Contains(videoId))
        {
            Task.Run(async () =>
            {
                int duration = videoId == "10000001" ? 370 : 400;
                while (stopwatch.ElapsedMilliseconds < duration)
                {
                    drawingPanel.Invalidate();
                    await Task.Delay(11); // Update approximately every 16ms (~60 FPS)
                }
            });
        }

        if (videoId == "81271335" && segment.LayoutType == "l1" && isControllerConnected)
        {
            int staggerStage = 0; // 0: first pair, 1: second pair, 2: third pair
            bool staggerReady = true;
            Stopwatch staggerTimer = new Stopwatch();

            Task.Run(async () =>
            {
                while (stopwatch.ElapsedMilliseconds < timeLimitMs)
                {
                    drawingPanel.Invalidate();

                    if (!controller.IsConnected) break;
                    var state = controller.GetState();
                    var gamepad = state.Gamepad;

                    // Only allow input if ready
                    if (staggerReady)
                    {
                        int baseIndex = staggerStage * 2;
                        if (baseIndex + 1 < buttons.Count)
                        {
                            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft) || gamepad.LeftThumbX < -5000)
                            {
                                // Simulate click on left button of current pair
                                buttons[baseIndex].PerformClick();
                                staggerReady = false;
                                staggerTimer.Restart();
                            }
                            else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) || gamepad.LeftThumbX > 5000)
                            {
                                // Simulate click on right button of current pair
                                buttons[baseIndex + 1].PerformClick();
                                staggerReady = false;
                                staggerTimer.Restart();
                            }
                        }
                    }
                    else
                    {
                        // Wait for 1 second before next stage
                        if (staggerTimer.ElapsedMilliseconds >= 1000)
                        {
                            staggerStage++;
                            staggerReady = true;
                            staggerTimer.Reset();
                        }
                    }

                    // End after third stage
                    if (staggerStage > 2) break;

                    await Task.Delay(16);
                }
            });
        }
        else
        {
            Task.Run(async () =>
            {
                int selectedIndex = (segment.DefaultChoiceIndex.HasValue && segment.DefaultChoiceIndex.Value >= 0 && segment.DefaultChoiceIndex.Value < choices.Count) ? segment.DefaultChoiceIndex.Value : 0; // Initialize selected index for controller input

                while (stopwatch.ElapsedMilliseconds < timeLimitMs)
                {
                    initialWidth = (int)((double)(1650 * scaleFactor) * (timeLimitMs - stopwatch.ElapsedMilliseconds) / timeLimitMs);
                    drawingPanel.Invalidate();

                    // Handle controller input
                    HandleControllerInput(ref selectedIndex, buttons, buttonSprites, ref inputCaptured, ref selectedSegmentId, choiceForm, selectSoundPath, hoverSoundPath, libVLC, videoId, choices, segment, movieFolder);

                    await Task.Delay(16); // Update approximately every 16ms (~60 FPS)
                }

                if (!inputCaptured && File.Exists(timeoutSoundPath))
                {
                    var timeoutPlayer = new MediaPlayer(new Media(libVLC, timeoutSoundPath, FromType.FromPath));
                    timeoutPlayer.Play();
                }

                choiceForm.Invoke(new Action(() => choiceForm.Close()));
            });
        }

        if (new[] { "81131714", "81481556", "81004016", "80988062", "81271335", "10000003" }.Contains(videoId))
        {
            int targetY = choiceForm.Location.Y;

            if (settings.DisableWindowAnimations)
            {
                choiceForm.Location = new System.Drawing.Point(choiceForm.Location.X, targetY);
            }
            else
            {
                if (videoId == "81271335" && segment.LayoutType == "l1")
                {
                    choiceForm.Location = new System.Drawing.Point(choiceForm.Location.X, targetY);

                    var originalLocation = choiceForm.Location;
                    choiceForm.Location = new Point(-10000, -10000);
                    choiceForm.Show();
                    choiceForm.Refresh();
                    Application.DoEvents();

                    Bitmap formBitmap = new Bitmap(choiceForm.Width, choiceForm.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(formBitmap))
                    {
                        IntPtr hdc = g.GetHdc();
                        PrintWindow(choiceForm.Handle, hdc, 0);
                        g.ReleaseHdc(hdc);
                    }

                    choiceForm.Hide();
                    choiceForm.Location = originalLocation;

                    choiceForm.Opacity = 1;
                    choiceForm.Visible = false;

                    int duration = 250;
                    int interval = 15;
                    int elapsed = 0;
                    int maxRadius = (int)Math.Ceiling(Math.Sqrt(choiceForm.Width * choiceForm.Width + choiceForm.Height * choiceForm.Height) / 2);

                    System.Windows.Forms.Timer rippleTimer = new System.Windows.Forms.Timer { Interval = interval };
                    rippleTimer.Tick += (s, e) =>
                    {
                        elapsed += interval;
                        double progress = Math.Min(1.0, (double)elapsed / duration);
                        int radius = (int)(maxRadius * EaseOutQuad(progress));

                        Bitmap masked = new Bitmap(choiceForm.Width, choiceForm.Height, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(masked))
                        {
                            g.Clear(Color.Transparent);
                        }

                        int feather = Math.Max(24, radius / 6);

                        BitmapData data = masked.LockBits(
                            new Rectangle(0, 0, masked.Width, masked.Height),
                            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                        BitmapData srcData = formBitmap.LockBits(
                            new Rectangle(0, 0, formBitmap.Width, formBitmap.Height),
                            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                        int w = masked.Width;
                        int h = masked.Height;
                        int cx = w / 2;
                        int cy = h / 2;

                        int bytes = Math.Abs(data.Stride) * h;
                        byte[] dst = new byte[bytes];
                        byte[] src = new byte[bytes];

                        System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, src, 0, bytes);

                        for (int y = 0; y < h; y++)
                        {
                            for (int x = 0; x < w; x++)
                            {
                                int dx = x - cx;
                                int dy = y - cy;
                                double dist = Math.Sqrt(dx * dx + dy * dy);

                                double alpha;
                                if (dist < radius - feather)
                                    alpha = 1.0;
                                else if (dist > radius)
                                    alpha = 0.0;
                                else
                                    alpha = 1.0 - (dist - (radius - feather)) / feather;

                                int idx = (y * data.Stride) + (x * 4);

                                byte b = src[idx + 0];
                                byte g = src[idx + 1];
                                byte r = src[idx + 2];
                                byte a = src[idx + 3];

                                dst[idx + 0] = b;
                                dst[idx + 1] = g;
                                dst[idx + 2] = r;
                                dst[idx + 3] = (byte)(a * alpha);
                            }
                        }

                        System.Runtime.InteropServices.Marshal.Copy(dst, 0, data.Scan0, bytes);


                        masked.UnlockBits(data);
                        formBitmap.UnlockBits(srcData);

                        IntPtr screenDC = NativeMethods.GetDC(IntPtr.Zero);
                        IntPtr memDC = NativeMethods.CreateCompatibleDC(screenDC);
                        IntPtr hBitmap = masked.GetHbitmap(Color.FromArgb(0));
                        IntPtr oldBitmap = NativeMethods.SelectObject(memDC, hBitmap);

                        NativeMethods.SIZE size = new NativeMethods.SIZE { cx = masked.Width, cy = masked.Height };
                        NativeMethods.POINT pointSource = new NativeMethods.POINT { x = 0, y = 0 };
                        NativeMethods.POINT topPos = new NativeMethods.POINT { x = choiceForm.Left, y = choiceForm.Top };
                        NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION
                        {
                            BlendOp = 0,
                            BlendFlags = 0,
                            SourceConstantAlpha = 255,
                            AlphaFormat = 1
                        };

                        NativeMethods.UpdateLayeredWindow(choiceForm.Handle, screenDC, ref topPos, ref size, memDC, ref pointSource, 0, ref blend, 2);

                        NativeMethods.SelectObject(memDC, oldBitmap);
                        NativeMethods.DeleteObject(hBitmap);
                        NativeMethods.DeleteDC(memDC);
                        NativeMethods.ReleaseDC(IntPtr.Zero, screenDC);

                        if (!choiceForm.Visible)
                            choiceForm.Visible = true;

                        if (progress >= 1.0)
                        {
                            rippleTimer.Stop();
                            choiceForm.BeginInvoke(new Action(() =>
                            {
                                // Remove layered style so the window becomes interactive
                                int exStyle = GetWindowLong(choiceForm.Handle, GWL_EXSTYLE);
                                SetWindowLong(choiceForm.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);

                                // Do NOT set Visible or Opacity here!
                                // Instead, force a redraw to update the window in-place
                                choiceForm.Invalidate();
                                choiceForm.Update();
                            }));
                        }
                    };

                    // Set layered style
                    int exStyle2 = GetWindowLong(choiceForm.Handle, GWL_EXSTYLE);
                    SetWindowLong(choiceForm.Handle, GWL_EXSTYLE, exStyle2 | WS_EX_LAYERED);

                    rippleTimer.Start();

                    choiceForm.FormClosing += (s, e) =>
                    {
                        // Prevent recursive closing
                        if ((choiceForm.Tag as string) == "Closing") return;

                        e.Cancel = true;
                        int durationOut = 250;
                        int intervalOut = 15;
                        int elapsedOut = 0;
                        int maxRadiusOut = (int)Math.Ceiling(Math.Sqrt(choiceForm.Width * choiceForm.Width + choiceForm.Height * choiceForm.Height) / 2);

                        Bitmap formBitmapOut = new Bitmap(choiceForm.Width, choiceForm.Height, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(formBitmapOut))
                        {
                            IntPtr hdc = g.GetHdc();
                            PrintWindow(choiceForm.Handle, hdc, 0);
                            g.ReleaseHdc(hdc);
                        }

                        System.Windows.Forms.Timer rippleOutTimer = new System.Windows.Forms.Timer { Interval = intervalOut };
                        rippleOutTimer.Tick += (sender2, e2) =>
                        {
                            if (choiceForm.IsDisposed || !choiceForm.IsHandleCreated)
                            {
                                rippleOutTimer.Stop();
                                rippleOutTimer.Dispose();
                                return;
                            }

                            elapsedOut += intervalOut;
                            double progress = Math.Min(1.0, (double)elapsedOut / durationOut);
                            int radius = (int)(maxRadiusOut * (1.0 - EaseOutQuad(progress)));

                            using (Bitmap masked = new Bitmap(choiceForm.Width, choiceForm.Height, PixelFormat.Format32bppArgb))
                            using (Graphics g = Graphics.FromImage(masked))
                            {
                                g.Clear(Color.Transparent);

                                int feather = Math.Max(24, radius / 6);

                                BitmapData data = masked.LockBits(
                                    new Rectangle(0, 0, masked.Width, masked.Height),
                                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                                BitmapData srcData = formBitmapOut.LockBits(
                                    new Rectangle(0, 0, formBitmapOut.Width, formBitmapOut.Height),
                                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                                int w = masked.Width;
                                int h = masked.Height;
                                int cx = w / 2;
                                int cy = h / 2;

                                int bytes = Math.Abs(data.Stride) * h;
                                byte[] dst = new byte[bytes];
                                byte[] src = new byte[bytes];

                                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, src, 0, bytes);

                                for (int y = 0; y < h; y++)
                                {
                                    for (int x = 0; x < w; x++)
                                    {
                                        int dx = x - cx;
                                        int dy = y - cy;
                                        double dist = Math.Sqrt(dx * dx + dy * dy);

                                        double alpha;
                                        if (dist < radius - feather)
                                            alpha = 1.0;
                                        else if (dist > radius)
                                            alpha = 0.0;
                                        else
                                            alpha = 1.0 - (dist - (radius - feather)) / feather;

                                        int idx = (y * data.Stride) + (x * 4);

                                        byte b = src[idx + 0];
                                        byte g2 = src[idx + 1];
                                        byte r = src[idx + 2];
                                        byte a = src[idx + 3];

                                        dst[idx + 0] = b;
                                        dst[idx + 1] = g2;
                                        dst[idx + 2] = r;
                                        dst[idx + 3] = (byte)(a * alpha);
                                    }
                                }

                                System.Runtime.InteropServices.Marshal.Copy(dst, 0, data.Scan0, bytes);

                                masked.UnlockBits(data);
                                formBitmapOut.UnlockBits(srcData);

                                IntPtr screenDC = NativeMethods.GetDC(IntPtr.Zero);
                                IntPtr memDC = NativeMethods.CreateCompatibleDC(screenDC);
                                IntPtr hBitmap = masked.GetHbitmap(Color.FromArgb(0));
                                IntPtr oldBitmap = NativeMethods.SelectObject(memDC, hBitmap);

                                NativeMethods.SIZE size = new NativeMethods.SIZE { cx = masked.Width, cy = masked.Height };
                                NativeMethods.POINT pointSource = new NativeMethods.POINT { x = 0, y = 0 };
                                NativeMethods.POINT topPos = new NativeMethods.POINT { x = choiceForm.Left, y = choiceForm.Top };
                                NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION
                                {
                                    BlendOp = 0,
                                    BlendFlags = 0,
                                    SourceConstantAlpha = 255,
                                    AlphaFormat = 1
                                };

                                NativeMethods.UpdateLayeredWindow(choiceForm.Handle, screenDC, ref topPos, ref size, memDC, ref pointSource, 0, ref blend, 2);

                                NativeMethods.SelectObject(memDC, oldBitmap);
                                NativeMethods.DeleteObject(hBitmap);
                                NativeMethods.DeleteDC(memDC);
                                NativeMethods.ReleaseDC(IntPtr.Zero, screenDC);

                                if (progress >= 1.0)
                                {
                                    rippleOutTimer.Stop();
                                    rippleOutTimer.Dispose();

                                    int exStyle = GetWindowLong(choiceForm.Handle, GWL_EXSTYLE);
                                    SetWindowLong(choiceForm.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);

                                    choiceForm.Tag = "Closing";
                                    choiceForm.BeginInvoke(new Action(() => choiceForm.Close()));
                                }
                            }
                        };

                        // Set layered style if not already set
                        int exStyle3 = GetWindowLong(choiceForm.Handle, GWL_EXSTYLE);
                        SetWindowLong(choiceForm.Handle, GWL_EXSTYLE, exStyle3 | WS_EX_LAYERED);

                        rippleOutTimer.Start();
                    };
                }
                else
                {
                    choiceForm.Location = new System.Drawing.Point(choiceForm.Location.X, targetY + 750);

                    System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer { Interval = 10 };
                    int elapsed = 0;
                    int duration = 750; // Duration in milliseconds

                    animationTimer.Tick += (sender, e) =>
                    {
                        elapsed += animationTimer.Interval;
                        double progress = Math.Min(1.0, (double)elapsed / duration);
                        double easedProgress = EaseOutQuad(progress);

                        int newY = (int)(targetY + 750 * (1 - easedProgress));
                        choiceForm.Location = new System.Drawing.Point(choiceForm.Location.X, newY);

                        if (progress >= 1.0)
                        {
                            animationTimer.Stop();
                        }
                    };

                    animationTimer.Start();

                    choiceForm.FormClosing += (sender, e) =>
                    {
                        // Only animate if not already at the bottom
                        if (choiceForm.Location.Y == targetY)
                        {
                            e.Cancel = true; // Cancel the close, we'll close after animation
                            int closeElapsed = 0;
                            int closeDuration = 750;
                            int startY = choiceForm.Location.Y;
                            int endY = targetY + 750;

                            System.Windows.Forms.Timer closeTimer = new System.Windows.Forms.Timer { Interval = 10 };
                            closeTimer.Tick += (s, args) =>
                            {
                                closeElapsed += closeTimer.Interval;
                                double closeProgress = Math.Min(1.0, (double)closeElapsed / closeDuration);
                                double closeEased = EaseInQuad(closeProgress);

                                int newCloseY = (int)(startY + (endY - startY) * closeEased);
                                choiceForm.Location = new System.Drawing.Point(choiceForm.Location.X, newCloseY);

                                if (closeProgress >= 1.0)
                                {
                                    closeTimer.Stop();
                                    choiceForm.FormClosing -= null; // Remove handler to avoid recursion
                                    choiceForm.Close(); // Now close for real
                                }
                            };

                            closeTimer.Start();
                        }
                    };
                }
            }
        }

        System.Windows.Forms.Timer visibilityTimer = new System.Windows.Forms.Timer { Interval = 15 };
        visibilityTimer.Tick += (sender, e) =>
        {
            choiceForm.Opacity = 1; // Make the form visible
            visibilityTimer.Stop(); // Stop the timer
        };
        visibilityTimer.Start();

        Task.Run(async () =>
        {
            while (stopwatch.ElapsedMilliseconds < timeLimitMs)
            {
                await Task.Delay(16);
            }

            if (!inputCaptured)
            {
                // If a timeout segment is specified, use it
                if (segment.TimeoutSegment != null && !string.IsNullOrEmpty(segment.TimeoutSegment.SegmentId))
                {
                    selectedSegmentId = segment.TimeoutSegment.SegmentId;
                    Console.WriteLine($"No choice made. Using timeout segment: {selectedSegmentId}");
                }
                // Otherwise, use the default choice if available
                else if (segment.DefaultChoiceIndex.HasValue && segment.DefaultChoiceIndex.Value >= 0 && segment.DefaultChoiceIndex.Value < choices.Count)
                {
                    selectedSegmentId = choices[segment.DefaultChoiceIndex.Value].SegmentId;
                    Console.WriteLine($"No choice made. Defaulting to the specified choice: {selectedSegmentId}");
                }
                else
                {
                    Console.WriteLine("No choice made. No default choice or timeout segment specified.");
                }

                choiceForm.Invoke(new Action(() => choiceForm.Close()));
            }
        });

        choiceForm.ShowDialog();

        if (videoId == "81271335" && segment.LayoutType == "l1")
        {
            if (correctAnswersCount == 3)
            {
                selectedSegmentId = choices[0].SegmentId;
                //Console.WriteLine("Correct Segment.");
            }
            else
            {
                selectedSegmentId = choices[1].SegmentId;
                //Console.WriteLine("Incorrect Segment.");
            }
        }

        return (selectedSegmentId, selectedChoiceId);
    }

    private static Settings LoadSettings()
    {
        if (File.Exists(ConfigFilePath))
        {
            string json = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<Settings>(json);
        }
        return new Settings
        {
            AudioLanguage = "English",
            SubtitleLanguage = "Disabled",
        };
    }

    // Align the UI window with the video player
    private static void AlignWithVideoPlayer(Form choiceForm, string videoId, Segment segment)
    {
        IntPtr videoPlayerHandle = FindWindow(null, "VLC (Direct3D11 output)");
        if (videoPlayerHandle != IntPtr.Zero)
        {
            GetWindowRect(videoPlayerHandle, out RECT rect);

            // Find the width and height of the video player window
            int playerWidth = rect.Right - rect.Left;
            int playerHeight = rect.Bottom - rect.Top;

            // Set the choiceForm width to the player width
            choiceForm.Width = playerWidth;

            // Set the choiceForm height based on the videoId and layoutType
            double heightFactor = 0.30; // Default height factor
            if (segment.LayoutType == "ReubenZone" || segment.LayoutType == "EnderconZone" || segment.LayoutType == "TempleZone" || segment.LayoutType == "MCSMTeamName" || segment.LayoutType == "Crafting" || segment.LayoutType == "EpisodeEnd" || segment.LayoutType == "RedstoniaZone" || segment.LayoutType == "MCSMThroneZone" || segment.LayoutType == "MCSMTownZone" || segment.LayoutType == "MCSMWoolLand" || segment.LayoutType == "MCSMLabZone" || segment.LayoutType == "MCSMGunZone" || segment.LayoutType == "IvorZone")
            {
                heightFactor = 1;
            }
            else
            {
                switch (videoId)
                {
                    case "81004016":
                        heightFactor = 0.24;
                        break;
                    case "81481556":
                        heightFactor = 1;
                        if (segment.LayoutType == "l0")
                        {
                            heightFactor = 0.25;
                        }
                        break;
                    case "81328829":
                        heightFactor = 0.23;
                        break;
                    case "10000003":
                        heightFactor = 0.2;
                        break;
                    case "80988062":
                        heightFactor = 0.20;
                        break;
                    case "81271335":
                        if (segment.LayoutType == "l1")
                        {
                            heightFactor = 0.30;
                        }
                        else if (segment.LayoutType == "l0")
                        {
                            heightFactor = 0.23;
                        }
                        break;
                    case "81131714":
                        heightFactor = 0.18;
                        break;
                    case "80151644":
                        heightFactor = 0.3;
                        break;
                    case "81054409":
                        heightFactor = 0.35;
                        break;
                    case "81287545":
                        heightFactor = 0.35;
                        break;
                    case "81019938":
                        heightFactor = 0.35;
                        break;
                    case "81260654":
                        heightFactor = 0.35;
                        break;
                    case "81054415":
                        heightFactor = 0.35;
                        break;
                    case "81058723":
                        heightFactor = 0.35;
                        break;
                    case "80994695":
                        heightFactor = 0.22;
                        break;
                    case "10000001":
                        heightFactor = 0.2;
                        break;
                    case "81251335":
                        heightFactor = 0.217;
                        break;
                    case "80149064":
                        heightFactor = 0.305;
                        break;
                    case "80135585":
                        heightFactor = 0.40;
                        break;
                    case "81108751":
                        heightFactor = 0.23;
                        break;
                    case "81205738":
                        heightFactor = 0.23;
                        break;
                    case "80227804":
                        heightFactor = 0.23;
                        break;
                    case "80227805":
                        heightFactor = 0.23;
                        break;
                    case "80227800":
                        heightFactor = 0.23;
                        break;
                    case "80227801":
                        heightFactor = 0.23;
                        break;
                    case "80227802":
                        heightFactor = 0.23;
                        break;
                    case "80227803":
                        heightFactor = 0.23;
                        break;
                    case "80227699":
                        heightFactor = 0.23;
                        break;
                    case "80227698":
                        heightFactor = 0.23;
                        break;
                    case "81319137":
                        heightFactor = 0.23;
                        break;
                    case "81205737":
                        heightFactor = 0.23;
                        break;
                    case "81175265":
                        heightFactor = 0.25;
                        break;
                    case "80227815":
                        heightFactor = 1;
                        break;
                    case "81250260":
                        heightFactor = 1;
                        break;
                    case "81250261":
                        heightFactor = 0.33;
                        break;
                    case "81250262":
                        heightFactor = 0.33;
                        break;
                    case "81250263":
                        heightFactor = 0.33;
                        break;
                    case "81250264":
                        heightFactor = 0.33;
                        break;
                    case "81250265":
                        heightFactor = 0.33;
                        break;
                    case "81250266":
                        heightFactor = 0.33;
                        break;
                    case "81250267":
                        heightFactor = 0.33;
                        break;
                }
            }
            choiceForm.Height = (int)(playerHeight * heightFactor);

            // Center the choice window and align it with the bottom
            int centerX = rect.Left;
            int bottomY = rect.Bottom - choiceForm.Height;

            choiceForm.Location = new System.Drawing.Point(centerX, bottomY);
            SetWindowLong(choiceForm.Handle, GWL_HWNDPARENT, videoPlayerHandle);
        }
    }

    // Check if an Xbox controller is connected
    private static bool IsControllerConnected()
    {
        var directInput = new SharpDX.DirectInput.DirectInput();
        var joystickGuid = Guid.Empty;

        foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad, SharpDX.DirectInput.DeviceEnumerationFlags.AllDevices))
        {
            joystickGuid = deviceInstance.InstanceGuid;
            break;
        }

        if (joystickGuid == Guid.Empty)
        {
            foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Joystick, SharpDX.DirectInput.DeviceEnumerationFlags.AllDevices))
            {
                joystickGuid = deviceInstance.InstanceGuid;
                break;
            }
        }

        return joystickGuid != Guid.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int GWL_HWNDPARENT = -8;

    private const int LWA_COLORKEY = 0x00000001;
    private const int LWA_ALPHA = 0x00000002;

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    }

    private static Bitmap ExtractSprite(Bitmap spriteSheet, int rowIndex, int rowCount = 3)
    {
        if (spriteSheet == null) return null;
        int spriteHeight = spriteSheet.Height / rowCount;
        Rectangle spriteRect = new Rectangle(0, rowIndex * spriteHeight, spriteSheet.Width, spriteHeight);
        return spriteSheet.Clone(spriteRect, spriteSheet.PixelFormat);
    }

    private static Bitmap LoadBitmap(string path)
    {
        if (File.Exists(path))
        {
            return new Bitmap(path);
        }
        else
        {
            return null;
        }
    }
    private static void HandleControllerInput(ref int selectedIndex, List<Button> buttons, List<Bitmap> buttonSprites, ref bool inputCaptured, ref string selectedSegmentId, Form choiceForm, string selectSoundPath, string hoverSoundPath, LibVLC libVLC, string videoId, List<Choice> choices, Segment segment, string movieFolder)
    {
        var controller = new Controller(UserIndex.One);
        if (!controller.IsConnected)
        {
            return;
        }

        var state = controller.GetState();
        var gamepad = state.Gamepad;

        int previousIndex = selectedIndex;
        bool moved = false;

        bool is2x2Grid = videoId == "81481556" && ((segment.LayoutType == "l2" && buttons.Count == 4) || (segment.LayoutType == "l1" && buttons.Count == 4));
        int col = selectedIndex % 2;
        int row = selectedIndex / 2;

        // Handle D-Pad, left joystick, right joystick, and bumper input
        if (!inputCaptured)
        {
            // Left/Right
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft) || gamepad.LeftThumbX < -5000 || gamepad.RightThumbX < -5000 || gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
            {
                if (is2x2Grid)
                {
                    if (col > 0)
                        selectedIndex = row * 2 + (col - 1);
                }
                else
                    selectedIndex = Math.Max(0, selectedIndex - 1);
                moved = true;
            }
            else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) || gamepad.LeftThumbX > 5000 || gamepad.RightThumbX > 5000 || gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
            {
                if (is2x2Grid)
                {
                    if (col < 1)
                        selectedIndex = row * 2 + (col + 1);
                }
                else
                    selectedIndex = Math.Min(buttons.Count - 1, selectedIndex + 1);
                moved = true;
            }

            // Up/Down for 2x2 grid
            else if (is2x2Grid && (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) || gamepad.LeftThumbY > 5000 || gamepad.RightThumbY > 5000))
            {
                if (row > 0)
                    selectedIndex = (row - 1) * 2 + col;
                moved = true;
            }
            else if (is2x2Grid && (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown) || gamepad.LeftThumbY < -5000 || gamepad.RightThumbY < -5000))
            {
                if (row < 1)
                    selectedIndex = (row + 1) * 2 + col;
                moved = true;
            }

            // Play hover sound and rumble if the selected button changes
            if (moved && selectedIndex != previousIndex)
            {
                // Small rumble for moving to a choice
                controller.SetVibration(new Vibration { LeftMotorSpeed = 2000, RightMotorSpeed = 2000 });
                Task.Delay(100).ContinueWith(_ => controller.SetVibration(new Vibration())); // Stop rumble after 100ms
                Task.Delay(200).Wait();
                if (File.Exists(hoverSoundPath))
                {
                    var hoverPlayer = new MediaPlayer(new Media(libVLC, hoverSoundPath, FromType.FromPath));
                    hoverPlayer.Play();
                }
            }

            // Highlight the selected button
            for (int i = 0; i < buttons.Count; i++)
            {
                Bitmap focusedSprite = null;
                Bitmap defaultSprite = null;

                if (videoId == "81481556" && (segment.LayoutType == "l2" || segment.LayoutType == "l0"))
                {
                    defaultSprite = ExtractSprite(buttonSprites[i], 1, 6);
                    focusedSprite = ExtractSprite(buttonSprites[i], 2, 6);
                }
                else if (videoId == "81271335" && segment.LayoutType == "l1")
                {
                    // Use the full sprite for all states
                    defaultSprite = buttonSprites[i];
                    focusedSprite = buttonSprites[i];
                }
                else
                {
                    defaultSprite = ExtractSprite(buttonSprites[i], 0);
                    focusedSprite = ExtractSprite(buttonSprites[i], 1);
                }

                buttons[i].BackgroundImage = i == selectedIndex
                    ? new Bitmap(focusedSprite, buttons[i].Size)
                    : new Bitmap(defaultSprite, buttons[i].Size);
            }
        }

        // Handle selection
        if ((gamepad.Buttons.HasFlag(GamepadButtonFlags.A) || gamepad.RightTrigger > 128 || gamepad.LeftTrigger > 128) && !inputCaptured)
        {
            selectedSegmentId = (string)buttons[selectedIndex].Tag;
            inputCaptured = true;

            Bitmap selectedSprite = null;
            Bitmap correctSprite = null;
            Bitmap incorrectSprite = null;

            bool handledSpecial = false;
            if (videoId == "81481556" && segment.LayoutType == "l2" && segment.CorrectIndex.HasValue)
            {
                // Get sprites
                selectedSprite = ExtractSprite(buttonSprites[selectedIndex], 3, 6);
                correctSprite = ExtractSprite(buttonSprites[selectedIndex], 4, 6);
                incorrectSprite = ExtractSprite(buttonSprites[selectedIndex], 5, 6);

                if (selectedIndex == segment.CorrectIndex.Value)
                {
                    buttons[selectedIndex].BackgroundImage = new Bitmap(correctSprite, buttons[selectedIndex].Size);
                    // Play correct sound
                    string correctSoundPath = FindTexturePath(movieFolder, "sfx_select_correct.m4a");
                    if (!string.IsNullOrEmpty(correctSoundPath) && File.Exists(correctSoundPath))
                    {
                        var soundPlayer = new MediaPlayer(new Media(libVLC, correctSoundPath, FromType.FromPath));
                        soundPlayer.Play();
                    }
                }
                else
                {
                    buttons[selectedIndex].BackgroundImage = new Bitmap(incorrectSprite, buttons[selectedIndex].Size);
                    // Show correct button as correct
                    var correctButton = buttons[segment.CorrectIndex.Value];
                    var correctBtnSprite = ExtractSprite(buttonSprites[segment.CorrectIndex.Value], 4, 6);
                    correctButton.BackgroundImage = new Bitmap(correctBtnSprite, correctButton.Size);

                    // Play incorrect sound
                    string incorrectSoundPath = FindTexturePath(movieFolder, "sfx_select_incorrect.m4a");
                    if (!string.IsNullOrEmpty(incorrectSoundPath) && File.Exists(incorrectSoundPath))
                    {
                        var soundPlayer = new MediaPlayer(new Media(libVLC, incorrectSoundPath, FromType.FromPath));
                        soundPlayer.Play();
                    }
                }
                handledSpecial = true;
            }

            if (!handledSpecial)
            {
                // Default selection sprite
                if (videoId == "81481556" && (segment.LayoutType == "l2" || segment.LayoutType == "l0"))
                {
                    selectedSprite = ExtractSprite(buttonSprites[selectedIndex], 3, 6);
                }
                else if (videoId == "81271335" && segment.LayoutType == "l1")
                {
                    selectedSprite = buttonSprites[selectedIndex];
                }
                else
                {
                    selectedSprite = ExtractSprite(buttonSprites[selectedIndex], 2);
                }
                buttons[selectedIndex].BackgroundImage = new Bitmap(selectedSprite, buttons[selectedIndex].Size);

                if (File.Exists(selectSoundPath))
                {
                    var selectPlayer = new MediaPlayer(new Media(libVLC, selectSoundPath, FromType.FromPath));
                    selectPlayer.Play();
                }
            }

            buttons[selectedIndex].Enabled = false;
            foreach (var btn in buttons)
            {
                if (btn != buttons[selectedIndex])
                {
                    btn.Enabled = false;
                }
            }

            // Big rumble for selecting a choice
            controller.SetVibration(new Vibration { LeftMotorSpeed = 65535, RightMotorSpeed = 65535 });
            Task.Delay(300).ContinueWith(_ => controller.SetVibration(new Vibration())); // Stop rumble after 300ms

            if (videoId == "81481556" && segment.LayoutType == "l1" || videoId == "80988062" && choices.Any(choice => choice.Text?.Equals("GO BACK", StringComparison.OrdinalIgnoreCase) == true) || videoId == "80988062" && choices.Any(choice => choice.Text?.Equals("EXIT TO CREDITS", StringComparison.OrdinalIgnoreCase) == true) || videoId == "81131714" && choices.Any(choice => choice.Text?.Equals("EXIT TO CREDITS", StringComparison.OrdinalIgnoreCase) == true) || videoId == "81131714" && segment.LayoutType == "l6" || videoId == "10000001" || videoId == "10000003" || videoId == "81251335" || videoId == "80994695" || videoId == "80135585" || videoId == "81328829" || videoId == "81205738" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205737" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
            {
                choiceForm.Close(); // Close the form immediately after a choice is made
            }
            else if (videoId == "81481556" && segment.LayoutType == "l2")
            {
                // Delay closing the form by about 2 seconds (2000 ms)
                Task.Delay(2000).ContinueWith(_ =>
                {
                    if (choiceForm.IsHandleCreated)
                    {
                        choiceForm.Invoke(new Action(() => choiceForm.Close()));
                    }
                });
            }
            else
            {
                if (videoId == "81271335" && segment.LayoutType == "l1")
                {
                    inputCaptured = false;
                    //button.Enabled = true;
                }
                else
                {
                    choiceForm.ActiveControl = null;
                }
            }
        }
    }
    private static void EaseIntoFocusedSprite(Button button, Bitmap defaultSprite, Bitmap focusedSprite, int durationMs)
    {
        System.Windows.Forms.Timer easingTimer = new System.Windows.Forms.Timer { Interval = 10 };
        int elapsed = 0;

        easingTimer.Tick += (sender, e) =>
        {
            elapsed += easingTimer.Interval;
            double progress = Math.Min(1.0, (double)elapsed / durationMs);

            // Apply ease-out effect
            double easedProgress = EaseOutQuad(progress);

            // Interpolate between the default and focused sprites
            Bitmap blendedSprite = BlendSprites(defaultSprite, focusedSprite, easedProgress);
            button.BackgroundImage = new Bitmap(blendedSprite, button.Size);

            if (progress >= 1.0)
            {
                easingTimer.Stop();
            }
        };

        easingTimer.Start();
    }

    private static void EaseOutToDefaultSprite(Button button, Bitmap defaultSprite, Bitmap focusedSprite, int durationMs)
    {
        System.Windows.Forms.Timer easingTimer = new System.Windows.Forms.Timer { Interval = 10 };
        int elapsed = 0;

        easingTimer.Tick += (sender, e) =>
        {
            elapsed += easingTimer.Interval;
            double progress = Math.Min(1.0, (double)elapsed / durationMs);

            // Apply ease-out effect
            double easedProgress = EaseOutQuad(progress);

            // Interpolate between the focused and default sprites
            Bitmap blendedSprite = BlendSprites(focusedSprite, defaultSprite, easedProgress);
            button.BackgroundImage = new Bitmap(blendedSprite, button.Size);

            if (progress >= 1.0)
            {
                easingTimer.Stop();
            }
        };

        easingTimer.Start();
    }

    private static double EaseOutQuad(double t)
    {
        return t * (2 - t);
    }

    private static double EaseInQuad(double t)
    {
        return t * t;
    }

    private static double EaseOutElastic(double t)
    {
        double p = 0.3;
        return Math.Pow(2, -10 * t) * Math.Sin((t - p / 4) * (2 * Math.PI) / p) + 1;
    }

    public class ShadowLabel : Label
    {
        public Color ShadowColor { get; set; } = Color.Black;
        public int ShadowOffset { get; set; } = 2;

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush shadowBrush = new SolidBrush(ShadowColor))
            using (SolidBrush textBrush = new SolidBrush(this.ForeColor))
            {
                // Draw shadow
                var shadowLocation = new PointF(this.ShadowOffset, this.ShadowOffset);
                e.Graphics.DrawString(this.Text, this.Font, shadowBrush, shadowLocation);

                // Draw main text
                var textLocation = new PointF(0, 0);
                e.Graphics.DrawString(this.Text, this.Font, textBrush, textLocation);
            }
        }
    }

    private static Bitmap BlendSprites(Bitmap sprite1, Bitmap sprite2, double progress)
    {
        Bitmap blended = new Bitmap(sprite1.Width, sprite1.Height);
        using (Graphics g = Graphics.FromImage(blended))
        {
            ColorMatrix colorMatrix = new ColorMatrix
            {
                Matrix33 = (float)progress // Set the alpha value based on progress
            };

            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            g.DrawImage(sprite1, new Rectangle(0, 0, sprite1.Width, sprite1.Height));
            g.DrawImage(sprite2, new Rectangle(0, 0, sprite2.Width, sprite2.Height), 0, 0, sprite2.Width, sprite2.Height, GraphicsUnit.Pixel, attributes);
        }
        return blended;
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
}