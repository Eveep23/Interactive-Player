using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using Newtonsoft.Json.Linq;
using System.Linq;
using LibVLCSharp.Shared;
using System.Threading;
using System.Net;
using Newtonsoft.Json;
using SharpDX.DirectInput;
using SharpDX.XInput;

public static class JsonParser
{
    public static Dictionary<string, Segment> ParseSegments(string jsonFile, ref string initialSegment, long videoDuration)
    {
        try
        {
            var json = JObject.Parse(File.ReadAllText(jsonFile));

            // Handle initialSegment
            initialSegment = json["initialSegment"]?.ToString();

            var segmentsToken = json["segments"];
            if (segmentsToken == null) throw new Exception("Segments key not found.");

            var segments = new Dictionary<string, Segment>();
            foreach (var property in segmentsToken.Children<JProperty>())
            {
                var segment = property.Value.ToObject<Segment>();
                if (segment != null)
                {
                    segment.Id = property.Name;

                    // Handle missing endTimeMs
                    if (segment.EndTimeMs == 0)
                    {
                        segment.EndTimeMs = (int)videoDuration;
                    }

                    segments[property.Name] = segment;
                }
            }

            Console.WriteLine($"Loaded {segments.Count} segments.");
            return segments;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing JSON: {ex.Message}");
            return null;
        }
    }

    public static (Dictionary<string, List<Moment>>, string, Dictionary<string, object>, Dictionary<string, object>, Dictionary<string, List<SegmentGroup>>) ParseMoments(string jsonFile)
    {
        try
        {
            var json = JObject.Parse(File.ReadAllText(jsonFile));
            var video = json["jsonGraph"]?["videos"]?.First?.First;

            if (video == null)
                throw new Exception("Video node not found in info JSON.");

            // Extract video ID
            var pathsArray = json["paths"] as JArray;
            var videoId = pathsArray?[0]?[1]?.ToString();
            if (string.IsNullOrEmpty(videoId))
                throw new Exception("Video ID not found in info JSON.");

            // Attempt to access momentsBySegment at different nesting levels
            var momentsBySegmentToken = video["interactiveVideoMoments"]?["value"]?["momentsBySegment"]
                                       ?? video["interactiveVideoMoments"]?["momentsBySegment"];

            if (momentsBySegmentToken == null)
                throw new Exception("MomentsBySegment node not found.");

            var momentsBySegment = new Dictionary<string, List<Moment>>();

            foreach (var property in momentsBySegmentToken.Children<JProperty>())
            {
                var segmentId = property.Name;
                var moments = property.Value.ToObject<List<Moment>>();
                momentsBySegment[segmentId] = moments;
            }

            // Extract global and persistent states
            var stateHistory = video["interactiveVideoMoments"]?["value"]?["stateHistory"];
            var globalState = stateHistory?["global"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
            var persistentState = stateHistory?["persistent"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

            // Extract segment groups
            var segmentGroupsToken = video["interactiveVideoMoments"]?["value"]?["segmentGroups"];
            var segmentGroups = new Dictionary<string, List<SegmentGroup>>();

            if (segmentGroupsToken != null)
            {
                foreach (var property in segmentGroupsToken.Children<JProperty>())
                {
                    var segmentId = property.Name;
                    var group = property.Value.ToObject<List<SegmentGroup>>(new JsonSerializer { Converters = { new SegmentGroupConverter() } });
                    segmentGroups[segmentId] = group;
                }
            }

            Console.WriteLine($"Loaded moments for {momentsBySegment.Count} segments.");
            return (momentsBySegment, videoId, globalState, persistentState, segmentGroups);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing info JSON: {ex.Message}");
            return (null, null, null, null, null);
        }
    }

    public static void MergeMomentsIntoSegments(Dictionary<string, Segment> segments, Dictionary<string, List<Moment>> momentsBySegment)
    {
        foreach (var segment in segments.Values)
        {
            if (momentsBySegment.TryGetValue(segment.Id, out List<Moment> moments))
            {
                var choiceMoment = moments.Find(m => m.Type == "scene:cs_template");
                if (choiceMoment != null)
                {
                    segment.Choices = choiceMoment.Choices;
                    segment.ChoiceDisplayTimeMs = choiceMoment.UIDisplayMS ?? 0;
                    segment.HideChoiceTimeMs = choiceMoment.HideTimeoutUiMS ?? segment.EndTimeMs;

                    // Transfer TimeoutSegment information
                    segment.TimeoutSegment = choiceMoment.TimeoutSegment;

                    // Transfer LayoutType information
                    segment.LayoutType = choiceMoment.LayoutType;

                    // Transfer Notification information
                    segment.Notification = choiceMoment.Notification;

                    // Assign segmentId to each choice
                    if (segment.Choices != null)
                    {
                        foreach (var choice in segment.Choices)
                        {
                            if (string.IsNullOrEmpty(choice.SegmentId))
                            {
                                choice.SegmentId = choice.sg ?? choice.Id;
                            }
                        }
                    }
                }
                else
                {
                    // Handle moments with only notifications
                    var notificationMoment = moments.Find(m => m.Notification != null && m.Notification.Count > 0);
                    if (notificationMoment != null)
                    {
                        segment.Notification = notificationMoment.Notification;
                    }
                }
            }
        }
    }

    // Trim the segment ID
    private static string TrimSegmentId(string segmentId)
    {
        if (segmentId.Contains("_"))
        {
            string trimmedId = segmentId.Split('_')[0];
            Console.WriteLine($"Segment {segmentId} not found. Using trimmed ID: {trimmedId}");
            return trimmedId;
        }

        return segmentId;
    }

    public static string HandleSegment(MediaPlayer mediaPlayer, Segment segment, Dictionary<string, Segment> segments, string movieFolder, string videoId, ref Dictionary<string, object> globalState, ref Dictionary<string, object> persistentState, string infoJsonFile, string saveFilePath, Dictionary<string, List<SegmentGroup>> segmentGroups, bool isFirstLoad)
    {
        mediaPlayer.Time = segment.StartTimeMs;
        string nextSegment = segment.DefaultNext;
        bool choiceDisplayed = false;

        string defaultButtonTexturePath = FindDefaultButtonTexture(movieFolder, segment.Choices ?? new List<Choice>());

        // Load audio and subtitle tracks from config save file after media is parsed
        string configSaveFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        var localGlobalState = globalState;
        var localPersistentState = persistentState;

        mediaPlayer.Media.ParsedChanged += (sender, e) =>
        {
            if (e.ParsedStatus == MediaParsedStatus.Done)
            {
                AudioManager.LoadAudioTrackFromSaveFile(mediaPlayer, mediaPlayer.Media, configSaveFilePath);
                SubtitleManager.LoadSubtitleTrackFromSaveFile(mediaPlayer, mediaPlayer.Media, configSaveFilePath);
            }
        };

        // Only reset states if this is the first time loading the save
        if (isFirstLoad)
        {
            var initialStates = LoadInitialStates(infoJsonFile);
            globalState = initialStates.globalState;
            persistentState = initialStates.persistentState;
            localGlobalState = globalState;
            localPersistentState = persistentState;
        }

        while (mediaPlayer.Time < segment.EndTimeMs)
        {
            // Display notification if within the specified time range
            if (segment.Notification != null)
            {
                foreach (var notification in segment.Notification)
                {
                    if (mediaPlayer.Time >= notification.StartMs && mediaPlayer.Time <= notification.EndMs)
                    {
                        int displayDurationMs = notification.EndMs - notification.StartMs;
                        UIManager.ShowNotificationUI(notification.Text, movieFolder, videoId, displayDurationMs);
                    }
                }
            }

            if (!choiceDisplayed && segment.Choices != null && segment.Choices.Count > 0 &&
                mediaPlayer.Time >= segment.ChoiceDisplayTimeMs)
            {
                long choiceDurationMs = segment.HideChoiceTimeMs - segment.ChoiceDisplayTimeMs;

                if (choiceDurationMs <= 0 || choiceDurationMs > 30000)
                {
                    choiceDurationMs = 30000;
                }

                Console.WriteLine($"Choice point reached for segment {segment.Id}");

                // Filter out choices with exceptions
                var validChoices = segment.Choices.Where(choice =>
                {
                    if (!string.IsNullOrEmpty(choice.Exception))
                    {
                        return !PreconditionChecker.CheckPrecondition(choice.Exception, localGlobalState, localPersistentState, infoJsonFile);
                    }
                    return true;
                }).ToList();

                // Load button sprites for each valid choice
                var buttonSprites = new List<Bitmap>();
                foreach (var choice in validChoices)
                {
                    string buttonSpritePath = null;

                    // Use specific button texture for this choice
                    string url = choice?.Background?.VisualStates?.Default?.Image?.Url;
                    if (!string.IsNullOrEmpty(url))
                    {
                        buttonSpritePath = FindTexturePath(movieFolder, Path.GetFileName(new Uri(url).LocalPath));
                        if (Path.GetExtension(buttonSpritePath).Equals(".webp", StringComparison.OrdinalIgnoreCase))
                        {
                            buttonSpritePath = Path.ChangeExtension(buttonSpritePath, ".png");
                        }
                    }

                    // If specific button texture is not found, use the default button texture
                    if (string.IsNullOrEmpty(buttonSpritePath) || !File.Exists(buttonSpritePath))
                    {
                        buttonSpritePath = defaultButtonTexturePath;
                    }

                    // Add the button sprite or null if no texture is found
                    if (!string.IsNullOrEmpty(buttonSpritePath) && File.Exists(buttonSpritePath))
                    {
                        buttonSprites.Add(new Bitmap(buttonSpritePath));
                    }
                    else
                    {
                        buttonSprites.Add(null);
                    }
                }

                var buttonIcons = new List<Bitmap>();
                foreach (var choice in validChoices)
                {
                    string iconPath = null;

                    // Check for an icon URL in the choice's Icon field
                    string iconUrl = choice?.Icon?.VisualStates?.Default?.Image?.Url;
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        iconPath = FindTexturePath(movieFolder, Path.GetFileName(new Uri(iconUrl).LocalPath));
                        if (Path.GetExtension(iconPath).Equals(".webp", StringComparison.OrdinalIgnoreCase))
                        {
                            iconPath = Path.ChangeExtension(iconPath, ".png");
                        }
                    }

                    if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                    {
                        buttonIcons.Add(new Bitmap(iconPath));
                    }
                    else
                    {
                        buttonIcons.Add(null);
                    }
                }

                string selectedSegment = UIManager.ShowChoiceUI(validChoices, buttonSprites, buttonIcons, (int)choiceDurationMs, movieFolder, videoId, segment);

                if (!string.IsNullOrEmpty(selectedSegment))
                {
                    nextSegment = selectedSegment;

                    // Update states based on the chosen option
                    var chosenOption = validChoices.FirstOrDefault(c => c.SegmentId == selectedSegment);
                    if (chosenOption != null && chosenOption.ImpressionData != null)
                    {
                        var impressionData = chosenOption.ImpressionData.Data;
                        if (impressionData != null)
                        {
                            if (impressionData.Global != null)
                            {
                                foreach (var kvp in impressionData.Global)
                                {
                                    localGlobalState[kvp.Key] = kvp.Value;
                                    Console.WriteLine($"Global state changed: {kvp.Key} = {kvp.Value}");
                                }
                            }

                            if (impressionData.Persistent != null)
                            {
                                foreach (var kvp in impressionData.Persistent)
                                {
                                    localPersistentState[kvp.Key] = kvp.Value;
                                    Console.WriteLine($"Persistent state changed: {kvp.Key} = {kvp.Value}");
                                }
                            }
                        }
                    }
                    if (videoId == "10000001")
                    {
                        break; // Break out of the loop and return the selected segment immediately
                    }
                }
                else
                {
                    Console.WriteLine("No choice made. Defaulting to the specified choice.");
                    if (segment.TimeoutSegment != null)
                    {
                        nextSegment = IsControllerConnected() ? "Fallback_Tutorial_Controller" : "Fallback_Tutorial_Site";
                    }
                    else
                    {
                        nextSegment = GetDefaultChoice(segment);
                    }
                }

                choiceDisplayed = true;
            }

            HandleKeyPress(mediaPlayer, infoJsonFile, saveFilePath);
        }

        // Check segment groups for the next segment
        if (segmentGroups.TryGetValue(segment.Id, out List<SegmentGroup> group))
        {
            foreach (var item in group)
            {
                if (item.Precondition == null || PreconditionChecker.CheckPrecondition(item.Precondition, localGlobalState, localPersistentState, infoJsonFile))
                {
                    nextSegment = item.Segment;
                    break;
                }
            }
        }

        // Handle segment group (sg) if nextSegment is a segment group and no SegmentId is listed
        if (segments.ContainsKey(nextSegment))
        {
            return nextSegment;
        }

        if (segmentGroups.TryGetValue(nextSegment, out List<SegmentGroup> nextGroup))
        {
            foreach (var item in nextGroup)
            {
                if (item.Precondition == null || PreconditionChecker.CheckPrecondition(item.Precondition, localGlobalState, localPersistentState, infoJsonFile))
                {
                    nextSegment = item.Segment;
                    break;
                }
            }
        }

        return nextSegment;
    }

    // Method to load initial states from the info JSON
    private static (Dictionary<string, object> globalState, Dictionary<string, object> persistentState) LoadInitialStates(string infoJsonFile)
    {
        var json = JObject.Parse(File.ReadAllText(infoJsonFile));
        var video = json["jsonGraph"]?["videos"]?.First?.First;

        var stateHistory = video["interactiveVideoMoments"]?["value"]?["stateHistory"];
        var globalState = stateHistory?["global"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
        var persistentState = stateHistory?["persistent"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

        return (globalState, persistentState);
    }


    // Check if an Xbox controller is connected
    private static bool IsControllerConnected()
    {
        var directInput = new SharpDX.DirectInput.DirectInput();
        var joystickGuid = Guid.Empty;

        foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad, SharpDX.DirectInput.DeviceEnumerationFlags.AllDevices))
        {
            joystickGuid = deviceInstance.InstanceGuid;
            break;
        }

        if (joystickGuid == Guid.Empty)
        {
            foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Joystick, SharpDX.DirectInput.DeviceEnumerationFlags.AllDevices))
            {
                joystickGuid = deviceInstance.InstanceGuid;
                break;
            }
        }

        return joystickGuid != Guid.Empty;
    }

    // Find the default button texture
    private static string FindDefaultButtonTexture(string movieFolder, List<Choice> choices)
    {
        if (choices != null)
        {
            foreach (var choice in choices)
            {
                string url = choice?.Background?.VisualStates?.Default?.Image?.Url;
                if (!string.IsNullOrEmpty(url))
                {
                    string localPath = FindTexturePath(movieFolder, Path.GetFileName(new Uri(url).LocalPath));
                    if (File.Exists(localPath))
                    {
                        return localPath;
                    }
                }
            }
        }

        string[] potentialDefaultTextures = new[]
        {
            "choices_sprite_2x.png",
            "text_background_sprite_2x.png",
            "choice_bg_sprite_2x.png",
            "default_button_sprite_2x.png",
            "choice_container_sprite_2x.png",
            "button_sprite.png",
            "pib_choices_2x.png"
        };

        foreach (string textureName in potentialDefaultTextures)
        {
            string texturePath = FindTexturePath(movieFolder, textureName);
            if (File.Exists(texturePath))
            {
                return texturePath;
            }
        }

        // If no texture is found
        Console.WriteLine("Default button texture not found in the movie folder.");
        return null;
    }

    private static string FindTexturePath(string folder, string textureName)
    {
        var files = Directory.GetFiles(folder, textureName, SearchOption.AllDirectories);
        if (files.Length > 0)
        {
            return files[0];
        }
        return null;
    }

    public static string GetDefaultButtonSpritePath(string jsonFilePath)
    {
        try
        {
            string jsonContent = File.ReadAllText(jsonFilePath);
            dynamic jsonData = JsonConvert.DeserializeObject(jsonContent);

            string defaultSpriteUrl = jsonData?.jsonGraph?.videos?.First?.interactiveVideoMoments?.value
                ?.uiDefinition?.layouts?.l0?.elements?.notification?.children?.background?.backgroundImage?.url;

            if (!string.IsNullOrEmpty(defaultSpriteUrl))
            {
                Console.WriteLine($"Default button sprite URL found: {defaultSpriteUrl}");
                return DownloadSprite(defaultSpriteUrl);
            }
            else
            {
                Console.WriteLine("No default button sprite URL found in the JSON.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting default button sprite: {ex.Message}");
            return null;
        }
    }

    // Download and save sprite locally
    private static string DownloadSprite(string url)
    {
        try
        {
            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

            if (!File.Exists(localPath))
            {
                using (var client = new WebClient())
                {
                    Console.WriteLine($"Downloading sprite from: {url}");
                    client.DownloadFile(url, localPath);
                    Console.WriteLine($"Sprite downloaded to: {localPath}");
                }
            }
            else
            {
                Console.WriteLine($"Sprite already exists at: {localPath}");
            }

            return localPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading sprite from {url}: {ex.Message}");
            return null;
        }
    }

    private static void HandleKeyPress(MediaPlayer mediaPlayer, string infoJsonFile, string saveFilePath)
    {
        if (!Console.KeyAvailable) return;

        var key = Console.ReadKey(intercept: true).Key;

        switch (key)
        {
            case ConsoleKey.Spacebar:
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

            case ConsoleKey.L:
                Console.WriteLine("Switching audio track...");
                AudioManager.ListAndSelectAudioTrack(mediaPlayer, mediaPlayer.Media);
                break;

            case ConsoleKey.S:
                Console.WriteLine("Switching subtitles...");
                SubtitleManager.ListAndSelectSubtitleTrack(mediaPlayer, mediaPlayer.Media);
                break;

            case ConsoleKey.C:
                Console.WriteLine("Checking preconditions...");
                PreconditionChecker.CheckPreconditions(infoJsonFile, saveFilePath);
                break;

            default:
                // No action for other keys
                break;
        }
    }

    private static string GetDefaultChoice(Segment segment)
    {
        if (segment.DefaultChoiceIndex.HasValue && segment.Choices != null && segment.Choices.Count > segment.DefaultChoiceIndex.Value)
        {
            return segment.Choices[segment.DefaultChoiceIndex.Value].SegmentId;
        }
        return segment.DefaultNext;
    }

    private static string GetChoiceWithTimeout(Segment segment, long timeLimitMs)
    {
        string selectedChoice = null;
        bool inputCaptured = false;

        Thread inputThread = new Thread(() =>
        {
            Console.WriteLine($"You have {timeLimitMs / 1000} seconds to make a choice:");
            for (int i = 0; i < segment.Choices.Count; i++)
            {
                Console.WriteLine($"{i + 1}: {segment.Choices[i].Text}");
            }

            Console.Write("Enter your choice: ");
            if (int.TryParse(Console.ReadLine(), out int choiceIndex) &&
                choiceIndex > 0 && choiceIndex <= segment.Choices.Count)
            {
                selectedChoice = segment.Choices[choiceIndex - 1].SegmentId;
                inputCaptured = true;
            }
        });

        inputThread.Start();
        inputThread.Join((int)timeLimitMs);

        if (!inputCaptured)
        {
            Console.WriteLine("Input timed out.");
        }

        return selectedChoice;
    }
}
