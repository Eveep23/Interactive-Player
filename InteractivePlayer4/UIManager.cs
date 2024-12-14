using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

public static class UIManager
{
    public static string ShowChoiceUI(List<Choice> choices, List<Bitmap> buttonSprites, int timeLimitMs, string movieFolder)
    {
        string selectedSegmentId = null;
        bool inputCaptured = false;

        int formWidth = 1900;

        Form choiceForm = new Form
        {
            Text = "Make a Choice",
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.Green,
            TransparencyKey = Color.Green,
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            Width = formWidth,
            Height = 450
        };

        AlignWithVideoPlayer(choiceForm);

        int buttonHeight = 60;
        int horizontalSpacing = 10;
        int buttonTopMargin = 20;

        List<int> buttonWidths = new List<int>();
        List<Button> buttons = new List<Button>();

        for (int i = 0; i < choices.Count; i++)
        {
            var spriteSheet = buttonSprites[i];
            if (spriteSheet != null)
            {
                Bitmap defaultSprite = ExtractSprite(spriteSheet, 0);
                buttonWidths.Add(defaultSprite.Width);
            }
            else
            {
                buttonWidths.Add(300);
            }
        }

        int totalButtonsWidth = buttonWidths.Sum() + (choices.Count - 1) * horizontalSpacing;
        int buttonStartX = (formWidth - totalButtonsWidth) / 2;
        int currentX = buttonStartX;

        for (int i = 0; i < choices.Count; i++)
        {
            var spriteSheet = buttonSprites[i];
            if (spriteSheet != null)
            {
                Bitmap defaultSprite = ExtractSprite(spriteSheet, 0);
                Bitmap focusedSprite = ExtractSprite(spriteSheet, 1);
                Bitmap selectedSprite = ExtractSprite(spriteSheet, 2);

                int buttonWidth = buttonWidths[i];
                buttonHeight = defaultSprite.Height;

                var button = new Button
                {
                    Text = choices[i].Text,
                    Size = new Size(buttonWidth, buttonHeight),
                    Location = new Point(currentX, buttonTopMargin),
                    BackgroundImage = defaultSprite,
                    BackgroundImageLayout = ImageLayout.Stretch,
                    Tag = choices[i].SegmentId,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    UseVisualStyleBackColor = false,
                    TabStop = false
                };

                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseDownBackColor = Color.Transparent;
                button.FlatAppearance.MouseOverBackColor = Color.Transparent;
                button.FlatAppearance.CheckedBackColor = Color.Transparent;

                button.MouseEnter += (sender, e) => { if (button.Enabled) button.BackgroundImage = focusedSprite; };
                button.MouseLeave += (sender, e) => { if (button.Enabled) button.BackgroundImage = defaultSprite; };
                button.MouseDown += (sender, e) => { if (button.Enabled) button.BackgroundImage = selectedSprite; };
                button.MouseUp += (sender, e) => { if (button.Enabled) button.BackgroundImage = focusedSprite; };

                button.Click += (sender, e) =>
                {
                    if (!inputCaptured)
                    {
                        selectedSegmentId = (string)((Button)sender).Tag;
                        inputCaptured = true;

                        button.BackgroundImage = selectedSprite;
                        button.Enabled = false;

                        foreach (var btn in buttons)
                        {
                            if (btn != button)
                            {
                                btn.Enabled = false;
                            }
                        }

                        choiceForm.ActiveControl = null;
                    }
                };

                buttons.Add(button);
                choiceForm.Controls.Add(button);

                currentX += buttonWidth + horizontalSpacing;
            }
        }

        string FindTexturePath(string folder, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path))
                {
                    return path;
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
        string webPath = FindTexturePath(movieFolder, new[] { "web_2x.png", "web_2x_v2.png", "web_3x.png" });

        // Handle cases where a texture wasn't found (optional)
        if (webPath == null)
        {
            Console.WriteLine("Web texture not found.");
        }

        Bitmap timerFillSprite = LoadBitmap(timerFillPath);
        Bitmap timerCapLSprite = LoadBitmap(timerCapLPath);
        Bitmap timerCapRSprite = LoadBitmap(timerCapRPath);
        Bitmap timerBottomSprite = LoadBitmap(timerBottomPath);
        Bitmap timerTopSprite = LoadBitmap(timerTopPath);
        Bitmap webSprite = LoadBitmap(webPath);

        int initialWidth = 1800;
        int timerBarHeight = timerFillSprite?.Height ?? 20;
        int formCenterX = formWidth / 2;
        int timerBarY = buttonTopMargin + buttonHeight + 40;

        // Create a Panel
        Panel drawingPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(formWidth, choiceForm.Height),
            BackColor = Color.Transparent
        };

        drawingPanel.Paint += (sender, e) =>
        {
            Graphics g = e.Graphics;

            int alignedY = timerBarY;

            // Draw timer bottom
            if (timerBottomSprite != null)
            {
                g.DrawImage(timerBottomSprite, new Rectangle((formWidth - 1800) / 2, alignedY, 1800, 50));
            }

            // Draw timer fill
            if (timerFillSprite != null)
            {
                // All this crap is due to System.Drawings applying a fade effect
                int leftEdgeWidth = 10;
                int rightEdgeWidth = 10;
                int middleWidth = Math.Max(0, initialWidth - leftEdgeWidth - rightEdgeWidth);
                int totalWidth = leftEdgeWidth + middleWidth + rightEdgeWidth;
                int destX = (formWidth - totalWidth) / 2;
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
                g.DrawImage(timerCapLSprite, new Rectangle((formWidth - initialWidth) / 2 - timerCapLSprite.Width, alignedY, timerCapLSprite.Width, timerBarHeight));
            }

            // Draw right cap
            if (timerCapRSprite != null)
            {
                g.DrawImage(timerCapRSprite, new Rectangle((formWidth + initialWidth) / 2, alignedY, timerCapRSprite.Width, timerBarHeight));
            }

            // Draw timer top
            if (timerTopSprite != null)
            {
                g.DrawImage(timerTopSprite, new Rectangle((formWidth - 1800) / 2, alignedY, 1800, 50));
            }

            // Draw overlay
            if (webSprite != null)
            {
                int webY = alignedY + (timerBarHeight / 2) - (webSprite.Height / 2);
                g.DrawImage(webSprite, new Rectangle((formWidth - webSprite.Width) / 2, webY, webSprite.Width, webSprite.Height));
            }
        };

        choiceForm.Controls.Add(drawingPanel);

        System.Windows.Forms.Timer countdownTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };

        int remainingTime = timeLimitMs / 1000;

        countdownTimer.Tick += (sender, e) =>
        {
            remainingTime--;

            if (remainingTime >= 0)
            {
                initialWidth = (int)((double)1800 * remainingTime / (timeLimitMs / 1000));
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

    // Align the UI window with the video player
    private static void AlignWithVideoPlayer(Form choiceForm)
    {
        IntPtr videoPlayerHandle = FindWindow(null, "VLC (Direct3D11 output)");
        if (videoPlayerHandle != IntPtr.Zero)
        {
            GetWindowRect(videoPlayerHandle, out RECT rect);

            // Find the width and height of the video player window
            int playerWidth = rect.Right - rect.Left;
            int playerHeight = rect.Bottom - rect.Top;

            // Center the choice window and align it with the bottom
            int centerX = rect.Left + (playerWidth / 2) - (choiceForm.Width / 2);
            int bottomY = rect.Bottom - choiceForm.Height;

            choiceForm.Location = new Point(centerX, bottomY);
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

    private static Bitmap ExtractSprite(Bitmap spriteSheet, int rowIndex)
    {
        if (spriteSheet == null) return null;

        int spriteHeight = spriteSheet.Height / 3;
        Rectangle spriteRect = new Rectangle(0, rowIndex * spriteHeight, spriteSheet.Width, spriteHeight);

        return spriteSheet.Clone(spriteRect, spriteSheet.PixelFormat);
    }

    private static Bitmap LoadBitmap(string path)
    {
        return File.Exists(path) ? new Bitmap(path) : null;
    }
}