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

            var headerImageToken = video["interactiveVideoMoments"]?["value"]?["headerImage"];
            if (headerImageToken != null)
            {
                foreach (var property in momentsBySegmentToken.Children<JProperty>())
                {
                    var segmentId = property.Name;
                    var moments = property.Value.ToObject<List<Moment>>();

                    // Assign headerImage to each moment
                    foreach (var moment in moments)
                    {
                        var headerImageUrl = headerImageToken["url"]?.ToString();
                        if (!string.IsNullOrEmpty(headerImageUrl))
                        {
                            moment.HeaderImage = new HeaderImage { Url = headerImageUrl };
                        }
                    }

                    momentsBySegment[segmentId] = moments;
                }
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

    public static void MergeMomentsIntoSegments(Dictionary<string, Segment> segments, Dictionary<string, List<Moment>> momentsBySegment, string videoId)
    {
        foreach (var segment in segments.Values)
        {
            if (momentsBySegment.TryGetValue(segment.Id, out List<Moment> moments))
            {
                foreach (var moment in moments.Where(m => m.Type == "notification:playbackImpression" && m.ImpressionData != null))
                {
                    var impressionData = moment.ImpressionData.Data;
                    if (impressionData != null)
                    {
                        if (impressionData.Global != null)
                        {
                            foreach (var kvp in impressionData.Global)
                            {
                                Console.WriteLine($"Global state changed: {kvp.Key} = {kvp.Value}");
                            }
                        }

                        if (impressionData.Persistent != null)
                        {
                            foreach (var kvp in impressionData.Persistent)
                            {
                                Console.WriteLine($"Persistent state changed: {kvp.Key} = {kvp.Value}");
                            }
                        }
                    }
                }

                var tutorialMoment = moments.Find(m => m.Type == "notification:inlineTutorial");
                if (tutorialMoment != null)
                {
                    segment.TutorialMoment = tutorialMoment;
                }

                var choiceMoment = moments.Find(m => m.Type == "scene:cs_template");
                if (choiceMoment != null)
                {
                    segment.Choices = choiceMoment.Choices ?? choiceMoment.ChoiceSets?.FirstOrDefault();
                    segment.fakechoices = choiceMoment.fakechoices;
                    segment.ChoiceSets = choiceMoment.ChoiceSets;
                    segment.HeaderImage = choiceMoment.HeaderImage;
                    segment.HeaderText = choiceMoment.HeaderText;
                    segment.AnswerSequence = choiceMoment.AnswerSequence;
                    segment.CorrectIndex = choiceMoment.CorrectIndex;
                    segment.Id = choiceMoment.id;

                    if (videoId == "80988062")
                    {
                        segment.ChoiceDisplayTimeMs = choiceMoment.uiInteractionStartMS ?? 0;
                        segment.HideChoiceTimeMs = choiceMoment.uiHideMS ?? segment.EndTimeMs;
                    }
                    else
                    {
                        segment.ChoiceDisplayTimeMs = choiceMoment.UIDisplayMS ?? 0;
                        segment.HideChoiceTimeMs = choiceMoment.HideTimeoutUiMS ?? segment.EndTimeMs;
                    }

                    segment.TimeoutSegment = choiceMoment.TimeoutSegment;
                    segment.LayoutType = choiceMoment.LayoutType;
                    segment.Notification = choiceMoment.Notification;
                    segment.DefaultChoiceIndex = choiceMoment.DefaultChoiceIndex;
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

                    // Handle "fake" choices
                    if (choiceMoment.fakechoices != null && choiceMoment.fakechoices.Count > 0)
                    {
                        segment.fakeChoiceDisplayTimeMs = choiceMoment.fakeuiInteractionStartMS ?? segment.ChoiceDisplayTimeMs;
                        segment.fakeHideChoiceTimeMs = choiceMoment.fakeuiHideMS ?? segment.HideChoiceTimeMs;
                        segment.fakechoices = choiceMoment.fakechoices;
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
        /* if (Math.Abs(mediaPlayer.Time - segment.StartTimeMs) > 102)
        {
            mediaPlayer.Time = segment.StartTimeMs + 22;
        } */

        string nextSegment = segment.DefaultNext;
        bool choiceDisplayed = false;
        bool fakeChoiceDisplayed = false;
        bool tutorialDisplayed = false;

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
        if (segmentStates != null && !string.IsNullOrEmpty(segment.Id))
        {
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
        }
        else
        {
            Console.WriteLine($"Segment state not found for segment ID: {segment?.Id ?? "null"}");
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

        // Ensure EndTimeMs has a value
        int endTimeMs = segment.EndTimeMs > 0 ? segment.EndTimeMs : int.MaxValue;

        KeyForm.InitializeKeyPressWindow(mediaPlayer, infoJsonFile, saveFilePath, segment, segments);

        while (mediaPlayer.Time < endTimeMs - 105)
        {
            if (videoId == "10000001")
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
            }

            if (videoId == "80988062")
            {
                // Handle "fake" choices
                if (!fakeChoiceDisplayed && segment.fakechoices != null && segment.fakechoices.Count > 0 &&
                mediaPlayer.Time >= segment.fakeChoiceDisplayTimeMs && mediaPlayer.Time < segment.fakeHideChoiceTimeMs)
                {
                    long fakeChoiceDurationMs = segment.fakeHideChoiceTimeMs - segment.fakeChoiceDisplayTimeMs;

                    Console.WriteLine($"Fake choice point reached for segment {segment.Id}");

                    var fakeChoiceTexts = segment.fakechoices.Select(fc => fc.Text).ToList();
                    var fakeButtonSprites = new List<Bitmap>();
                    var fakeButtonIcons = new List<Bitmap>();

                    foreach (var fakeChoice in segment.fakechoices)
                    {
                        string buttonSpritePath = defaultButtonTexturePath;
                        string iconPath = null;

                        // Add button sprite
                        if (!string.IsNullOrEmpty(buttonSpritePath) && File.Exists(buttonSpritePath))
                        {
                            fakeButtonSprites.Add(new Bitmap(buttonSpritePath));
                        }
                        else
                        {
                            fakeButtonSprites.Add(null);
                        }

                        // Add button icon
                        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                        {
                            fakeButtonIcons.Add(new Bitmap(iconPath));
                        }
                        else
                        {
                            fakeButtonIcons.Add(null);
                        }
                    }

                    UIManager.ShowChoiceUI(segment.fakechoices, fakeButtonSprites, fakeButtonIcons, (int)fakeChoiceDurationMs, movieFolder, videoId, segment);

                    fakeChoiceDisplayed = true;
                }
            }
            
            var tut = segment.TutorialMoment;
            if (!tutorialDisplayed && segment.TutorialMoment != null &&
                mediaPlayer.Time >= tut.StartMs)
            {
                int tutorialDurationMs = (tut.EndMs ?? 0) - (tut.StartMs ?? 0);

                UIManager.ShowTutorialWindow(tut.HeaderText, tut.BodyText, tutorialDurationMs, videoId, movieFolder);

                tutorialDisplayed = true;
            }
            
            if (!choiceDisplayed && segment.Choices != null && segment.Choices.Count > 0 &&
                mediaPlayer.Time >= segment.ChoiceDisplayTimeMs)
            {
                long choiceDurationMs = segment.HideChoiceTimeMs - segment.ChoiceDisplayTimeMs;

                //Console.WriteLine($"Choice point reached for segment {segment.Id}");

                // Determine the valid choices to display
                List<Choice> validChoices;

                // Load questions from questions.json
                string questionsFilePath = Path.Combine(movieFolder, "questions.json");
                if (File.Exists(questionsFilePath))
                {
                    var questionsJson = JObject.Parse(File.ReadAllText(questionsFilePath));
                    var questions = questionsJson["questions"]?.ToObject<Dictionary<string, Question>>();

                    if (questions != null && questions.Count > 0)
                    {
                        // Randomly select a question
                        var random = new Random();
                        var randomQuestionKey = questions.Keys.ElementAt(random.Next(questions.Count));
                        var randomQuestion = questions[randomQuestionKey];

                        //Console.WriteLine($"Loaded question: {randomQuestionKey}");

                        // Replace choiceSets while retaining SegmentId and sg
                        if (segment.ChoiceSets != null && randomQuestion.ChoiceSets != null)
                        {
                            for (int i = 0; i < segment.ChoiceSets.Count && i < randomQuestion.ChoiceSets.Count; i++)
                            {
                                var originalChoices = segment.ChoiceSets[i];
                                var newChoices = randomQuestion.ChoiceSets[i];

                                for (int j = 0; j < originalChoices.Count && j < newChoices.Count; j++)
                                {
                                    // Retain SegmentId and sg, replace other fields
                                    originalChoices[j].Text = newChoices[j].Text;
                                    originalChoices[j].SubText = newChoices[j].SubText;
                                    originalChoices[j].Background = newChoices[j].Background;
                                    originalChoices[j].Icon = newChoices[j].Icon;
                                    originalChoices[j].Overrides = newChoices[j].Overrides;
                                    originalChoices[j].Default = newChoices[j].Default;
                                    originalChoices[j].ImpressionData = newChoices[j].ImpressionData;
                                }
                            }
                        }

                        // Replace AnswerSequence
                        if (randomQuestion.AnswerSequence != null)
                        {
                            segment.AnswerSequence = randomQuestion.AnswerSequence;
                        }

                        //Console.WriteLine($"Current Answer Sequence: {string.Join(", ", segment.AnswerSequence)}");

                        // Replace HeaderImage with the corresponding image for the random question
                        if (segment.ChoiceSets != null && segment.ChoiceSets.Count > 0)
                        {
                            string headerImagePath = FindTexturePath(movieFolder, $"{randomQuestionKey}.png");
                            if (!string.IsNullOrEmpty(headerImagePath) && File.Exists(headerImagePath))
                            {
                                segment.HeaderImage = new HeaderImage { Url = headerImagePath };
                            }
                        }
                        else
                        {
                            segment.HeaderImage = null;
                        }

                    }
                }

                if (segment.ChoiceSets != null && segment.ChoiceSets.Count > 0)
                {
                    // Combine all choices from all choice sets
                    validChoices = segment.ChoiceSets.SelectMany(choiceSet => choiceSet).ToList();
                }
                else if (videoId == "81131714" && segment.LayoutType == "l5")
                {
                    // Only show choices whose precondition is met
                    validChoices = segment.Choices
                        .Where(choice =>
                            !string.IsNullOrEmpty(choice.PreconditionId) &&
                            PreconditionChecker.CheckPrecondition(choice.PreconditionId, localGlobalState, localPersistentState, infoJsonFile)
                        )
                        .ToList();
                }
                else
                {
                    validChoices = segment.Choices.Where(choice =>
                    {
                        if (!string.IsNullOrEmpty(choice.Exception))
                        {
                            return !PreconditionChecker.CheckPrecondition(choice.Exception, localGlobalState, localPersistentState, infoJsonFile);
                        }
                        return choice.Text != "blank";
                    }).ToList();
                }

                // Determine the correct and wrong segments
                string correctSegmentId = validChoices.FirstOrDefault()?.SegmentId ?? validChoices.FirstOrDefault()?.sg;
                string wrongSegmentId = validChoices.LastOrDefault()?.SegmentId ?? validChoices.LastOrDefault()?.sg;

                if (segment.AnswerSequence != null && segment.AnswerSequence.Count > 0)
                {
                    // The correct answer is always the first choice, and the wrong answer is the last choice
                    correctSegmentId = validChoices.FirstOrDefault()?.SegmentId ?? validChoices.FirstOrDefault()?.sg;
                    wrongSegmentId = validChoices.LastOrDefault()?.SegmentId ?? validChoices.LastOrDefault()?.sg;
                }

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

                var configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(configFilePath));
                bool customStoryChangingNotification = settings?.CustomStoryChangingNotification ?? false;

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

                    if (videoId == "80988062" && customStoryChangingNotification && buttonSpritePath != null)
                    {
                        string fileName = Path.GetFileName(buttonSpritePath);
                        if (fileName.Equals("netflix_2x.png", StringComparison.OrdinalIgnoreCase))
                        {
                            // Switch to "emulator_2x.png" if the real image is "netflix_2x.png"
                            string emulatorSpritePath = FindTexturePath(movieFolder, "emulator_2x.png");
                            if (!string.IsNullOrEmpty(emulatorSpritePath) && File.Exists(emulatorSpritePath))
                            {
                                buttonSpritePath = emulatorSpritePath;
                            }
                        }
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

                var (selectedSegment, choiceId) = UIManager.ShowChoiceUI(validChoices, buttonSprites, buttonIcons, (int)choiceDurationMs, movieFolder, videoId, segment, segment.HeaderText);

                if (!string.IsNullOrEmpty(selectedSegment))
                {
                    nextSegment = selectedSegment;

                    // Update states based on the chosen option
                    Choice chosenOption;
                    if (videoId == "81481556" || videoId == "81131714" && segment.LayoutType == "l69" || videoId == "81131714" && segment.LayoutType == "l5")
                    {
                        chosenOption = validChoices.FirstOrDefault(c => c.Id == choiceId);
                    }
                    else
                    {
                        chosenOption = validChoices.FirstOrDefault(c => c.SegmentId == selectedSegment);
                    }
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
                                    //Console.WriteLine($"Global state changed: {kvp.Key} = {kvp.Value}");
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
                                    //Console.WriteLine($"Persistent state changed: {kvp.Key} = {kvp.Value}");
                                }
                            }
                        }
                    }

                    if (videoId == "81131714" && segment.Choices != null && segment.Choices.Any(choice => choice.Text?.Equals("EXIT TO CREDITS", StringComparison.OrdinalIgnoreCase) == true) || videoId == "81131714" && segment.LayoutType == "l69" && chosenOption.Id == "SkipAhead" || videoId == "80988062" && segment.Choices != null && segment.Choices.Any(choice => choice.Text?.Equals("GO BACK", StringComparison.OrdinalIgnoreCase) == true) || videoId == "80988062" && segment.Choices != null && segment.Choices.Any(choice => choice.Text?.Equals("EXIT TO CREDITS", StringComparison.OrdinalIgnoreCase) == true) || videoId == "81131714" && segment.LayoutType == "l6" || videoId == "81481556"|| videoId == "10000001" || videoId == "10000003" || videoId == "81251335" || videoId == "80994695" || videoId == "80135585" || videoId == "81328829" || videoId == "80227804" || videoId == "80227805" || videoId == "80227800" || videoId == "80227801" || videoId == "80227802" || videoId == "80227803" || videoId == "80227699" || videoId == "80227698" || videoId == "81319137" || videoId == "81205738" || videoId == "81205737" || videoId == "80227815" || videoId == "81250260" || videoId == "81250261" || videoId == "81250262" || videoId == "81250263" || videoId == "81250264" || videoId == "81250265" || videoId == "81250266" || videoId == "81250267")
                    {
                        break; // Break out of the loop and return the selected segment immediately
                    }
                }

                choiceDisplayed = true;
            }
            
            if (mediaPlayer.Time >= endTimeMs - 105)
            {
                mediaPlayer.Pause();
                break;
            }
            
            Thread.Sleep(5);
        }

        mediaPlayer.Pause();

        if (videoId == "81271335" && segment?.Id == "Ident-Head")
        {
            var controller = new SharpDX.XInput.Controller(SharpDX.XInput.UserIndex.One);
            if (controller.IsConnected)
            {
                nextSegment = "s02";
            }
            else
            {
                nextSegment = "s04";
            }
        }

        // Check segment groups for the next segment
        if (!string.IsNullOrEmpty(segment?.Id) && segmentGroups.TryGetValue(segment.Id, out List<SegmentGroup> group))
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
            if (videoId == "81271335" && (nextSegment == "sL1" || nextSegment == "sL2" || nextSegment == "sL3" || nextSegment == "sL4" || segment.Id == "sL5" || nextSegment == "sL6" || nextSegment == "sL1_Cutdown" || nextSegment == "sL2_Cutdown" || nextSegment == "sL3_Cutdown" || nextSegment == "sL4_Cutdown" || nextSegment == "sL5_Cutdown" || nextSegment == "sL6_Cutdown" || nextSegment == "sVariantRetry"))
            {
                nextGroup = ShuffleInGroupsOfFour(nextGroup);
            }

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

        if (videoId == "81481556" && (string.IsNullOrEmpty(nextSegment) || !segments.ContainsKey(nextSegment)))
        {
            var folderName = new DirectoryInfo(movieFolder).Name;
            var episodeSuffix = folderName.Split(' ').LastOrDefault(s => s.StartsWith("E", StringComparison.OrdinalIgnoreCase) && s.Length > 1);
            int episodeNum = 0;
            if (episodeSuffix != null && int.TryParse(episodeSuffix.Substring(1), out episodeNum))
            {
                int nextEpisode = episodeNum + 1;
                string message = $"To Continue, Play Episode {nextEpisode:D2}";
                MessageBox.Show(message, "Episode Required", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            }
            else
            {
                MessageBox.Show("To Continue, Play the Next Episode", "Episode Required", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            }
            return null;
        }

        if (!string.IsNullOrEmpty(nextSegment) && segments.TryGetValue(nextSegment, out Segment nextSeg))
        {
            if (Math.Abs(mediaPlayer.Time - nextSeg.StartTimeMs) > 422)
            {
                mediaPlayer.Time = nextSeg.StartTimeMs + 25;
            }
        }

        mediaPlayer.Play();

        return nextSegment;
    }

    private static List<SegmentGroup> ShuffleInGroupsOfFour(List<SegmentGroup> input)
    {
        if (input == null || input.Count <= 1)
            return input;

        var rnd = new Random();
        var result = new List<SegmentGroup>(input.Count);

        for (int i = 0; i < input.Count; i += 4)
        {
            int groupSize = Math.Min(4, input.Count - i);
            var chunk = input.GetRange(i, groupSize);
            if (chunk.Count > 1)
            {
                chunk = chunk.OrderBy(x => rnd.Next()).ToList();
            }
            result.AddRange(chunk);
        }

        return result;
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

    private static string GetDefaultChoice(Segment segment)
    {
        if (segment.DefaultChoiceIndex.HasValue && segment.Choices != null && segment.Choices.Count > segment.DefaultChoiceIndex.Value)
        {
            return segment.Choices[segment.DefaultChoiceIndex.Value].SegmentId;
        }
        return segment.DefaultNext;
    }
}