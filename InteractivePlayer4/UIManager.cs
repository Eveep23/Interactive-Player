﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using LibVLCSharp.Shared;


public static class UIManager
{
    private static readonly string ConfigFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

    public static string ShowChoiceUI(List<Choice> choices, List<Bitmap> buttonSprites, List<Bitmap> buttonIcons, int timeLimitMs, string movieFolder, string videoId)
    {
        string selectedSegmentId = null;
        bool inputCaptured = false;

        int formWidth = 1900;

        Form choiceForm = new Form
        {
            Text = "Make a Choice",
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = videoId == "81004016" ? Color.Black : Color.Tan,
            TransparencyKey = Color.Tan,
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            Width = formWidth,
            Height = 450
        };

        AlignWithVideoPlayer(choiceForm, videoId);

        // Load settings
        var settings = LoadSettings();
        int uiSpeed = settings.UISpeed;

        // Calculate scaling factor based on the resized form
        double scaleFactor = (double)choiceForm.Width / formWidth;

        int buttonHeight = (int)(60 * scaleFactor);
        int horizontalSpacing = (int)(10 * scaleFactor);
        int buttonTopMargin = (int)(20 * scaleFactor);

        List<int> buttonWidths = new List<int>();
        List<Button> buttons = new List<Button>();

        // Initialize VLC
        Core.Initialize();
        var libVLC = new LibVLC();

        // Load sound files
        string hoverSoundPath = FindTexturePath(movieFolder, new[] { "CSD_Hover.m4a", "cap_focus.m4a", "sfx_focus.m4a", "sfx_focus_44100.m4a", "toggle.m4a", "sfx_focus.m4a", "IX_choicePointSound_tonal_focus_48k.m4a" });
        string selectSoundPath = FindTexturePath(movieFolder, new[] { "CSD_Select.m4a", "cap_select.m4a", "sfx_select.m4a", "sfx_selected_44100.m4a", "select.m4a", "spirit_select_48.m4a", "sfx_buttonSelect.m4a", "IX_choicePointSound_tonal_select_48k.m4a" });

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

        int currentX = spacing;

        for (int i = 0; i < choices.Count; i++)
        {
            var spriteSheet = buttonSprites[i];
            if (spriteSheet != null)
            {
                Bitmap defaultSprite = ExtractSprite(spriteSheet, 0);
                Bitmap focusedSprite = ExtractSprite(spriteSheet, 1);
                Bitmap selectedSprite = ExtractSprite(spriteSheet, 2);

                int buttonWidth = buttonWidths[i];
                buttonHeight = (int)(defaultSprite.Height * scaleFactor);

                var button = new Button
                {
                    Text = (new[] { "80149064", "80135585", "81054409", "81287545", "81019938", "81260654", "81054415", "81058723" }.Contains(videoId)) ? string.Empty : choices[i].Text,
                    Size = new Size(buttonWidth, buttonHeight),
                    Location = new Point(0, 0), // Position within the panel
                    BackgroundImage = new Bitmap(defaultSprite, new Size(buttonWidth, buttonHeight)),
                    BackgroundImageLayout = ImageLayout.Stretch,
                    Tag = choices[i].SegmentId,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    UseVisualStyleBackColor = false,
                    TabStop = false,
                    Font = new Font("Arial", (float)(22 * scaleFactor), FontStyle.Bold), // Set font to Arial, bold and scale it
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = (new[] { "81004016", "81205738", "81108751" }.Contains(videoId)) ? new Padding((int)(buttonWidth * 0.45), 0, 0, 0) : new Padding(0)
                };

                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseDownBackColor = Color.Transparent;
                button.FlatAppearance.MouseOverBackColor = Color.Transparent;
                button.FlatAppearance.CheckedBackColor = Color.Transparent;

                button.MouseEnter += (sender, e) =>
                {
                    if (button.Enabled)
                    {
                        button.BackgroundImage = new Bitmap(focusedSprite, new Size(buttonWidth, buttonHeight));
                        if (File.Exists(hoverSoundPath))
                        {
                            var hoverPlayer = new MediaPlayer(new Media(libVLC, hoverSoundPath, FromType.FromPath));
                            hoverPlayer.Play();
                        }
                    }
                };
                button.MouseLeave += (sender, e) =>
                {
                    if (button.Enabled)
                    {
                        button.BackgroundImage = new Bitmap(defaultSprite, new Size(buttonWidth, buttonHeight));
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

                        foreach (var btn in buttons)
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
                        choiceForm.ActiveControl = null;
                    }
                };

                // Adjust height to accommodate text only if the video ID matches
                int panelHeight = (new[] { "81054409", "81287545", "81019938", "81260654", "81054415", "81058723" }.Contains(videoId)) ? buttonHeight + (int)(50 * scaleFactor) : buttonHeight;

                var buttonPanel = new Panel
                {
                    Size = new Size(buttonWidth, panelHeight),
                    Location = new Point(currentX, buttonTopMargin),
                    BackColor = Color.Transparent
                };

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
                        Location = new Point(0, (buttonHeight - iconHeight) / 2), // Left aligned
                        BackColor = Color.Transparent,
                        Enabled = false
                    };

                    button.Controls.Add(iconPictureBox);
                }

                // Add text label underneath the button for specific video IDs
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

                    // Center the label horizontally within the buttonPanel
                    textLabel.Location = new Point((buttonPanel.Width - textLabel.Width) / 2, buttonHeight + 10);
                }

                buttons.Add(button);
                choiceForm.Controls.Add(buttonPanel);

                currentX += buttonWidth + spacing;
            }
        }

        // Adjust the timer bar position to avoid overlapping with the buttons and labels
        int timerBarY;
        if (new[] { "81054409", "81287545", "81019938", "81260654", "81054415", "81058723" }.Contains(videoId))
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

        // Define possible names for each texture
        string timerFillPath = FindTexturePath(movieFolder, new[] { "timer_fill_2x.png", "timer_fill_2x_v2.png", "timer_fill_3x.png" });
        string timerCapLPath = FindTexturePath(movieFolder, new[] { "timer_capL_2x.png", "timer_capL_2x_v2.png", "timer_capL_3x.png" });
        string timerCapRPath = FindTexturePath(movieFolder, new[] { "timer_capR_2x.png", "timer_capR_2x_v2.png", "timer_capR_3x.png" });
        string timerBottomPath = FindTexturePath(movieFolder, new[] { "timer_bottom_2x.png", "timer_bottom_2x_v2.png", "timer_bottom_3x.png" });
        string timerTopPath = FindTexturePath(movieFolder, new[] { "timer_top_2x.png", "timer_top_2x_v2.png", "timer_top_3x.png" });
        string webPath = FindTexturePath(movieFolder, new[] { "web_2x.png", "web_2x_v2.png", "web_3x.png", "web_icon_2x.png" });

        // Handle cases where a texture wasn't found
        if (webPath == null)
        {
            Console.WriteLine("Texture not found.");
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

        // Create a Panel
        Panel drawingPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(choiceForm.Width, choiceForm.Height),
            BackColor = Color.Transparent
        };

        drawingPanel.Paint += (sender, e) =>
        {
            Graphics g = e.Graphics;

            int alignedY = timerBarY;

            // Draw timer bottom
            if (timerBottomSprite != null)
            {
                g.DrawImage(timerBottomSprite, new Rectangle((choiceForm.Width - (int)(1800 * scaleFactor)) / 2, alignedY, (int)(1800 * scaleFactor), (int)(50 * scaleFactor)));
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
                int destY = alignedY;
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
                g.DrawImage(timerCapLSprite, new Rectangle((choiceForm.Width - initialWidth) / 2 - (int)(timerCapLSprite.Width * scaleFactor), alignedY, (int)(timerCapLSprite.Width * scaleFactor), timerBarHeight));
            }

            // Draw right cap
            if (timerCapRSprite != null)
            {
                g.DrawImage(timerCapRSprite, new Rectangle((choiceForm.Width + initialWidth) / 2, alignedY, (int)(timerCapRSprite.Width * scaleFactor), timerBarHeight));
            }

            // Draw timer top
            if (timerTopSprite != null)
            {
                g.DrawImage(timerTopSprite, new Rectangle((choiceForm.Width - (int)(1800 * scaleFactor)) / 2, alignedY, (int)(1800 * scaleFactor), (int)(50 * scaleFactor)));
            }

            // Draw overlay
            if (webSprite != null)
            {
                int webY = alignedY + (timerBarHeight / 2) - (int)(webSprite.Height * scaleFactor / 2);
                g.DrawImage(webSprite, new Rectangle((choiceForm.Width - (int)(webSprite.Width * scaleFactor)) / 2, webY, (int)(webSprite.Width * scaleFactor), (int)(webSprite.Height * scaleFactor)));
            }
        };

        choiceForm.Controls.Add(drawingPanel);

        System.Windows.Forms.Timer countdownTimer = new System.Windows.Forms.Timer
        {
            Interval = uiSpeed
        };

        int remainingTime = timeLimitMs / uiSpeed;

        countdownTimer.Tick += (sender, e) =>
        {
            remainingTime--;

            if (remainingTime >= 0)
            {
                initialWidth = (int)((double)(1650 * scaleFactor) * remainingTime / (timeLimitMs / uiSpeed));
                drawingPanel.Invalidate();
            }

            if (remainingTime <= 0)
            {
                countdownTimer.Stop();
                choiceForm.Close();
            }
        };

        countdownTimer.Start();
        choiceForm.ShowDialog();

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
            UISpeed = 750
        };
    }

    // Align the UI window with the video player
    private static void AlignWithVideoPlayer(Form choiceForm, string videoId)
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

            // Set the choiceForm height based on the videoId
            double heightFactor = 0.40; // Default height factor
            switch (videoId)
            {
                case "81004016":
                    heightFactor = 0.30;
                    break;
                case "81054409":
                    heightFactor = 0.45;
                    break;
                case "81287545":
                    heightFactor = 0.45;
                    break;
                case "81019938":
                    heightFactor = 0.45;
                    break;
                case "81260654":
                    heightFactor = 0.45;
                    break;
                case "81054415":
                    heightFactor = 0.45;
                    break;
                case "81058723":
                    heightFactor = 0.45;
                    break;
            }
            choiceForm.Height = (int)(playerHeight * heightFactor);

            // Center the choice window and align it with the bottom
            int centerX = rect.Left;
            int bottomY = rect.Bottom - choiceForm.Height;

            choiceForm.Location = new Point(centerX, bottomY);
            SetWindowLong(choiceForm.Handle, GWL_HWNDPARENT, videoPlayerHandle);
        }
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

    private const int GWL_HWNDPARENT = -8;

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
            Console.WriteLine($"Bitmap not found at path: {path}");
            return null;
        }
    }
}
