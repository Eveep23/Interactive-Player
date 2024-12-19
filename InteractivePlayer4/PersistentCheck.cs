using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

class PersistentCheck
{
    static void Main2(string[] args)
    {
        string infoJsonFilePath = "info.json";

        if (!File.Exists(infoJsonFilePath))
        {
            Console.WriteLine("JSON file not found.");
            return;
        }

        try
        {
            string jsonContent = File.ReadAllText(infoJsonFilePath);
            JObject jsonData = JObject.Parse(jsonContent);

            var persistentStates = jsonData["jsonGraph"]?["videos"]?.First?.First?["interactiveVideoMoments"]?["value"]?["stateHistory"]?["persistent"];
            var segments = jsonData["jsonGraph"]?["videos"]?.First?.First?["interactiveVideoMoments"]?["value"]?["momentsBySegment"];
            var preconditions = jsonData["jsonGraph"]?["videos"]?.First?.First?["interactiveVideoMoments"]?["value"]?["preconditions"];

            Console.WriteLine("\nPersistent States:");
            foreach (var state in persistentStates)
            {
                Console.WriteLine($"  {state.Path}: {state.First}");
            }

            Console.WriteLine("\nSegments and Their Preconditions:");
            foreach (var segment in segments)
            {
                string segmentId = ((JProperty)segment).Name;
                var moments = segment.First;

                Console.WriteLine($"\nSegment: {segmentId}");
                foreach (var moment in moments)
                {
                    int startTime = moment["startMs"]?.ToObject<int>() ?? 0;
                    int endTime = moment["endMs"]?.ToObject<int>() ?? 0;
                    Console.WriteLine($"  Time Range: {startTime} ms - {endTime} ms");

                    // Display Preconditions
                    var precondition = moment["precondition"];
                    if (precondition != null)
                    {
                        string preconditionDescription = TranslateCondition(precondition, preconditions);
                        Console.WriteLine($"  Preconditions: {preconditionDescription}");
                    }

                    // Choices
                    var choices = moment["choices"];
                    if (choices != null)
                    {
                        Console.WriteLine("  Choices:");
                        foreach (var choice in choices)
                        {
                            string choiceText = choice["text"]?.ToString() ?? "No Text";
                            string nextSegment = choice["segmentId"]?.ToString() ?? "No Segment";
                            Console.WriteLine($"    - {choiceText} -> {nextSegment}");

                            var choicePrecondition = choice["precondition"];
                            if (choicePrecondition != null)
                            {
                                string choiceConditionDescription = TranslateCondition(choicePrecondition, preconditions);
                                Console.WriteLine($"      Preconditions: {choiceConditionDescription}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing the JSON: {ex.Message}");
        }
    }

    static string TranslateCondition(JToken condition, JToken preconditions)
    {
        if (condition == null) return "No condition";

        string type = condition.Type.ToString();
        switch (type)
        {
            case "String":
                return condition.ToString();

            case "Array":
                var array = (JArray)condition;
                string operatorType = array[0]?.ToString();
                switch (operatorType)
                {
                    case "eql":
                        return $"{TranslateCondition(array[1], preconditions)} equals {TranslateCondition(array[2], preconditions)}";
                    case "not":
                        return $"NOT ({TranslateCondition(array[1], preconditions)})";
                    case "and":
                        return $"({string.Join(" AND ", array.Skip(1).Select(cond => TranslateCondition(cond, preconditions)))})";
                    case "or":
                        return $"({string.Join(" OR ", array.Skip(1).Select(cond => TranslateCondition(cond, preconditions)))})";
                    default:
                        return $"Unknown operator {operatorType}";
                }

            default:
                return condition.ToString();
        }
    }
}