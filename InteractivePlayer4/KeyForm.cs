using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public static class KeyForm
{
    private static Form keyPressForm;

    private static System.Threading.Thread keyPressThread;

    public static void InitializeKeyPressWindow(MediaPlayer mediaPlayer, string infoJsonFile, string saveFilePath, Segment currentSegment, Dictionary<string, Segment> segments)
    {
        if (keyPressForm != null && !keyPressForm.IsDisposed)
        {
            return;
        }

        string currentDirectory = Directory.GetCurrentDirectory();
        string topBarPath = Path.Combine(currentDirectory, "general", "Top_bar.png");
        string logoPath = Path.Combine(currentDirectory, "general", "Interactive_player_logo.png");
        string buttonUnselectedPath = Path.Combine(currentDirectory, "general", "ButtonUnselected.png");
        string buttonSelectedPath = Path.Combine(currentDirectory, "general", "ButtonSelected.png");

        keyPressForm = new Form
        {
            Text = "Interactive Player",
            Width = 400,
            Height = 250,
            ShowInTaskbar = true,
            TopMost = true,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            Location = new System.Drawing.Point(100, 100),
            BackColor = ColorTranslator.FromHtml("#141414")
        };

        Panel topBarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackgroundImage = Image.FromFile(topBarPath),
            BackgroundImageLayout = ImageLayout.Stretch
        };

        PictureBox logoPictureBox = new PictureBox
        {
            Image = Image.FromFile(logoPath),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Width = 190,
            Height = 50
        };

        topBarPanel.Controls.Add(logoPictureBox);
        logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        topBarPanel.Resize += (sender, e) =>
        {
            logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        };

        keyPressForm.Controls.Add(topBarPanel);

        // Buttons for Skip Back, Pause/Play, and Skip Forward
        Panel buttonPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        // Create a button for Skip Back
        Button skipBackButton = new Button
        {
            Width = 80,
            Height = 80,
            BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png")), // Background image
            BackgroundImageLayout = ImageLayout.Stretch,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            BackColor = Color.Transparent, // Transparent background
            FlatAppearance = 
            {
                BorderSize = 0,
                MouseDownBackColor = Color.Transparent,
                MouseOverBackColor = Color.Transparent
            }
        };

        // Draw the icon directly on the button
        skipBackButton.Paint += (sender, e) =>
        {
            Image icon = Image.FromFile(Path.Combine(currentDirectory, "general", "SkipBackground.png"));
            int iconSize = 50;
            int iconX = (skipBackButton.Width - iconSize) / 2;
            int iconY = (skipBackButton.Height - iconSize) / 2;
            e.Graphics.DrawImage(icon, iconX, iconY, iconSize, iconSize);
        };

        skipBackButton.MouseEnter += (sender, e) =>
        {
            skipBackButton.BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonSelected.png"));
        };
        skipBackButton.MouseLeave += (sender, e) =>
        {
            skipBackButton.BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png"));
        };
        skipBackButton.Click += (sender, e) =>
        {
            SkipTime(mediaPlayer, ref currentSegment, segments, - 10000);
        };

        // Create a button for Pause/Play
        Button pausePlayButton = new Button
        {
            Width = 80,
            Height = 80,
            BackgroundImage = mediaPlayer.IsPlaying
                ? Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png")) // Start with unselected background if playing
                : Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png")),
            BackgroundImageLayout = ImageLayout.Stretch,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            BackColor = Color.Transparent, // Transparent background
            FlatAppearance = 
            {
                BorderSize = 0,
                MouseDownBackColor = Color.Transparent,
                MouseOverBackColor = Color.Transparent
            }
        };

        // Draw the icon directly on the button
        pausePlayButton.Paint += (sender, e) =>
        {
            string iconPath = mediaPlayer.IsPlaying
                ? Path.Combine(currentDirectory, "general", "Pause.png") // Show pause icon if playing
                : Path.Combine(currentDirectory, "general", "Play.png"); // Show play icon if paused
            Image icon = Image.FromFile(iconPath);
            int iconSize = 50;
            int iconX = (pausePlayButton.Width - iconSize) / 2;
            int iconY = (pausePlayButton.Height - iconSize) / 2;
            e.Graphics.DrawImage(icon, iconX, iconY, iconSize, iconSize);
        };

        // Handle hover and click events
        pausePlayButton.MouseEnter += (sender, e) =>
        {
            pausePlayButton.BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonSelected.png"));
        };
        pausePlayButton.MouseLeave += (sender, e) =>
        {
            pausePlayButton.BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png"));
        };

        pausePlayButton.Click += (sender, e) =>
        {
            if (mediaPlayer.IsPlaying)
            {
                mediaPlayer.Pause();
            }
            else
            {
                mediaPlayer.Play();
            }

            pausePlayButton.BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png"));
            pausePlayButton.Invalidate();
        };

        Button skipForwardButton = new Button
        {
            Width = 80,
            Height = 80,
            BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png")),
            BackgroundImageLayout = ImageLayout.Stretch,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            BackColor = Color.Transparent,
            FlatAppearance = 
            {
                BorderSize = 0,
                MouseDownBackColor = Color.Transparent,
                MouseOverBackColor = Color.Transparent
            }
        };

        skipForwardButton.Paint += (sender, e) =>
        {
            Image icon = Image.FromFile(Path.Combine(currentDirectory, "general", "SkipForward.png"));
            int iconSize = 50;
            int iconX = (skipForwardButton.Width - iconSize) / 2;
            int iconY = (skipForwardButton.Height - iconSize) / 2;
            e.Graphics.DrawImage(icon, iconX, iconY, iconSize, iconSize);
        };

        skipForwardButton.MouseEnter += (sender, e) =>
        {
            skipForwardButton.BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonSelected.png"));
        };
        skipForwardButton.MouseLeave += (sender, e) =>
        {
            skipForwardButton.BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "ButtonUnselected.png"));
        };
        skipForwardButton.Click += (sender, e) =>
        {
            SkipTime(mediaPlayer, ref currentSegment, segments, 10000);
        };

        FlowLayoutPanel buttonLayout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Anchor = AnchorStyles.None
        };

        // Center the FlowLayoutPanel within the buttonPanel
        buttonLayout.SizeChanged += (sender, e) =>
        {
            buttonLayout.Location = new Point(
                (buttonPanel.ClientSize.Width - buttonLayout.Width) / 2,
                (buttonPanel.ClientSize.Height - buttonLayout.Height) / 2
            );
        };

        buttonLayout.Controls.Add(skipBackButton);
        buttonLayout.Controls.Add(pausePlayButton);
        buttonLayout.Controls.Add(skipForwardButton);

        buttonPanel.Controls.Add(buttonLayout);
        keyPressForm.Controls.Add(buttonPanel);

        keyPressForm.KeyPreview = true;
        keyPressForm.KeyDown += (sender, e) =>
        {
            HandleKeyPress(e.KeyCode, mediaPlayer, infoJsonFile, saveFilePath, currentSegment, segments);
        };

        keyPressForm.FormClosed += (sender, e) =>
        {
            mediaPlayer?.Dispose();

            foreach (Form form in Application.OpenForms)
            {
                form.Close();
            }

            Application.Exit();
        };

        if (keyPressThread == null || !keyPressThread.IsAlive)
        {
            keyPressThread = new System.Threading.Thread(() =>
            {
                Application.Run(keyPressForm);
            });
            keyPressThread.SetApartmentState(System.Threading.ApartmentState.STA);
            keyPressThread.Start();
        }
    }


    private static void HandleKeyPress(Keys key, MediaPlayer mediaPlayer, string infoJsonFile, string saveFilePath, Segment currentSegment, Dictionary<string, Segment> segments)
    {
        switch (key)
        {
            case Keys.Space:
                if (mediaPlayer.IsPlaying)
                {
                    mediaPlayer.Pause();
                    Console.WriteLine("Paused. Press Spacebar to resume.");
                }
                else
                {
                    mediaPlayer.Play();
                    Console.WriteLine("Resumed. Press Spacebar to pause.");
                }
                break;
            /*
            case Keys.L:
                Console.WriteLine("Switching audio track...");
                AudioManager.ListAndSelectAudioTrack(mediaPlayer, mediaPlayer.Media);
                break;
            
            case Keys.S:
                Console.WriteLine("Switching subtitles...");
                SubtitleManager.ListAndSelectSubtitleTrack(mediaPlayer, mediaPlayer.Media);
                break;

            case Keys.C:
                Console.WriteLine("Checking preconditions...");
                PreconditionChecker.CheckPreconditions(infoJsonFile, saveFilePath);
                break;
            */
            case Keys.Right:
                SkipTime(mediaPlayer, ref currentSegment, segments, 10000);
                break;

            case Keys.Left:
                SkipTime(mediaPlayer, ref currentSegment, segments, - 10000);
                break;

            default:
                // No action for other keys
                break;
        }
    }

    private static DateTime lastSkipTime = DateTime.MinValue;
    private static readonly TimeSpan skipCooldown = TimeSpan.FromMilliseconds(500);

    private static void SkipTime(MediaPlayer mediaPlayer, ref Segment currentSegment, Dictionary<string, Segment> segments, int offsetMs)
    {

        if (DateTime.Now - lastSkipTime < skipCooldown)
        {
            Console.WriteLine("Skip action is on cooldown. Please wait.");
            return;
        }

        lastSkipTime = DateTime.Now;
        long currentTime = mediaPlayer.Time;
        long newTime = mediaPlayer.Time + offsetMs;

        if (currentSegment.Choices != null && currentSegment.Choices.Count > 0)
        {
            if (currentTime >= currentSegment.ChoiceDisplayTimeMs && currentTime <= currentSegment.HideChoiceTimeMs)
            {
                Console.WriteLine("Cannot skip while in a choice point.");
                return;
            }
        }

        if (currentSegment.fakechoices != null && currentSegment.fakechoices.Count > 0)
        {
            if (currentTime >= currentSegment.fakeChoiceDisplayTimeMs && currentTime <= currentSegment.fakeHideChoiceTimeMs)
            {
                Console.WriteLine("Cannot skip while in a choice point.");
                return;
            }
        }

        if (newTime < 0)
        {
            newTime = 0;
        }

        foreach (var segment in segments.Values)
        {
            if (newTime >= segment.StartTimeMs && newTime <= segment.EndTimeMs)
            {
                currentSegment = segment;
                break;
            }
        }

        if (newTime < currentSegment.StartTimeMs)
        {
            newTime = currentSegment.StartTimeMs;
        }
        else if (newTime > currentSegment.EndTimeMs)
        {
            newTime = currentSegment.EndTimeMs;
        }

        if (currentSegment.Choices != null && currentSegment.Choices.Count > 0)
        {
            if (newTime >= currentSegment.ChoiceDisplayTimeMs && newTime <= currentSegment.HideChoiceTimeMs)
            {
                Console.WriteLine("Cannot skip into a choice point.");
                return;
            }
        }

        if (currentSegment.fakechoices != null && currentSegment.fakechoices.Count > 0)
        {
            if (newTime >= currentSegment.fakeChoiceDisplayTimeMs && newTime <= currentSegment.fakeHideChoiceTimeMs)
            {
                Console.WriteLine("Cannot skip into a fake choice point.");
                return;
            }
        }

        mediaPlayer.Time = newTime;
        Console.WriteLine($"Skipped to {mediaPlayer.Time} ms in segment {currentSegment.Id}.");
    }
}
