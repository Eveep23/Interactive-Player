using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

public static class PreconditionChecker
{
    public static void CheckPreconditions(string infoPath, string savePath)
    {
        // Load JSON files
        JObject infoJson = JObject.Parse(File.ReadAllText(infoPath));
        JObject saveJson = JObject.Parse(File.ReadAllText(savePath));

        // Extract states
        var globalState = saveJson["GlobalState"] as JObject;
        var persistentState = saveJson["PersistentState"] as JObject;

        // Extract preconditions
        var value = infoJson["jsonGraph"]?["videos"]?.First?.First?["interactiveVideoMoments"]?["value"] as JObject;
        var preconditions = value?["preconditions"] as JObject;

        if (preconditions == null)
        {
            Console.WriteLine("Preconditions not found in the provided info.json file.");
            return;
        }

        Console.WriteLine("Preconditions:\n");

        // Dictionary to store precondition results
        var preconditionResults = new Dictionary<string, int>();

        // Evaluate and store preconditions that act as states
        foreach (var precondition in preconditions)
        {
            string preconditionId = precondition.Key;
            var preconditionLogic = precondition.Value;

            if (preconditionLogic[0].ToString() == "sum")
            {
                int result = EvaluateSum(preconditionLogic.Skip(1), persistentState, globalState);
                preconditionResults[preconditionId] = result;
            }
        }

        // Iterate through preconditions and evaluate them
        foreach (var precondition in preconditions)
        {
            string preconditionId = precondition.Key;
            var preconditionLogic = precondition.Value;

            bool result = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults);

            Console.WriteLine($"{preconditionId}: {(result ? "Met" : "Not Met")}");
        }
    }

    public static bool CheckPrecondition(string preconditionId, Dictionary<string, object> globalState, Dictionary<string, object> persistentState, string infoJsonFile)
    {
        // Load preconditions from the info JSON
        var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);

        if (preconditions == null || !preconditions.TryGetValue(preconditionId, out var preconditionLogic))
        {
            Console.WriteLine($"Precondition {preconditionId} not found.");
            return false;
        }

        // Evaluate the precondition
        return EvaluatePrecondition(preconditionLogic, JObject.FromObject(persistentState), JObject.FromObject(globalState), new Dictionary<string, int>());
    }

    private static Dictionary<string, JToken> LoadPreconditionsFromInfoJson(string infoJsonFile)
    {
        // Load the info JSON file
        JObject infoJson = JObject.Parse(File.ReadAllText(infoJsonFile));

        // Extract preconditions
        var value = infoJson["jsonGraph"]?["videos"]?.First?.First?["interactiveVideoMoments"]?["value"] as JObject;
        var preconditions = value?["preconditions"] as JObject;

        return preconditions?.ToObject<Dictionary<string, JToken>>();
    }

    static bool EvaluatePrecondition(JToken logic, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults)
    {
        string operation = logic[0].ToString();

        switch (operation)
        {
            case "eql":
                return EvaluateEquality(logic[1], logic[2], persistentState, globalState, preconditionResults);
            case "and":
                foreach (var subLogic in logic.Skip(1))
                {
                    if (!EvaluatePrecondition(subLogic, persistentState, globalState, preconditionResults))
                        return false;
                }
                return true;
            case "or":
                foreach (var subLogic in logic.Skip(1))
                {
                    if (EvaluatePrecondition(subLogic, persistentState, globalState, preconditionResults))
                        return true;
                }
                return false;
            case "not":
                return !EvaluatePrecondition(logic[1], persistentState, globalState, preconditionResults);
            case "lt":
                return EvaluateLessThan(logic[1], logic[2], persistentState, globalState, preconditionResults);
            case "gt":
                return EvaluateGreaterThan(logic[1], logic[2], persistentState, globalState, preconditionResults);
            case "lte":
                return EvaluateLessThanOrEqual(logic[1], logic[2], persistentState, globalState, preconditionResults);
            case "gte":
                return EvaluateGreaterThanOrEqual(logic[1], logic[2], persistentState, globalState, preconditionResults);
            case "sum":
                int sum = EvaluateSum(logic.Skip(1), persistentState, globalState);
                return sum > 0; // Adjust this condition as needed
            default:
                throw new NotImplementedException($"Unsupported operation: {operation}");
        }
    }

    static bool EvaluateEquality(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults)
    {
        string stateType = path[0].ToString();
        string key = path[1].ToString();

        JToken actualValue = null;
        if (stateType == "persistentState")
        {
            actualValue = persistentState?[key];
        }
        else if (stateType == "globalState")
        {
            actualValue = globalState?[key];
        }
        else if (stateType == "precondition")
        {
            if (preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                return preconditionValue == expectedValue.ToObject<int>();
            }
            else
            {
                throw new ArgumentException($"Unknown precondition: {key}");
            }
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState);
            return sum == expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return JToken.DeepEquals(actualValue, expectedValue);
    }

    static bool EvaluateLessThan(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults)
    {
        string stateType = path[0].ToString();
        string key = path[1].ToString();

        JToken actualValue = null;
        if (stateType == "persistentState")
        {
            actualValue = persistentState?[key];
        }
        else if (stateType == "globalState")
        {
            actualValue = globalState?[key];
        }
        else if (stateType == "precondition")
        {
            if (preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                return preconditionValue < expectedValue.ToObject<int>();
            }
            else
            {
                throw new ArgumentException($"Unknown precondition: {key}");
            }
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState);
            return sum < expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue < (int)expectedValue;
    }

    static bool EvaluateGreaterThan(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults)
    {
        string stateType = path[0].ToString();
        string key = path[1].ToString();

        JToken actualValue = null;
        if (stateType == "persistentState")
        {
            actualValue = persistentState?[key];
        }
        else if (stateType == "globalState")
        {
            actualValue = globalState?[key];
        }
        else if (stateType == "precondition")
        {
            if (preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                return preconditionValue > expectedValue.ToObject<int>();
            }
            else
            {
                throw new ArgumentException($"Unknown precondition: {key}");
            }
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState);
            return sum > expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue > (int)expectedValue;
    }

    static bool EvaluateLessThanOrEqual(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults)
    {
        string stateType = path[0].ToString();
        string key = path[1].ToString();

        JToken actualValue = null;
        if (stateType == "persistentState")
        {
            actualValue = persistentState?[key];
        }
        else if (stateType == "globalState")
        {
            actualValue = globalState?[key];
        }
        else if (stateType == "precondition")
        {
            if (preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                return preconditionValue <= expectedValue.ToObject<int>();
            }
            else
            {
                throw new ArgumentException($"Unknown precondition: {key}");
            }
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState);
            return sum <= expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue <= (int)expectedValue;
    }

    static bool EvaluateGreaterThanOrEqual(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults)
    {
        string stateType = path[0].ToString();
        string key = path[1].ToString();

        JToken actualValue = null;
        if (stateType == "persistentState")
        {
            actualValue = persistentState?[key];
        }
        else if (stateType == "globalState")
        {
            actualValue = globalState?[key];
        }
        else if (stateType == "precondition")
        {
            if (preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                return preconditionValue >= expectedValue.ToObject<int>();
            }
            else
            {
                throw new ArgumentException($"Unknown precondition: {key}");
            }
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState);
            return sum >= expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue >= (int)expectedValue;
    }

    static int EvaluateSum(IEnumerable<JToken> paths, JObject persistentState, JObject globalState)
    {
        int sum = 0;

        foreach (var path in paths)
        {
            string stateType = path[0].ToString();
            string key = path[1].ToString();

            JToken actualValue = null;
            if (stateType == "persistentState")
            {
                actualValue = persistentState?[key];
            }
            else if (stateType == "globalState")
            {
                actualValue = globalState?[key];
            }
            else
            {
                throw new ArgumentException($"Unknown state type: {stateType}");
            }

            if (actualValue != null && actualValue.Type == JTokenType.Integer)
            {
                sum += (int)actualValue;
            }
        }

        return sum;
    }
}
