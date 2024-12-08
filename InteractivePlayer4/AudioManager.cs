using System;
using System.Linq;
using LibVLCSharp.Shared;

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
        int audioTrackIndex = 0;
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
        if (int.TryParse(Console.ReadLine(), out int selectedTrack) && selectedTrack >= 0 && selectedTrack < audioTracks.Count)
        {
            int trackId = audioTracks[selectedTrack].Id;

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
}