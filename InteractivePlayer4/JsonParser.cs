using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using Newtonsoft.Json.Linq;
using System.Linq;
using LibVLCSharp.Shared;
using System.Windows.Forms;
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

    public static (Dictionary<string, List<Moment>>, string, Dictionary<string, object>, Dictionary<string, object>, Dictionary<string, List<SegmentGroup>>, Dictionary<string, List<SegmentState>>) ParseMoments(string jsonFile)
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

            // Extract segment states
            var segmentStateToken = video["interactiveVideoMoments"]?["value"]?["segmentState"];
            var segmentStates = new Dictionary<string, List<SegmentState>>();

            if (segmentStateToken != null)
            {
                foreach (var property in segmentStateToken.Children<JProperty>())
                {
                    var segmentId = property.Name;
                    var states = property.Value.ToObject<List<SegmentState>>();
                    segmentStates[segmentId] = states;
                }
            }

            Console.WriteLine($"Loaded moments for {momentsBySegment.Count} segments.");
            return (momentsBySegment, videoId, globalState, persistentState, segmentGroups, segmentStates);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing info JSON: {ex.Message}");
            return (null, null, null, null, null, null);
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

                    // Transfer ImpressionData information
                    segment.ImpressionData = choiceMoment.ImpressionData;

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

                    // Handle moments with only impressionData
                    var impressionMoment = moments.Find(m => m.ImpressionData != null);
                    if (impressionMoment != null)
                    {
                        segment.ImpressionData = impressionMoment.ImpressionData;
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

    public static string HandleSegment(MediaPlayer mediaPlayer, Segment segment, Dictionary<string, Segment> segments, string movieFolder, string videoId, ref Dictionary<string, object> globalState, ref Dictionary<string, object> persistentState, string infoJsonFile, string saveFilePath, Dictionary<string, List<SegmentGroup>> segmentGroups, Dictionary<string, List<SegmentState>> segmentStates, bool isFirstLoad)
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

        // Apply segment state changes
        if (segmentStates.TryGetValue(segment.Id, out List<SegmentState> states))
        {
            foreach (var state in states)
            {
                if (string.IsNullOrEmpty(state.PreconditionId) || PreconditionChecker.CheckPrecondition(state.PreconditionId, localGlobalState, localPersistentState, infoJsonFile))
                {
                    if (state.Data.Persistent != null)
                    {
                        foreach (var kvp in state.Data.Persistent)
                        {
                            localPersistentState[kvp.Key] = kvp.Value;
                            Console.WriteLine($"Persistent state changed: {kvp.Key} = {kvp.Value}");
                        }
                    }
                }
            }
        }

        // Apply segment impressionData changes
        if (segment.ImpressionData != null)
        {
            var impressionData = segment.ImpressionData.Data;
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

        while (mediaPlayer.Time < segment.EndTimeMs - 360)
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

                Console.WriteLine($"Choice point reached for segment {segment.Id}");

                // Filter out choices with exceptions and those with text "blank"
                var validChoices = segment.Choices.Where(choice =>
                {
                    if (!string.IsNullOrEmpty(choice.Exception))
                    {
                        return !PreconditionChecker.CheckPrecondition(choice.Exception, localGlobalState, localPersistentState, infoJsonFile);
                    }
                    return choice.Text != "blank";
                }).ToList();

                // Apply overrides if preconditions are met
                foreach (var choice in validChoices)
                {
                    bool preconditionMet = false;
                    Override lastOverrideWithoutPrecondition = null;

                    // Extract data from the "default" property if it exists
                    if (choice.Default != null)
                    {
                        if (choice.Default.Background != null)
                        {
                            choice.Background = choice.Default.Background;
                        }
                        if (!string.IsNullOrEmpty(choice.Default.Text))
                        {
                            choice.Text = choice.Default.Text;
                        }
                        if (!string.IsNullOrEmpty(choice.Default.SegmentId))
                        {
                            choice.SegmentId = choice.Default.SegmentId;
                        }
                        if (choice.Default.Icon != null)
                        {
                            choice.Icon = choice.Default.Icon;
                        }
                        if (choice.Default.ImpressionData != null)
                        {
                            choice.ImpressionData = choice.Default.ImpressionData;
                        }
                        if (!string.IsNullOrEmpty(choice.Default.sg))
                        {
                            choice.sg = choice.Default.sg;
                        }
                    }

                    if (choice.Overrides != null)
                    {
                        foreach (var overrideItem in choice.Overrides)
                        {
                            if (string.IsNullOrEmpty(overrideItem.PreconditionId))
                            {
                                lastOverrideWithoutPrecondition = overrideItem;
                            }
                            else if (PreconditionChecker.CheckPrecondition(overrideItem.PreconditionId, localGlobalState, localPersistentState, infoJsonFile))
                            {
                                if (overrideItem.Data != null)
                                {
                                    if (overrideItem.Data.Background != null)
                                    {
                                        choice.Background = overrideItem.Data.Background;
                                    }
                                    if (!string.IsNullOrEmpty(overrideItem.Data.SegmentId))
                                    {
                                        choice.SegmentId = overrideItem.Data.SegmentId;
                                    }
                                }
                                preconditionMet = true;
                                break;
                            }
                        }

                        // If no preconditions are met, apply the last override without a precondition
                        if (!preconditionMet && lastOverrideWithoutPrecondition != null)
                        {
                            if (lastOverrideWithoutPrecondition.Data != null)
                            {
                                if (lastOverrideWithoutPrecondition.Data.Background != null)
                                {
                                    choice.Background = lastOverrideWithoutPrecondition.Data.Background;
                                }
                                if (!string.IsNullOrEmpty(lastOverrideWithoutPrecondition.Data.SegmentId))
                                {
                                    choice.SegmentId = lastOverrideWithoutPrecondition.Data.SegmentId;
                                }
                            }
                        }
                    }
                }

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
                                    if (kvp.Value is JArray operation && operation[0].ToString() == "sum")
                                    {
                                        string stateType = operation[1][0].ToString();
                                        string key = operation[1][1].ToString();
                                        int value = operation[2].ToObject<int>();

                                        if (stateType == "persistentState" && localPersistentState.ContainsKey(key))
                                        {
                                            if (localPersistentState[key] is int intValue)
                                            {
                                                localPersistentState[key] = intValue + value;
                                            }
                                            else if (localPersistentState[key] is long longValue)
                                            {
                                                localPersistentState[key] = longValue + value;
                                            }
                                            else if (localPersistentState[key] is double doubleValue)
                                            {
                                                localPersistentState[key] = doubleValue + value;
                                            }
                                            else
                                            {
                                                throw new InvalidCastException($"Cannot cast {localPersistentState[key].GetType()} to int, long, or double.");
                                            }
                                        }
                                        else if (stateType == "globalState" && localGlobalState.ContainsKey(key))
                                        {
                                            if (localGlobalState[key] is int intValue)
                                            {
                                                localGlobalState[key] = intValue + value;
                                            }
                                            else if (localGlobalState[key] is long longValue)
                                            {
                                                localGlobalState[key] = longValue + value;
                                            }
                                            else if (localGlobalState[key] is double doubleValue)
                                            {
                                                localGlobalState[key] = doubleValue + value;
                                            }
                                            else
                                            {
                                                throw new InvalidCastException($"Cannot cast {localGlobalState[key].GetType()} to int, long, or double.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        localPersistentState[kvp.Key] = kvp.Value;
                                    }
                                    Console.WriteLine($"Persistent state changed: {kvp.Key} = {kvp.Value}");
                                }
                            }
                        }
                    }
                    if (videoId == "10000001" || videoId == "81251335" || videoId == "80994695" || videoId == "80135585" || videoId == "81328829" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205738" || videoId == "81205737" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
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
                    nextSegment = item.Segment ?? item.GroupSegment;
                    break;
                }
            }
        }

        // Handle segment group (sg) if nextSegment is a segment group and no SegmentId is listed
        while (!segments.ContainsKey(nextSegment) && segmentGroups.TryGetValue(nextSegment, out List<SegmentGroup> nextGroup))
        {
            foreach (var item in nextGroup)
            {
                if (item.Precondition == null || PreconditionChecker.CheckPrecondition(item.Precondition, localGlobalState, localPersistentState, infoJsonFile))
                {
                    nextSegment = item.Segment ?? item.GroupSegment;
                    break;
                }
            }
        }

        // Check for special segment IDs to start a new episode
        if (nextSegment.Contains("playEpisode"))
        {
            // Extract episode number
            string episodeNumber = new string(nextSegment.Where(char.IsDigit).ToArray());
            string message = $"To Continue, Play Episode {episodeNumber}";

            MessageBox.Show(message, "Episode Required", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            return null; // Return null to indicate the current interactive should stop
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
}