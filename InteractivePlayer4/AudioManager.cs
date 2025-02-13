using System;
using System.IO;
using System.Linq;
using LibVLCSharp.Shared;
using Newtonsoft.Json;

public static class AudioManager
{
    public static void ListAndSelectAudioTrack(MediaPlayer mediaPlayer, Media media)
    {
        if (media.Tracks == null || media.Tracks.Length == 0)
        {
            Console.WriteLine("No tracks found in the media file.");
            return;
        }

        // List all audio tracks
        int audioTrackIndex = 1; // Start from 1
        var audioTracks = media.Tracks
            .Where(track => track is MediaTrack audioTrack && audioTrack.TrackType == TrackType.Audio)
            .Select(track => (MediaTrack)track)
            .ToList();

        if (!audioTracks.Any())
        {
            Console.WriteLine("No audio tracks available.");
            return;
        }

        Console.WriteLine("Available Audio Tracks:");
        foreach (var track in audioTracks)
        {
            Console.WriteLine($"[{audioTrackIndex}] Audio Track: {track.Description} - Language: {track.Language}");
            audioTrackIndex++;
        }

        // Select an audio track
        Console.WriteLine("Enter the number of the audio track you want to switch to:");
        if (int.TryParse(Console.ReadLine(), out int selectedTrack) && selectedTrack >= 1 && selectedTrack <= audioTracks.Count)
        {
            int trackId = audioTracks[selectedTrack - 1].Id; // Adjust for 1-based index

            if (mediaPlayer.SetAudioTrack(trackId))
            {
                Console.WriteLine($"Audio track {selectedTrack} successfully selected.");
            }
            else
            {
                Console.WriteLine($"Failed to select audio track {selectedTrack}. Default track will be used.");
            }
        }
        else
        {
            Console.WriteLine("Invalid selection.");
        }
    }

    public static void LoadAudioTrackFromSaveFile(MediaPlayer mediaPlayer, Media media, string saveFilePath)
    {
        if (!File.Exists(saveFilePath))
        {
            Console.WriteLine("Save file not found. Defaulting to track 1.");
            SetDefaultAudioTrack(mediaPlayer, media);
            return;
        }

        var saveData = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(saveFilePath));
        if (saveData == null || string.IsNullOrEmpty(saveData.AudioLanguage))
        {
            Console.WriteLine("Invalid save data. Defaulting to track 1.");
            SetDefaultAudioTrack(mediaPlayer, media);
            return;
        }

        var audioTracks = media.Tracks
            .Where(track => track is MediaTrack audioTrack && audioTrack.TrackType == TrackType.Audio)
            .Select(track => (MediaTrack)track)
            .ToList();

        string[] trackInfo = saveData.AudioLanguage.Split(new[] { '-' }, 2);
        string trackName = trackInfo.Length > 1 ? trackInfo[0].Trim() : null;
        string trackLanguage = trackInfo.Length > 1 ? trackInfo[1].Trim() : trackInfo[0].Trim();

        if (!trackLanguage.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            trackLanguage = trackLanguage.Substring(0, 3);
        }

        var selectedTrack = audioTracks.FirstOrDefault(track =>
            track.Language.Equals(trackLanguage, StringComparison.OrdinalIgnoreCase) &&
            (trackName == null || track.Description.Equals(trackName, StringComparison.OrdinalIgnoreCase)));

        if (!selectedTrack.Equals(default(MediaTrack)))
        {
            if (mediaPlayer.SetAudioTrack(selectedTrack.Id))
            {
                Console.WriteLine($"Audio track '{saveData.AudioLanguage}' successfully selected.");
            }
            else
            {
                Console.WriteLine($"Failed to select audio track '{saveData.AudioLanguage}'. Defaulting to track 1.");
                SetDefaultAudioTrack(mediaPlayer, media);
            }
        }
        else
        {
            Console.WriteLine($"Audio track '{saveData.AudioLanguage}' not found. Defaulting to track 1.");
            SetDefaultAudioTrack(mediaPlayer, media);
        }

        // Set audio output mode using equalizer
        var equalizer = new Equalizer();
        switch (saveData.AudioOutput)
        {
            case "Headphones":
                // Configure equalizer for Headphones (boost bass and treble)
                equalizer.SetPreamp(0.0f);
                equalizer.SetAmp(0, 3); // Boost low frequencies
                equalizer.SetAmp(1, 2);
                equalizer.SetAmp(2, 1);
                equalizer.SetAmp(3, 0); // Mid frequencies
                equalizer.SetAmp(4, 0);
                equalizer.SetAmp(5, 1);
                equalizer.SetAmp(6, 2);
                equalizer.SetAmp(7, 3); // Boost high frequencies
                Console.WriteLine("Audio output set to Headphones.");
                mediaPlayer.SetEqualizer(equalizer);
                break;
        }
    }

    private static void SetDefaultAudioTrack(MediaPlayer mediaPlayer, Media media)
    {
        var audioTracks = media.Tracks
            .Where(track => track is MediaTrack audioTrack && audioTrack.TrackType == TrackType.Audio)
            .Select(track => (MediaTrack)track)
            .ToList();

        if (audioTracks.Any())
        {
            mediaPlayer.SetAudioTrack(audioTracks[0].Id);
            Console.WriteLine("Default audio track 1 selected.");
        }
        else
        {
            Console.WriteLine("No audio tracks available to select.");
        }
    }
}
