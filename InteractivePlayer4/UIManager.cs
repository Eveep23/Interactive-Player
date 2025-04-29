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
            Width = formWidth,
            Height = 200
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

            // Find the width and height of the video player window
            int playerWidth = rect.Right - rect.Left;
            int playerHeight = rect.Bottom - rect.Top;

            // Set the notificationForm width to the player width
            notificationForm.Width = playerWidth;

            // Set the notificationForm height to a fixed value
            notificationForm.Height = (int)(playerHeight * 0.10); // Adjust height factor as needed

            // Center the notification window and align it with the top, adding a top margin
            int centerX = rect.Left;
            int topY = rect.Top + 30; // Add a top margin of 30 pixels

            notificationForm.Location = new System.Drawing.Point(centerX, topY);
            SetWindowLong(notificationForm.Handle, GWL_HWNDPARENT, videoPlayerHandle);
        }
    }

    private static bool soundPlayed = false;
    private static int correctAnswersCount = 0;

    public static string ShowChoiceUI(List<Choice> choices, List<Bitmap> buttonSprites, List<Bitmap> buttonIcons, int timeLimitMs, string movieFolder, string videoId, Segment segment)
    {
        string selectedSegmentId = null;
        bool inputCaptured = false;

        correctAnswersCount = 0;

        soundPlayed = false;

        int formWidth = 1900;

        Form choiceForm = new Form
        {
            Text = "Make a Choice",
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = videoId == "80149064" ? Color.FromArgb(15, 15, 15) :
                       (videoId == "80151644" ? Color.FromArgb(125, 125, 125) :
                       (videoId == "81004016" || videoId == "80988062" || videoId == "81271335" && segment.LayoutType == "l1" ? Color.Black :
                       (videoId == "81131714" ? Color.FromArgb(247, 233, 95) :
                       Color.FromArgb(41, 41, 41)))),
            TransparencyKey = (videoId == "81004016" || videoId == "80988062" || videoId == "81131714" || videoId == "81271335" && segment.LayoutType == "l1") ? Color.Empty :
                              (videoId == "80149064" ? Color.FromArgb(15, 15, 15) :
                              (videoId == "80151644" ? Color.FromArgb(125, 125, 125) :
                              Color.FromArgb(41, 41, 41))),
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            Width = formWidth,
            Height = 450,
            Opacity = 0
        };

        AlignWithVideoPlayer(choiceForm, videoId, segment);

        if (videoId == "81271335" && segment.LayoutType == "l1")
        {
            // Load the persistent state from the save file
            string saveFilePath = Path.Combine(movieFolder, "save.json");
            var saveData = SaveManager.LoadSaveData(saveFilePath);

            int p_qs = 1; // Default to 1 if not found
            if (saveData?.PersistentState != null && saveData.PersistentState.TryGetValue("p_qs", out var p_qsValue))
            {
                if (int.TryParse(p_qsValue.ToString(), out int parsedValue))
                {
                    p_qs = parsedValue;
                }
            }

            string backgroundFileName;
            switch (p_qs)
            {
                case 0:
                    backgroundFileName = "lvl1_2x.png";
                    break;
                case 1:
                    backgroundFileName = "lvl2_2x.png";
                    break;
                case 2:
                    backgroundFileName = "lvl3_2x.png";
                    break;
                case 3:
                    backgroundFileName = "lvl4_2x.png";
                    break;
                case 4:
                    backgroundFileName = "lvl5_2x.png";
                    break;
                case 5:
                    backgroundFileName = "lvl6_2x.png";
                    break;
                default:
                    backgroundFileName = "lvl1_2x.png"; 
                    break;
            }

            // Find the background path
            string backgroundPath = FindTexturePath(movieFolder, new[] { backgroundFileName });
            if (!string.IsNullOrEmpty(backgroundPath) && File.Exists(backgroundPath))
            {
                choiceForm.BackgroundImage = new Bitmap(backgroundPath);
                choiceForm.BackgroundImageLayout = ImageLayout.Stretch;
            }
        }

        if (new[] { "81131714", "81004016", "80988062", "81271335", "10000003" }.Contains(videoId))
        {
            int targetY = choiceForm.Location.Y;
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
        }

        // Load settings
        var settings = LoadSettings();

        // Calculate scaling factor based on the resized form
        double scaleFactor = (double)choiceForm.Width / formWidth;

        // Apply additional scaling for specific video ID
        if (videoId == "10000001" || videoId == "10000003" || videoId == "81251335" || videoId == "81271335" || videoId == "81287545" || videoId == "80149064" || videoId == "81260654" || videoId == "80994695" || videoId == "81328829" || videoId == "81058723" || videoId == "81054409" || videoId == "81108751" || videoId == "81004016" || videoId == "80988062" || videoId == "81131714" || videoId == "81205738" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205737" || videoId == "81054415" || videoId == "81175265" || videoId == "81019938" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
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
                Bitmap defaultSprite = ExtractSprite(spriteSheet, 0);
                Bitmap focusedSprite = ExtractSprite(spriteSheet, 1);
                Bitmap selectedSprite = ExtractSprite(spriteSheet, 2);

                if (videoId == "81271335" && segment.LayoutType == "l1")
                {
                    // For videoId 81271335, use the single sprite directly
                    defaultSprite = spriteSheet;
                    focusedSprite = spriteSheet;
                    selectedSprite = spriteSheet;
                }
                else
                {
                    // For other videoIds, split the sprite sheet into rows
                    defaultSprite = ExtractSprite(spriteSheet, 0);
                    focusedSprite = ExtractSprite(spriteSheet, 1);
                    selectedSprite = ExtractSprite(spriteSheet, 2);
                }

                int buttonWidth = buttonWidths[i];
                buttonHeight = (int)(defaultSprite.Height * scaleFactor);

                var button = new Button
                {
                    Text = (segment.LayoutType == "ReubenZone" || segment.LayoutType == "EnderconZone" || segment.LayoutType == "TempleZone" || segment.LayoutType == "Crafting" || segment.LayoutType == "EpisodeEnd" || segment.LayoutType == "RedstoniaZone" || segment.LayoutType == "MCSMThroneZone" || segment.LayoutType == "MCSMTownZone" || segment.LayoutType == "MCSMWoolLand" || segment.LayoutType == "MCSMLabZone" || segment.LayoutType == "MCSMGunZone" || segment.LayoutType == "IvorZone" || videoId == "81271335" && segment.LayoutType == "l1") ? string.Empty : (new[] { "80149064", "80135585", "81054409", "81287545", "81019938", "81260654", "81054415", "81058723", "80227815", "81250260", "81250261", "81250262", "81250263", "81250264", "81250265", "81250266", "81250267" }.Contains(videoId)) ? string.Empty : choices[i].Text,
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
                    ForeColor = (videoId == "81328829") ? Color.Black : // Set text color to black for Headspace
                                (new[] { "80227804", "80227805", "80227800", "80227801", "80227802", "80227803", "80227699", "80227698" }.Contains(videoId)) ? ColorTranslator.FromHtml("#27170a") :
                                (videoId == "81131714" ? ColorTranslator.FromHtml("#dc007f") : Color.White),
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
                        if (videoId == "10000001")
                        {
                            EaseIntoFocusedSprite(button, defaultSprite, focusedSprite, 65);
                        }
                        else
                        {
                            button.BackgroundImage = new Bitmap(focusedSprite, new Size(buttonWidth, buttonHeight));
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
                        if (videoId == "10000001")
                        {
                            EaseOutToDefaultSprite(button, defaultSprite, focusedSprite, 65);
                        }
                        else
                        {
                            button.BackgroundImage = new Bitmap(defaultSprite, new Size(buttonWidth, buttonHeight));
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
                        inputCaptured = true;

                        button.BackgroundImage = new Bitmap(selectedSprite, new Size(buttonWidth, buttonHeight));
                        button.Enabled = false;

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

                        if (videoId == "10000001" || videoId == "10000003" || videoId == "81251335" || videoId == "80994695" || videoId == "80135585" || videoId == "81328829" || videoId == "81205738" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205737" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
                        {
                            choiceForm.Close(); // Close the form immediately after a choice is made
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
                    if (choices[i].Text == "TNT launcher")
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
                    if (choices[i].Text == "Craft Anvil")
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
        if (segment.LayoutType == "ReubenZone" || segment.LayoutType == "EnderconZone" || segment.LayoutType == "TempleZone" || segment.LayoutType == "MCSMTeamName" || segment.LayoutType == "Crafting" || segment.LayoutType == "EpisodeEnd" || segment.LayoutType == "RedstoniaZone" || segment.LayoutType == "MCSMThroneZone" || segment.LayoutType == "MCSMTownZone" || segment.LayoutType == "MCSMWoolLand" || segment.LayoutType == "MCSMLabZone" || segment.LayoutType == "MCSMGunZone" || segment.LayoutType == "IvorZone")
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

        // If Cat Burglar and a controller is connected, show a message and restart the program
        if (videoId == "81271335" && isControllerConnected)
        {
            var result = MessageBox.Show(
                "Cat Burglar isn't currently compatible with a gamepad controller, please disconnect the gamepad controller",
                "Controller Not Supported",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.OK)
            {
                Application.Restart();
                Environment.Exit(0); 
            }
        }

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
            else if (new[] { "80227815", "81250260", "81250261", "81250262", "81250263", "81250264", "81250265", "81250266", "81250267" }.Contains(videoId))
            {
                // Calculate the eased Y position for the specified video IDs
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
                await Task.Delay(16); // Update approximately every 16ms (~60 FPS)
            }

            if (!inputCaptured && File.Exists(timeoutSoundPath))
            {
                var timeoutPlayer = new MediaPlayer(new Media(libVLC, timeoutSoundPath, FromType.FromPath));
                timeoutPlayer.Play();
            }

            choiceForm.Invoke(new Action(() => choiceForm.Close()));
        });

        if (new[] { "10000001", "80227815", "81250260", "81250261", "81250262", "81250263", "81250264", "81250265", "81250266", "81250267" }.Contains(videoId))
        {
            Task.Run(async () =>
            {
                int duration = videoId == "10000001" ? 370 : 400;
                while (stopwatch.ElapsedMilliseconds < duration)
                {
                    drawingPanel.Invalidate();
                    await Task.Delay(16); // Update approximately every 16ms (~60 FPS)
                }
            });
        }

        Task.Run(async () =>
        {
            int selectedIndex = 0; // Initialize selected index for controller input

            while (stopwatch.ElapsedMilliseconds < timeLimitMs)
            {
                initialWidth = (int)((double)(1650 * scaleFactor) * (timeLimitMs - stopwatch.ElapsedMilliseconds) / timeLimitMs);
                drawingPanel.Invalidate();

                // Handle controller input
                HandleControllerInput(ref selectedIndex, buttons, buttonSprites, ref inputCaptured, ref selectedSegmentId, choiceForm, selectSoundPath, hoverSoundPath, libVLC, videoId);

                await Task.Delay(16); // Update approximately every 16ms (~60 FPS)
            }

            if (!inputCaptured && File.Exists(timeoutSoundPath))
            {
                var timeoutPlayer = new MediaPlayer(new Media(libVLC, timeoutSoundPath, FromType.FromPath));
                timeoutPlayer.Play();
            }

            choiceForm.Invoke(new Action(() => choiceForm.Close()));
        });

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
                // If no choice was made, select the default choice
                if (segment.DefaultChoiceIndex.HasValue && segment.DefaultChoiceIndex.Value >= 0 && segment.DefaultChoiceIndex.Value < choices.Count)
                {

                    if (segment.TimeoutSegment != null)
                    {
                        selectedSegmentId = IsControllerConnected() ? "Fallback_Tutorial_Controller" : "Fallback_Tutorial_Site";
                    }
                    else
                    {
                        selectedSegmentId = choices[segment.DefaultChoiceIndex.Value].SegmentId;
                    }
                        
                    Console.WriteLine($"No choice made. Defaulting to the specified choice.");
                }
                else
                {
                    Console.WriteLine("No choice made. No default choice specified.");
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

        return selectedSegmentId;
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
                    case "81328829":
                        heightFactor = 0.23;
                        break;
                    case "10000003":
                        heightFactor = 0.2;
                        break;
                    case "80988062":
                        heightFactor = 0.24;
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
                        heightFactor = 0.24;
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

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;

    private const int GWL_HWNDPARENT = -8;

    private const int LWA_COLORKEY = 0x00000001;
    private const int LWA_ALPHA = 0x00000002;

    private static Bitmap ExtractSprite(Bitmap spriteSheet, int rowIndex)
    {
        if (spriteSheet == null) return null;

        int spriteHeight = spriteSheet.Height / 3;
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
    private static void HandleControllerInput(ref int selectedIndex, List<Button> buttons, List<Bitmap> buttonSprites, ref bool inputCaptured, ref string selectedSegmentId, Form choiceForm, string selectSoundPath, string hoverSoundPath, LibVLC libVLC, string videoId)
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

        // Handle D-Pad, left joystick, right joystick, and bumper input
        if (!inputCaptured)
        {
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft) || gamepad.LeftThumbX < -5000 || gamepad.RightThumbX < -5000 || gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
            {
                selectedIndex = Math.Max(0, selectedIndex - 1);
                moved = true;
            }
            else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) || gamepad.LeftThumbX > 5000 || gamepad.RightThumbX > 5000 || gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
            {
                selectedIndex = Math.Min(buttons.Count - 1, selectedIndex + 1);
                moved = true;
            }

            // Play hover sound and rumble if the selected button changes
            if (moved && selectedIndex != previousIndex)
            {
                // Small rumble for moving to a choice
                controller.SetVibration(new Vibration { LeftMotorSpeed = 2000, RightMotorSpeed = 2000 });
                Task.Delay(100).ContinueWith(_ => controller.SetVibration(new Vibration())); // Stop rumble after 100ms

                // Add delay to slow down the movement
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
                buttons[i].BackgroundImage = i == selectedIndex ? new Bitmap(ExtractSprite(buttonSprites[i], 1), buttons[i].Size) : new Bitmap(ExtractSprite(buttonSprites[i], 0), buttons[i].Size);
            }
        }

        // Handle selection
        if ((gamepad.Buttons.HasFlag(GamepadButtonFlags.A) || gamepad.RightTrigger > 128 || gamepad.LeftTrigger > 128) && !inputCaptured)
        {
            selectedSegmentId = (string)buttons[selectedIndex].Tag;
            inputCaptured = true;

            buttons[selectedIndex].BackgroundImage = new Bitmap(ExtractSprite(buttonSprites[selectedIndex], 2), buttons[selectedIndex].Size);
            buttons[selectedIndex].Enabled = false;

            foreach (var btn in buttons)
            {
                if (btn != buttons[selectedIndex])
                {
                    btn.Enabled = false;
                }
            }

            if (File.Exists(selectSoundPath))
            {
                var selectPlayer = new MediaPlayer(new Media(libVLC, selectSoundPath, FromType.FromPath));
                selectPlayer.Play();
            }

            // Big rumble for selecting a choice
            controller.SetVibration(new Vibration { LeftMotorSpeed = 65535, RightMotorSpeed = 65535 });
            Task.Delay(300).ContinueWith(_ => controller.SetVibration(new Vibration())); // Stop rumble after 300ms

            if (videoId == "10000001" || videoId == "10000003" || videoId == "81251335" || videoId == "80994695" || videoId == "80135585" || videoId == "81328829" || videoId == "81205738" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205737" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
            {
                choiceForm.Close(); // Close the form immediately after a choice is made
            }
            else
            {
                choiceForm.ActiveControl = null;
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

    private static double EaseOutElastic(double t)
    {
        double p = 0.3;
        return Math.Pow(2, -10 * t) * Math.Sin((t - p / 4) * (2 * Math.PI) / p) + 1;
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