using System;
using System.IO;
using System.Linq;
using LibVLCSharp.Shared;
using Newtonsoft.Json;

public static class SubtitleManager
{
    public static void ListAndSelectSubtitleTrack(MediaPlayer mediaPlayer, Media media)
    {
        if (media.Tracks == null || media.Tracks.Length == 0)
        {
            Console.WriteLine("No tracks found in the media file.");
            return;
        }

        // List all subtitle tracks
        int subtitleTrackIndex = 1; // Start from 1
        var subtitleTracks = media.Tracks
            .Where(track => track is MediaTrack subtitleTrack && subtitleTrack.TrackType == TrackType.Text)
            .Select(track => (MediaTrack)track)
            .ToList();

        if (!subtitleTracks.Any())
        {
            Console.WriteLine("No subtitle tracks available.");
            return;
        }

        Console.WriteLine("Available Subtitle Tracks:");
        foreach (var track in subtitleTracks)
        {
            Console.WriteLine($"[{subtitleTrackIndex}] Subtitle Track: {track.Description} - Language: {track.Language}");
            subtitleTrackIndex++;
        }

        Console.WriteLine("[0] Disable Subtitles");

        // Select a subtitle track
        Console.WriteLine("Enter the number of the subtitle track you want to switch to (or 0 to disable):");
        if (int.TryParse(Console.ReadLine(), out int selectedTrack) && selectedTrack >= 0 && selectedTrack <= subtitleTracks.Count)
        {
            if (selectedTrack == 0)
            {
                mediaPlayer.SetSpu(-1); // Disable subtitles
                Console.WriteLine("Subtitles disabled.");
            }
            else
            {
                int trackId = subtitleTracks[selectedTrack - 1].Id; // Adjust for 1-based index

                if (mediaPlayer.SetSpu(trackId))
                {
                    Console.WriteLine($"Subtitle track {selectedTrack} successfully selected.");
                }
                else
                {
                    Console.WriteLine($"Failed to select subtitle track {selectedTrack}. Default track will be used.");
                }
            }
        }
        else
        {
            Console.WriteLine("Invalid selection.");
        }
    }

    public static void LoadSubtitleTrackFromSaveFile(MediaPlayer mediaPlayer, Media media, string saveFilePath)
    {
        if (!File.Exists(saveFilePath))
        {
            Console.WriteLine("Save file not found. Disabling subtitles.");
            mediaPlayer.SetSpu(-1); // Disable subtitles
            return;
        }

        var saveData = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(saveFilePath));
        if (saveData == null || string.IsNullOrEmpty(saveData.SubtitleLanguage))
        {
            Console.WriteLine("Invalid save data. Disabling subtitles.");
            mediaPlayer.SetSpu(-1); // Disable subtitles
            return;
        }

        var subtitleTracks = media.Tracks
            .Where(track => track is MediaTrack subtitleTrack && subtitleTrack.TrackType == TrackType.Text)
            .Select(track => (MediaTrack)track)
            .ToList();

        if (saveData.SubtitleLanguage.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            mediaPlayer.SetSpu(-1); // Disable subtitles
            Console.WriteLine("Subtitles disabled.");
            return;
        }

        string[] trackInfo = saveData.SubtitleLanguage.Split(new[] { '-' }, 2);
        string trackName = trackInfo.Length > 1 ? trackInfo[0].Trim() : null;
        string trackLanguage = trackInfo.Length > 1 ? trackInfo[1].Trim() : trackInfo[0].Trim();

        if (!trackLanguage.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            trackLanguage = trackLanguage.Substring(0, 3);
        }

        var selectedTrack = subtitleTracks.FirstOrDefault(track =>
            track.Language != null && track.Language.Equals(trackLanguage, StringComparison.OrdinalIgnoreCase) &&
            (trackName == null || (track.Description != null && track.Description.Equals(trackName, StringComparison.OrdinalIgnoreCase))));

        if (!selectedTrack.Equals(default(MediaTrack)))
        {
            if (mediaPlayer.SetSpu(selectedTrack.Id))
            {
                Console.WriteLine($"Subtitle track '{saveData.SubtitleLanguage}' successfully selected.");
            }
            else
            {
                Console.WriteLine($"Failed to select subtitle track '{saveData.SubtitleLanguage}'. Disabling subtitles.");
                mediaPlayer.SetSpu(-1); // Disable subtitles
            }
        }
        else
        {
            Console.WriteLine($"Subtitle track '{saveData.SubtitleLanguage}' not found. Disabling subtitles.");
            mediaPlayer.SetSpu(-1); // Disable subtitles
        }
    }
}
