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

        // Calculate scaling factor based on the resized form
        double scaleFactor = (double)choiceForm.Width / formWidth;

        int buttonHeight = (int)(60 * scaleFactor);
        int horizontalSpacing = (int)(10 * scaleFactor);
        int buttonTopMargin = (int)(20 * scaleFactor);

        List<int> buttonWidths = new List<int>();
        List<Button> buttons = new List<Button>();

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

        int totalButtonsWidth = buttonWidths.Sum() + (choices.Count - 1) * horizontalSpacing;
        int buttonStartX = (choiceForm.Width - totalButtonsWidth) / 2;
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
                buttonHeight = (int)(defaultSprite.Height * scaleFactor);

                var button = new Button
                {
                    Text = choices[i].Text,
                    Size = new Size(buttonWidth, buttonHeight),
                    Location = new Point(currentX, buttonTopMargin),
                    BackgroundImage = new Bitmap(defaultSprite, new Size(buttonWidth, buttonHeight)),
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

                button.MouseEnter += (sender, e) => { if (button.Enabled) button.BackgroundImage = new Bitmap(focusedSprite, new Size(buttonWidth, buttonHeight)); };
                button.MouseLeave += (sender, e) => { if (button.Enabled) button.BackgroundImage = new Bitmap(defaultSprite, new Size(buttonWidth, buttonHeight)); };
                button.MouseDown += (sender, e) => { if (button.Enabled) button.BackgroundImage = new Bitmap(selectedSprite, new Size(buttonWidth, buttonHeight)); };
                button.MouseUp += (sender, e) => { if (button.Enabled) button.BackgroundImage = new Bitmap(focusedSprite, new Size(buttonWidth, buttonHeight)); };

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
        int timerBarY = buttonTopMargin + buttonHeight + (int)(40 * scaleFactor);

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
            Interval = 750
        };

        int remainingTime = timeLimitMs / 750;

        countdownTimer.Tick += (sender, e) =>
        {
            remainingTime--;

            if (remainingTime >= 0)
            {
                initialWidth = (int)((double)(1700 * scaleFactor) * remainingTime / (timeLimitMs / 750));
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

            // Resize the choiceForm to fit inside the VLC window
            choiceForm.Width = Math.Min(choiceForm.Width, playerWidth);
            choiceForm.Height = Math.Min(choiceForm.Height, playerHeight);

            // Center the choice window and align it with the bottom
            int centerX = rect.Left + (playerWidth / 2) - (choiceForm.Width / 2);
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
        return File.Exists(path) ? new Bitmap(path) : null;
    }
}