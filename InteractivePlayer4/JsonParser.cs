using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using Newtonsoft.Json.Linq;
using LibVLCSharp.Shared;
using System.Threading;
using System.Net;
using Newtonsoft.Json;

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

    public static Dictionary<string, List<Moment>> ParseMoments(string jsonFile)
    {
        try
        {
            var json = JObject.Parse(File.ReadAllText(jsonFile));
            var video = json["jsonGraph"]?["videos"]?.First?.First;
            if (video == null) throw new Exception("Video node not found in info JSON.");

            var momentsBySegmentToken = video["interactiveVideoMoments"]?["value"]?["momentsBySegment"];
            if (momentsBySegmentToken == null) throw new Exception("MomentsBySegment node not found.");

            var momentsBySegment = new Dictionary<string, List<Moment>>();
            foreach (var property in momentsBySegmentToken.Children<JProperty>())
            {
                var segmentId = property.Name;
                var moments = property.Value.ToObject<List<Moment>>();
                momentsBySegment[segmentId] = moments;
            }

            Console.WriteLine($"Loaded moments for {momentsBySegment.Count} segments.");
            return momentsBySegment;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing info JSON: {ex.Message}");
            return null;
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

                    // Assign segmentId to each choice
                    if (segment.Choices != null)
                    {
                        foreach (var choice in segment.Choices)
                        {
                            if (string.IsNullOrEmpty(choice.SegmentId))
                            {
                                choice.SegmentId = choice.Id;
                            }

                            // Check if the segment exists, if not, trim it
                            if (!segments.ContainsKey(choice.SegmentId))
                            {
                                choice.SegmentId = TrimSegmentId(choice.SegmentId);
                            }
                        }
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

    public static string HandleSegment(MediaPlayer mediaPlayer, Segment segment, Dictionary<string, Segment> segments, string movieFolder)
    {
        mediaPlayer.Time = segment.StartTimeMs;
        string nextSegment = segment.DefaultNext;
        bool choiceDisplayed = false;

        string defaultButtonTexturePath = FindDefaultButtonTexture(movieFolder, segment.Choices ?? new List<Choice>());

        while (mediaPlayer.Time < segment.EndTimeMs)
        {
            if (!choiceDisplayed && segment.Choices != null && segment.Choices.Count > 0 &&
                mediaPlayer.Time >= segment.ChoiceDisplayTimeMs)
            {
                long choiceDurationMs = segment.HideChoiceTimeMs - segment.ChoiceDisplayTimeMs;

                if (choiceDurationMs <= 0 || choiceDurationMs > 30000)
                {
                    choiceDurationMs = 30000;
                }

                Console.WriteLine($"Choice point reached for segment {segment.Id}");

                // Load button sprites for each choice
                var buttonSprites = new List<Bitmap>();
                foreach (var choice in segment.Choices)
                {
                    string buttonSpritePath = null;

                    // Use specific button texture for this choice
                    string url = choice?.Background?.VisualStates?.Default?.Image?.Url;
                    if (!string.IsNullOrEmpty(url))
                    {
                        buttonSpritePath = Path.Combine(movieFolder, Path.GetFileName(new Uri(url).LocalPath));
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

                string selectedSegment = UIManager.ShowChoiceUI(segment.Choices, buttonSprites, (int)choiceDurationMs, movieFolder);

                if (!string.IsNullOrEmpty(selectedSegment))
                {
                    nextSegment = selectedSegment;
                }
                else
                {
                    Console.WriteLine("No choice made. Defaulting to the specified choice.");
                    nextSegment = GetDefaultChoice(segment);
                }

                choiceDisplayed = true;
            }

            Thread.Sleep(100);
            HandleKeyPress(mediaPlayer);
        }

        return nextSegment;
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
                    string localPath = Path.Combine(movieFolder, Path.GetFileName(new Uri(url).LocalPath));
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
        "button_sprite.png"
    };

        foreach (string textureName in potentialDefaultTextures)
        {
            string texturePath = Path.Combine(movieFolder, textureName);
            if (File.Exists(texturePath))
            {
                return texturePath;
            }
        }

        // If no texture is found
        Console.WriteLine("Default button texture not found in the movie folder.");
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

    private static void HandleKeyPress(MediaPlayer mediaPlayer)
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