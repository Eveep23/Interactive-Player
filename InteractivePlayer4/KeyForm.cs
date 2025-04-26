using System;
using System.Windows.Forms;
using LibVLCSharp.Shared;

public static class KeyForm
{
    private static Form keyPressForm;

    private static System.Threading.Thread keyPressThread;

    public static void InitializeKeyPressWindow(MediaPlayer mediaPlayer, string infoJsonFile, string saveFilePath, Segment currentSegment)
    {
        if (keyPressForm != null && !keyPressForm.IsDisposed)
        {
            return;
        }

        keyPressForm = new Form
        {
            Text = "Key Listener",
            Width = 300,
            Height = 200,
            ShowInTaskbar = true,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            Location = new System.Drawing.Point(100, 100)
        };

        keyPressForm.KeyPreview = true;
        keyPressForm.KeyDown += (sender, e) =>
        {
            HandleKeyPress(e.KeyCode, mediaPlayer, infoJsonFile, saveFilePath, currentSegment);
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


    private static void HandleKeyPress(Keys key, MediaPlayer mediaPlayer, string infoJsonFile, string saveFilePath, Segment currentSegment)
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
            */
            case Keys.C:
                Console.WriteLine("Checking preconditions...");
                PreconditionChecker.CheckPreconditions(infoJsonFile, saveFilePath);
                break;
            
            case Keys.Right:
                SkipTime(mediaPlayer, currentSegment, 10000);
                break;

            case Keys.Left:
                SkipTime(mediaPlayer, currentSegment, -10000);
                break;

            default:
                // No action for other keys
                break;
        }
    }

    private static void SkipTime(MediaPlayer mediaPlayer, Segment currentSegment, int offsetMs)
    {
        long newTime = mediaPlayer.Time + offsetMs;

        // Make sure the time doesn't go out of the segment bounds
        if (newTime < currentSegment.StartTimeMs)
        {
            newTime = currentSegment.StartTimeMs;
        }
        else if (newTime > currentSegment.EndTimeMs)
        {
            newTime = currentSegment.EndTimeMs;
        }

        // Make sure the time doesn't go into a choice point
        if (currentSegment.Choices != null && currentSegment.Choices.Count > 0)
        {
            if (newTime >= currentSegment.ChoiceDisplayTimeMs && newTime <= currentSegment.HideChoiceTimeMs)
            {
                Console.WriteLine("Cannot skip into a choice point.");
                return;
            }
        }

        mediaPlayer.Time = newTime;
        Console.WriteLine($"Skipped to {mediaPlayer.Time} ms.");
    }
}
