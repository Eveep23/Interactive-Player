using LibVLCSharp.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DiscordRPC;

class Program
{
    public static DiscordRpcClient discordClient;

    private static LoadingForm loadingForm;

    static void Main(string[] args)
    {
        discordClient = new DiscordRpcClient("1374146925128847370");
        discordClient.Initialize();

        discordClient.SetPresence(new RichPresence()
        {
            Details = "Browsing Interactives",
            State = "Idle",
            Assets = new Assets()
            {
                LargeImageKey = "logo",
                LargeImageText = "Interactive Player"
            }
        });

        Core.Initialize();

        string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
        Settings settings = File.Exists(configFilePath)
            ? JsonConvert.DeserializeObject<Settings>(File.ReadAllText(configFilePath))
            : new Settings { EnableConsole = true };

        if (settings.EnableConsole)
        {
            AllocConsole();
        }
        else
        {
            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
            {
                FreeConsole();
            }
        }

        string movieFolder = null;

        if (args.Length > 0)
        {
            movieFolder = args[0].Trim('"');
            if (!Directory.Exists(movieFolder))
            {
                Console.WriteLine("The specified folder does not exist.");
                return;
            }
        }
        else
        {
            movieFolder = Utilities.ShowMovieSelectionMenu();
            if (movieFolder == null)
            {
                Console.WriteLine("No Interactive selected. Exiting.");
                return;
            }
        }

        discordClient.SetPresence(new RichPresence()
        {
            Details = "Playing an Interactive",
            State = Path.GetFileName(movieFolder),
            Assets = new Assets()
            {
                LargeImageKey = "logo",
                LargeImageText = "Interactive Player"
            }
        });

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

        loadingForm = null;
        var loadingThread = new Thread(() =>
        {
            loadingForm = new LoadingForm();
            Application.Run(loadingForm);
        });
        loadingThread.SetApartmentState(ApartmentState.STA);
        loadingThread.Start();

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
        JsonParser.MergeMomentsIntoSegments(segments, momentsBySegment, videoId);

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
            "--freetype-outline-thickness=3",
            "--no-stats",
            "--quiet",
             "--drop-late-frames",
            "--skip-frames",
            "--file-caching=100",
            "--network-caching=100",
            "--live-caching=100",
            "--disc-caching=100",
            "--no-input-fast-seek",
            "--no-video-title-show"
        );
        var media = new Media(libVLC, new Uri(Path.GetFullPath(videoFile)));
        var mediaPlayer = new MediaPlayer(media);

        if (loadingForm != null && loadingForm.IsHandleCreated)
        {
            loadingForm.Invoke((Action)(() => loadingForm.Close()));
            loadingForm = null;
        }

        try
        {
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

                isFirstLoad = false;
            }

            Console.WriteLine("Interactive finished.");
        }
        finally
        {
            mediaPlayer.Dispose();
            libVLC.Dispose();
            discordClient.Dispose();
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
}
