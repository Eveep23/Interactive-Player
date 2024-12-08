using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

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
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            Width = formWidth
        };

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
                // Extract the default, focused, and selected sprites
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

                // Changing button images based on state
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

        // Load the timer fill texture
        string timerFillPath = Path.Combine(movieFolder, "timer_fill_2x.png");
        Bitmap timerFillSprite = File.Exists(timerFillPath) ? new Bitmap(timerFillPath) : null;

        int initialWidth = 1800;
        int timerBarHeight = timerFillSprite?.Height ?? 20;
        int formCenterX = formWidth / 2;

        int timerBarY = buttonTopMargin + buttonHeight + 40;

        PictureBox timerBar = new PictureBox
        {
            Location = new Point(formCenterX - (initialWidth / 2), timerBarY),
            Size = new Size(initialWidth, timerBarHeight),
            Image = timerFillSprite,
            SizeMode = PictureBoxSizeMode.StretchImage
        };
        choiceForm.Controls.Add(timerBar);

        choiceForm.Height = timerBarY + timerBarHeight + 80;

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
                int newWidth = (int)((double)initialWidth * remainingTime / (timeLimitMs / 1000));
                timerBar.Size = new Size(newWidth, timerBarHeight);
                timerBar.Location = new Point(formCenterX - (newWidth / 2), timerBarY);
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

    private static Bitmap ExtractSprite(Bitmap spriteSheet, int rowIndex)
    {
        if (spriteSheet == null) return null;

        int spriteHeight = spriteSheet.Height / 3;
        Rectangle spriteRect = new Rectangle(0, rowIndex * spriteHeight, spriteSheet.Width, spriteHeight);

        return spriteSheet.Clone(spriteRect, spriteSheet.PixelFormat);
    }
}
