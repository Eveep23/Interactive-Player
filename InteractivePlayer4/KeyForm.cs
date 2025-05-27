using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using static UIManager;

public static class KeyForm
{
    private static Form keyPressForm;

    private static System.Threading.Thread keyPressThread;

    private static Timer keepOnTopTimer;

    private static int lastVlcLeft = int.MinValue;
    private static int lastVlcTop = int.MinValue;
    private static int lastVlcWidth = int.MinValue;
    private static int lastVlcOffset = int.MinValue;

    private static IntPtr cachedVlcHandle = IntPtr.Zero;
    private static DateTime lastVlcHandleCheck = DateTime.MinValue;
    private static readonly TimeSpan vlcHandleCheckInterval = TimeSpan.FromSeconds(5);

    public static void InitializeKeyPressWindow(MediaPlayer mediaPlayer, string infoJsonFile, string saveFilePath, Segment currentSegment, Dictionary<string, Segment> segments)
    {
        if (keyPressForm != null && !keyPressForm.IsDisposed)
        {
            return;
        }

        string currentDirectory = Directory.GetCurrentDirectory();
        string logoPath = Path.Combine(currentDirectory, "general", "Interactive_player_logo.png");
        string buttonUnselectedPath = Path.Combine(currentDirectory, "general", "ButtonUnselected.png");
        string buttonSelectedPath = Path.Combine(currentDirectory, "general", "ButtonSelected.png");

        keyPressForm = new Form
        {
            Text = "Interactive Player",
            Width = 1000,
            Height = 120,
            ShowInTaskbar = false,
            TopMost = true,
            FormBorderStyle = FormBorderStyle.None,
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            Location = new System.Drawing.Point(100, 100),
            BackColor = ColorTranslator.FromHtml("#141414"),
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        };

        PictureBox logoPictureBox = new PictureBox
        {
            Image = Image.FromFile(logoPath),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Width = 380,
            Height = 100
        };

        logoPictureBox.Location = new Point(
            (keyPressForm.Width - logoPictureBox.Width) / 2,
            (keyPressForm.Height - logoPictureBox.Height) / 2
        );
        logoPictureBox.Anchor = AnchorStyles.None;
        keyPressForm.Controls.Add(logoPictureBox);

        int buttonPanelLeftMargin = 20;
        Panel buttonPanel = new Panel
        {
            Width = 260,
            Height = 100,
            BackColor = Color.Transparent,
            Location = new Point(buttonPanelLeftMargin, (keyPressForm.Height - 100) / 2),
            Anchor = AnchorStyles.Left
        };

        Button skipBackButton = new Button
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
            SkipTime(mediaPlayer, ref currentSegment, segments, -10000);
        };

        Button pausePlayButton = new Button
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
        pausePlayButton.Paint += (sender, e) =>
        {
            string iconPath = mediaPlayer.IsPlaying
                ? Path.Combine(currentDirectory, "general", "Pause.png")
                : Path.Combine(currentDirectory, "general", "Play.png");
            Image icon = Image.FromFile(iconPath);
            int iconSize = 50;
            int iconX = (pausePlayButton.Width - iconSize) / 2;
            int iconY = (pausePlayButton.Height - iconSize) / 2;
            e.Graphics.DrawImage(icon, iconX, iconY, iconSize, iconSize);
        };
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
            long currentTime = mediaPlayer.Time;
            bool inRealChoice = currentSegment.Choices != null && currentSegment.Choices.Count > 0
                && currentTime >= currentSegment.ChoiceDisplayTimeMs && currentTime <= currentSegment.HideChoiceTimeMs;
            bool inFakeChoice = currentSegment.fakechoices != null && currentSegment.fakechoices.Count > 0
                && currentTime >= currentSegment.fakeChoiceDisplayTimeMs && currentTime <= currentSegment.fakeHideChoiceTimeMs;
            if (inRealChoice || inFakeChoice)
            {
                Console.WriteLine("Cannot pause or play during a choice point.");
                return;
            }
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

        int buttonSpacing = 10;
        pausePlayButton.Location = new Point(0, (buttonPanel.Height - pausePlayButton.Height) / 2);
        skipBackButton.Location = new Point(pausePlayButton.Right + buttonSpacing, (buttonPanel.Height - skipBackButton.Height) / 2);
        skipForwardButton.Location = new Point(skipBackButton.Right + buttonSpacing, (buttonPanel.Height - skipForwardButton.Height) / 2);

        buttonPanel.Controls.Add(pausePlayButton);
        buttonPanel.Controls.Add(skipBackButton);
        buttonPanel.Controls.Add(skipForwardButton);

        keyPressForm.Controls.Add(buttonPanel);

        string exitButtonIconPath = Path.Combine(currentDirectory, "general", "Exit.png");
        Button exitButton = new Button
        {
            Width = 80,
            Height = 80,
            BackgroundImage = Image.FromFile(buttonUnselectedPath),
            BackgroundImageLayout = ImageLayout.Stretch,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        exitButton.FlatAppearance.BorderSize = 0;
        exitButton.FlatAppearance.MouseDownBackColor = Color.Transparent;
        exitButton.FlatAppearance.MouseOverBackColor = Color.Transparent;

        exitButton.Paint += (sender, e) =>
        {
            Image icon = Image.FromFile(exitButtonIconPath);
            int iconSize = 50;
            int iconX = (exitButton.Width - iconSize) / 2;
            int iconY = (exitButton.Height - iconSize) / 2;
            e.Graphics.DrawImage(icon, iconX, iconY, iconSize, iconSize);
        };
        exitButton.MouseEnter += (sender, e) =>
        {
            exitButton.BackgroundImage = Image.FromFile(buttonSelectedPath);
        };
        exitButton.MouseLeave += (sender, e) =>
        {
            exitButton.BackgroundImage = Image.FromFile(buttonUnselectedPath);
        };
        exitButton.Click += (s, e) =>
        {
            mediaPlayer?.Dispose();

            foreach (Form form in Application.OpenForms)
            {
                form.Close();
            }

            Application.Exit();
        };

        exitButton.Location = new Point(
            keyPressForm.Width - exitButton.Width - 20,
            (keyPressForm.Height - exitButton.Height) / 2
        );

        string enterFullPath = Path.Combine(currentDirectory, "general", "EnterFull.png");
        string exitFullPath = Path.Combine(currentDirectory, "general", "ExitFull.png");
        string fullButtonUnselectedPath = buttonUnselectedPath;
        string fullButtonSelectedPath = buttonSelectedPath;

        Button fullScreenButton = new Button
        {
            Width = 80,
            Height = 80,
            BackgroundImage = Image.FromFile(fullButtonUnselectedPath),
            BackgroundImageLayout = ImageLayout.Stretch,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        fullScreenButton.FlatAppearance.BorderSize = 0;
        fullScreenButton.FlatAppearance.MouseDownBackColor = Color.Transparent;
        fullScreenButton.FlatAppearance.MouseOverBackColor = Color.Transparent;

        // Track fullscreen state
        bool isFullScreen = false;

        fullScreenButton.Paint += (sender, e) =>
        {
            string iconPath = isFullScreen ? exitFullPath : enterFullPath;
            Image icon = Image.FromFile(iconPath);
            int iconSize = 50;
            int iconX = (fullScreenButton.Width - iconSize) / 2;
            int iconY = (fullScreenButton.Height - iconSize) / 2;
            e.Graphics.DrawImage(icon, iconX, iconY, iconSize, iconSize);
        };
        fullScreenButton.MouseEnter += (sender, e) =>
        {
            fullScreenButton.BackgroundImage = Image.FromFile(fullButtonSelectedPath);
        };
        fullScreenButton.MouseLeave += (sender, e) =>
        {
            fullScreenButton.BackgroundImage = Image.FromFile(fullButtonUnselectedPath);
        };
        fullScreenButton.Click += async (s, e) =>
        {
            mediaPlayer.Fullscreen = !mediaPlayer.Fullscreen;
            isFullScreen = mediaPlayer.Fullscreen;
            fullScreenButton.Invalidate();

            await Task.Delay(300);
            AlignWithVLCWindow();
            keyPressForm.BringToFront();
            ForceTopMost(keyPressForm);
        };

        fullScreenButton.Location = new Point(
            keyPressForm.Width - exitButton.Width - fullScreenButton.Width - 40,
            (keyPressForm.Height - fullScreenButton.Height) / 2
        );

        keyPressForm.SizeChanged += (s, e) =>
        {
            fullScreenButton.Location = new Point(
                keyPressForm.Width - exitButton.Width - fullScreenButton.Width - 40,
                (keyPressForm.Height - fullScreenButton.Height) / 2
            );
            exitButton.Location = new Point(
                keyPressForm.Width - exitButton.Width - 20,
                (keyPressForm.Height - exitButton.Height) / 2
            );
        };
        keyPressForm.Controls.Add(fullScreenButton);

        keyPressForm.SizeChanged += (s, e) =>
        {
            exitButton.Location = new Point(
                keyPressForm.Width - exitButton.Width - 20,
                (keyPressForm.Height - exitButton.Height) / 2
            );
        };
        keyPressForm.Controls.Add(exitButton);

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

        keyPressForm.Opacity = 0.004;

        AttachOpacityHandlers(keyPressForm, keyPressForm);

        if (keyPressThread == null || !keyPressThread.IsAlive)
        {
            keyPressThread = new System.Threading.Thread(() =>
            {
                // Show the form before aligning
                keyPressForm.Shown += async (s, e) =>
                {
                    await Task.Delay(750);
                    AlignWithVLCWindow();
                    keyPressForm.BringToFront();
                    ForceTopMost(keyPressForm);

                    StartKeepOnTopTimer();
                };

                keyPressForm.FormClosed += (s, e) =>
                {
                    StopKeepOnTopTimer();
                };

                Application.Run(keyPressForm);
            });
            keyPressThread.SetApartmentState(System.Threading.ApartmentState.STA);
            keyPressThread.Start();
        }
    }

    private static Timer fadeTimer;
    private static double targetOpacity = 0.004;
    private static double fadeStep = 0.08;
    private static bool fadingIn = false;

    private static void AttachOpacityHandlers(Control control, Form form)
    {
        control.MouseEnter += (s, e) =>
        {
            keyPressForm.BringToFront();
            ForceTopMost(keyPressForm);
            StartFade(form, true);
        };
        control.MouseLeave += (s, e) =>
        {
            if (!form.Bounds.Contains(Cursor.Position))
                StartFade(form, false);
        };
        foreach (Control child in control.Controls)
        {
            AttachOpacityHandlers(child, form);
        }
    }

    private static void StartFade(Form form, bool fadeIn)
    {
        targetOpacity = fadeIn ? 0.93 : 0.004;
        fadingIn = fadeIn;

        if (fadeTimer == null)
        {
            fadeTimer = new Timer();
            fadeTimer.Interval = 15;
            fadeTimer.Tick += (s, e) =>
            {
                double current = form.Opacity;
                if (fadingIn)
                {
                    if (current < targetOpacity)
                    {
                        form.Opacity = Math.Min(targetOpacity, current + fadeStep);
                    }
                    else
                    {
                        form.Opacity = targetOpacity;
                        fadeTimer.Stop();
                    }
                }
                else
                {
                    if (current > targetOpacity)
                    {
                        form.Opacity = Math.Max(targetOpacity, current - fadeStep);
                    }
                    else
                    {
                        form.Opacity = targetOpacity;
                        fadeTimer.Stop();
                    }
                }
            };
        }
        fadeTimer.Stop();
        fadeTimer.Start();
    }

    private static void AlignWithVLCWindow()
    {
        if (cachedVlcHandle == IntPtr.Zero || (DateTime.Now - lastVlcHandleCheck) > vlcHandleCheckInterval)
        {
            cachedVlcHandle = FindWindow(null, "VLC (Direct3D11 output)");
            lastVlcHandleCheck = DateTime.Now;
        }

        if (cachedVlcHandle != IntPtr.Zero)
        {
            UIManager.RECT rect;
            if (GetWindowRect(cachedVlcHandle, out rect))
            {
                int vlcWidth = rect.Right - rect.Left;
                int vlcTop = rect.Top;
                int offset = 0;

                // Check if VLC is maximized (full-screen)
                WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                if (GetWindowPlacement(cachedVlcHandle, ref placement))
                {
                    // 3 = Maximized, 1 = Normal
                    if (placement.showCmd != 3)
                    {
                        offset = 30;
                    }
                }

                // Only update if position/size/offset changed
                if (vlcWidth != lastVlcWidth || rect.Left != lastVlcLeft || vlcTop != lastVlcTop || offset != lastVlcOffset)
                {
                    lastVlcWidth = vlcWidth;
                    lastVlcLeft = rect.Left;
                    lastVlcTop = vlcTop;
                    lastVlcOffset = offset;

                    if (keyPressForm != null && !keyPressForm.IsDisposed)
                    {
                        keyPressForm.Invoke(new Action(() =>
                        {
                            keyPressForm.Width = vlcWidth;
                            keyPressForm.Left = rect.Left;
                            keyPressForm.Top = vlcTop + offset;
                        }));
                    }
                }
            }
        }
    }

    private static void HandleKeyPress(Keys key, MediaPlayer mediaPlayer, string infoJsonFile, string saveFilePath, Segment currentSegment, Dictionary<string, Segment> segments)
    {
        switch (key)
        {
            case Keys.Space:
                long currentTime = mediaPlayer.Time;
                bool inRealChoice = currentSegment.Choices != null && currentSegment.Choices.Count > 0
                    && currentTime >= currentSegment.ChoiceDisplayTimeMs && currentTime <= currentSegment.HideChoiceTimeMs;
                bool inFakeChoice = currentSegment.fakechoices != null && currentSegment.fakechoices.Count > 0
                    && currentTime >= currentSegment.fakeChoiceDisplayTimeMs && currentTime <= currentSegment.fakeHideChoiceTimeMs;
                if (inRealChoice || inFakeChoice)
                {
                    Console.WriteLine("Cannot pause or play during a choice point.");
                    break;
                }
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
    private static readonly TimeSpan skipCooldown = TimeSpan.FromMilliseconds(400);

    private static void SkipTime(MediaPlayer mediaPlayer, ref Segment currentSegment, Dictionary<string, Segment> segments, int offsetMs)
    {
        if (DateTime.Now - lastSkipTime < skipCooldown)
        {
            Console.WriteLine("Skip action is on cooldown. Please wait.");
            return;
        }

        lastSkipTime = DateTime.Now;
        long currentTime = mediaPlayer.Time;
        long newTime = currentTime + offsetMs;

        if (offsetMs < 0 && newTime < currentSegment.StartTimeMs)
        {
            Console.WriteLine("Cannot skip before the start of the current segment.");
            return;
        }

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

    private static void StartKeepOnTopTimer()
    {
        if (keepOnTopTimer == null)
        {
            keepOnTopTimer = new System.Windows.Forms.Timer();
            keepOnTopTimer.Interval = 5000;
            keepOnTopTimer.Tick += (s, e) =>
            {
                if (keyPressForm != null && !keyPressForm.IsDisposed && keyPressForm.Visible)
                {
                    AlignWithVLCWindow();
                    keyPressForm.BringToFront();
                    ForceTopMost(keyPressForm);
                }
            };
        }
        keepOnTopTimer.Start();
    }

    private static void StopKeepOnTopTimer()
    {
        if (keepOnTopTimer != null)
        {
            keepOnTopTimer.Stop();
            keepOnTopTimer.Dispose();
            keepOnTopTimer = null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public System.Drawing.Point ptMinPosition;
        public System.Drawing.Point ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static void ForceTopMost(Form form)
    {
        SetWindowPos(form.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }
}
