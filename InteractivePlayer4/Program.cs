using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibVLCSharp.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static void Main(string[] args)
    {
        Core.Initialize();

        // Display movie selection menu
        string movieFolder = Utilities.ShowMovieSelectionMenu();
        if (movieFolder == null)
        {
            Console.WriteLine("No Interactive selected. Exiting.");
            return;
        }

        // Set paths for JSON files and save file
        string videoFile = GetVideoFilePath(movieFolder);
        string mainJsonFile = Directory.GetFiles(movieFolder, "*.json").FirstOrDefault(f => !f.ToLower().Contains("info") && !f.ToLower().Contains("direct"));
        string infoJsonFile = Directory.GetFiles(movieFolder, "*.json").FirstOrDefault(f => f.ToLower().Contains("info"));
        string saveFilePath = Path.Combine(movieFolder, "save.json");

        if (videoFile == null || mainJsonFile == null || infoJsonFile == null)
        {
            Console.WriteLine("Error: Required files not found in the selected Interactive folder.");
            return;
        }

        // Load save file if it exists
        string currentSegment = SaveManager.LoadSaveFile(saveFilePath);
        bool isFirstLoad = currentSegment == null;
        if (isFirstLoad)
        {
            File.Delete(saveFilePath); // Restart
        }

        string initialSegment = null;

        // Get video duration
        long videoDuration = GetVideoDuration(videoFile);

        // Parse JSON files
        Dictionary<string, Segment> segments = JsonParser.ParseSegments(mainJsonFile, ref initialSegment, videoDuration);
        var (momentsBySegment, videoId, globalState, persistentState, segmentGroups, segmentStates) = JsonParser.ParseMoments(infoJsonFile);

        // Handle missing segments or moments
        if (segments == null || momentsBySegment == null)
        {
            Console.WriteLine("Error: Failed to parse JSON.");
            return;
        }

        // Set starting segment if not continuing
        if (currentSegment == null)
        {
            currentSegment = initialSegment ?? segments.Values.FirstOrDefault(s => s.IsStartingSegment)?.Id ?? segments.Keys.First();
        }

        // Merge moments into segments
        JsonParser.MergeMomentsIntoSegments(segments, momentsBySegment);

        // Set starting segment if not continuing
        if (currentSegment == null)
        {
            currentSegment = segments.Values.FirstOrDefault(s => s.IsStartingSegment)?.Id ?? segments.Keys.First();
        }

        // Load states from save file if it exists
        if (!isFirstLoad)
        {
            var saveData = SaveManager.LoadSaveData(saveFilePath);
            globalState = saveData.GlobalState;
            persistentState = saveData.PersistentState;
        }

        // Initialize LibVLC with subtitle options
        var libVLC = new LibVLC(
            "--freetype-font=Consolas",
            "--freetype-bold",
            "--freetype-outline-thickness=3"
        );
        var media = new Media(libVLC, new Uri(Path.GetFullPath(videoFile)));
        var mediaPlayer = new MediaPlayer(media);

        try
        {
            // Start playing the video
            mediaPlayer.Play();

            while (!string.IsNullOrEmpty(currentSegment))
            {
                if (!segments.TryGetValue(currentSegment, out Segment segment))
                {
                    Console.WriteLine($"Error: Segment {currentSegment} not found.");
                    break;
                }

                Console.WriteLine($"Now playing segment: {segment.Id}");
                currentSegment = JsonParser.HandleSegment(mediaPlayer, segment, segments, movieFolder, videoId, ref globalState, ref persistentState, infoJsonFile, saveFilePath, segmentGroups, segmentStates, isFirstLoad);

                SaveManager.SaveProgress(saveFilePath, currentSegment, globalState, persistentState);

                // After the first segment, set isFirstLoad to false
                isFirstLoad = false;
            }

            Console.WriteLine("Interactive finished.");
        }
        finally
        {
            mediaPlayer.Dispose();
            libVLC.Dispose();
        }
    }

    private static string GetVideoFilePath(string movieFolder)
    {
        // Check for video files directly
        string videoFile = Directory.GetFiles(movieFolder, "*.mkv")
            .Concat(Directory.GetFiles(movieFolder, "*.mp4"))
            .FirstOrDefault();

        // If no video file is found, check for direct.json
        if (videoFile == null)
        {
            string directJsonFile = Directory.GetFiles(movieFolder, "direct.json").FirstOrDefault();
            if (directJsonFile != null)
            {
                var json = JObject.Parse(File.ReadAllText(directJsonFile));
                videoFile = json["Directory"]?.ToString();
            }
        }

        return videoFile;
    }

    private static long GetVideoDuration(string videoFile)
    {
        long duration = 0;

        using (var libVLC = new LibVLC())
        {
            using (var media = new Media(libVLC, new Uri(videoFile)))
            {
                media.Parse(MediaParseOptions.ParseLocal);

                duration = media.Duration;
            }
        }

        return duration;
    }
}
