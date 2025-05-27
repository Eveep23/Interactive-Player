using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

public static class PreconditionChecker
{
    public static int GetPreconditionValue(string preconditionId, Dictionary<string, object> globalState, Dictionary<string, object> persistentState, string infoJsonFile)
    {
        foreach (var key in new[] {
    "p_1b_f", "p_1c_f", "p_1e_f", "p_1h_f",
    "p_2b_f", "p_2c_f", "p_2d_f", "p_2e_f",
    "p_3a_f", "p_3d_f", "p_3e_f", "p_3f_f",
    "p_4a_f", "p_4e_f", "p_4f_f", "p_4g_f",
    "p_5a_f", "p_5b_f", "p_5c_f", "p_5d_f",
    "p_6a_f", "p_6b_f", "p_6c_f", "p_6d_f"
})
{
    if (persistentState.TryGetValue(key, out var value))
        Console.WriteLine($"{key}: {value}");
    else
        Console.WriteLine($"{key}: (missing)");
}
        
        if (preconditionId == "livesLost")
        {
            // List of persistentState keys to sum
            string[] keys = {
                "p_1b_f", "p_1c_f", "p_1e_f", "p_1h_f",
                "p_2b_f", "p_2c_f", "p_2d_f", "p_2e_f",
                "p_3a_f", "p_3d_f", "p_3e_f", "p_3f_f",
                "p_4a_f", "p_4e_f", "p_4f_f", "p_4g_f",
                "p_5a_f", "p_5b_f", "p_5c_f", "p_5d_f",
                "p_6a_f", "p_6b_f", "p_6c_f", "p_6d_f"
            };
            int sum = 0;
            foreach (var key in keys)
            {
                if (persistentState.TryGetValue(key, out var value))
                {
                    if (value is int intValue)
                        sum += intValue;
                    else if (value is long longValue)
                        sum += (int)longValue;
                    else if (value is string strValue && int.TryParse(strValue, out int parsed))
                        sum += parsed;
                }
            }
            return sum;
        }
        else if (preconditionId == "livesRemaining")
        {
            // livesRemaining = (livesLost - 3) * -1 = 3 - livesLost
            int livesLost = GetPreconditionValue("livesLost", globalState, persistentState, infoJsonFile);
            return 3 - livesLost;
        }
        else
        {
            return 0;
        }
    }

    public static bool CheckPrecondition(string preconditionId, Dictionary<string, object> globalState, Dictionary<string, object> persistentState, string infoJsonFile)
    {
        if (string.IsNullOrEmpty(preconditionId))
        {
            Console.WriteLine("Precondition ID is null or empty.");
            return false;
        }

        // Load preconditions from the info JSON
        var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);

        if (preconditions == null || !preconditions.TryGetValue(preconditionId, out var preconditionLogic))
        {
            Console.WriteLine($"Precondition {preconditionId} not found.");
            return false;
        }

        // Dictionary to store precondition results
        var preconditionResults = new Dictionary<string, int>();

        // Evaluate and store preconditions that act as states (like sums) first
        foreach (var precondition in preconditions)
        {
            string preconditionKey = precondition.Key;
            var preconditionValue = precondition.Value;

            if (preconditionValue[0].ToString() == "sum")
            {
                int result = EvaluateSum(preconditionValue.Skip(1), JObject.FromObject(persistentState), JObject.FromObject(globalState), preconditionResults, infoJsonFile);
                preconditionResults[preconditionKey] = result;
            }
        }

        // Iterate through preconditions and evaluate them
        foreach (var precondition in preconditions)
        {
            string preconditionKey = precondition.Key;
            var preconditionValue = precondition.Value;

            bool result = EvaluatePrecondition(preconditionValue, JObject.FromObject(persistentState), JObject.FromObject(globalState), preconditionResults, infoJsonFile);

            if (preconditionValue[0].ToString() != "sum")
            {
                preconditionResults[preconditionKey] = result ? 1 : 0; // Assuming precondition result is boolean
            }
        }

        // Check the specific precondition
        if (!preconditionResults.TryGetValue(preconditionId, out int preconditionResult))
        {
            Console.WriteLine($"Precondition {preconditionId} not evaluated.");
            return false;
        }

        return preconditionResult == 1;
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

    static bool EvaluatePrecondition(JToken logic, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
    {
        string operation = logic[0].ToString();

        switch (operation)
        {
            case "eql":
                return EvaluateEquality(logic[1], logic[2], persistentState, globalState, preconditionResults, infoJsonFile);
            case "and":
                foreach (var subLogic in logic.Skip(1))
                {
                    if (!EvaluatePrecondition(subLogic, persistentState, globalState, preconditionResults, infoJsonFile))
                        return false;
                }
                return true;
            case "or":
                foreach (var subLogic in logic.Skip(1))
                {
                    if (EvaluatePrecondition(subLogic, persistentState, globalState, preconditionResults, infoJsonFile))
                        return true;
                }
                return false;
            case "not":
                return !EvaluatePrecondition(logic[1], persistentState, globalState, preconditionResults, infoJsonFile);
            case "lt":
                return EvaluateLessThan(logic[1], logic[2], persistentState, globalState, preconditionResults, infoJsonFile);
            case "gt":
                return EvaluateGreaterThan(logic[1], logic[2], persistentState, globalState, preconditionResults, infoJsonFile);
            case "lte":
                return EvaluateLessThanOrEqual(logic[1], logic[2], persistentState, globalState, preconditionResults, infoJsonFile);
            case "gte":
                return EvaluateGreaterThanOrEqual(logic[1], logic[2], persistentState, globalState, preconditionResults, infoJsonFile);
            case "sum":
                int sum = EvaluateSum(logic.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
                preconditionResults[logic[1].ToString()] = sum; // Store the sum result in preconditionResults
                return true; // Sum operation itself is always true
            case "mult":
                int mult = EvaluateMultiplication(logic.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
                preconditionResults[logic[1].ToString()] = mult; // Store the multiplication result in preconditionResults
                return true; // Multiplication operation itself is always true
            default:
                throw new NotImplementedException($"Unsupported operation: {operation}");
        }
    }

    static int EvaluateMultiplication(IEnumerable<JToken> operands, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
    {
        int result = 1;

        foreach (var operand in operands)
        {
            if (operand.Type == JTokenType.Integer || operand.Type == JTokenType.Float)
            {
                result *= operand.ToObject<int>();
            }
            else if (operand.Type == JTokenType.Array)
            {
                var subOperation = operand[0].ToString();
                if (subOperation == "sum")
                {
                    result *= EvaluateSum(operand.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
                }
                else if (subOperation == "precondition")
                {
                    string preconditionKey = operand[1].ToString();
                    if (!preconditionResults.TryGetValue(preconditionKey, out int preconditionValue))
                    {
                        var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);
                        if (preconditions != null && preconditions.TryGetValue(preconditionKey, out var preconditionLogic))
                        {
                            bool preconditionResult = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults, infoJsonFile);
                            preconditionValue = preconditionResult ? 1 : 0; // Assuming precondition result is boolean
                            preconditionResults[preconditionKey] = preconditionValue;
                        }
                        else
                        {
                            throw new ArgumentException($"Unknown precondition: {preconditionKey}");
                        }
                    }
                    result *= preconditionValue;
                }
                else
                {
                    throw new ArgumentException($"Unsupported sub-operation in multiplication: {subOperation}");
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported operand type in multiplication: {operand.Type}");
            }
        }

        return result;
    }

    static bool EvaluateEquality(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
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
            if (!preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                // Evaluate the precondition if it hasn't been evaluated yet
                var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);
                if (preconditions != null && preconditions.TryGetValue(key, out var preconditionLogic))
                {
                    bool result = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults, infoJsonFile);
                    preconditionValue = result ? 1 : 0; // Assuming precondition result is boolean
                    preconditionResults[key] = preconditionValue;
                }
                else
                {
                    throw new ArgumentException($"Unknown precondition: {key}");
                }
            }
            return preconditionValue == expectedValue.ToObject<int>();
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
            return sum == expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return JToken.DeepEquals(actualValue, expectedValue);
    }

    static bool EvaluateLessThan(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
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
            if (!preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                // Evaluate the precondition if it hasn't been evaluated yet
                var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);
                if (preconditions != null && preconditions.TryGetValue(key, out var preconditionLogic))
                {
                    bool result = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults, infoJsonFile);
                    preconditionValue = result ? 1 : 0; // Assuming precondition result is boolean
                    preconditionResults[key] = preconditionValue;
                }
                else
                {
                    throw new ArgumentException($"Unknown precondition: {key}");
                }
            }
            return preconditionValue < expectedValue.ToObject<int>();
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
            return sum < expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue < (int)expectedValue;
    }

    static bool EvaluateGreaterThan(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
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
            if (!preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                // Evaluate the precondition if it hasn't been evaluated yet
                var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);
                if (preconditions != null && preconditions.TryGetValue(key, out var preconditionLogic))
                {
                    bool result = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults, infoJsonFile);
                    preconditionValue = result ? 1 : 0; // Assuming precondition result is boolean
                    preconditionResults[key] = preconditionValue;
                }
                else
                {
                    throw new ArgumentException($"Unknown precondition: {key}");
                }
            }
            return preconditionValue > expectedValue.ToObject<int>();
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
            return sum > expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue > (int)expectedValue;
    }

    static bool EvaluateLessThanOrEqual(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
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
            if (!preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                // Evaluate the precondition if it hasn't been evaluated yet
                var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);
                if (preconditions != null && preconditions.TryGetValue(key, out var preconditionLogic))
                {
                    bool result = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults, infoJsonFile);
                    preconditionValue = result ? 1 : 0; // Assuming precondition result is boolean
                    preconditionResults[key] = preconditionValue;
                }
                else
                {
                    throw new ArgumentException($"Unknown precondition: {key}");
                }
            }
            return preconditionValue <= expectedValue.ToObject<int>();
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
            return sum <= expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue <= (int)expectedValue;
    }

    static bool EvaluateGreaterThanOrEqual(JToken path, JToken expectedValue, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
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
            if (!preconditionResults.TryGetValue(key, out int preconditionValue))
            {
                // Evaluate the precondition if it hasn't been evaluated yet
                var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);
                if (preconditions != null && preconditions.TryGetValue(key, out var preconditionLogic))
                {
                    bool result = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults, infoJsonFile);
                    preconditionValue = result ? 1 : 0; // Assuming precondition result is boolean
                    preconditionResults[key] = preconditionValue;
                }
                else
                {
                    throw new ArgumentException($"Unknown precondition: {key}");
                }
            }
            return preconditionValue >= expectedValue.ToObject<int>();
        }
        else if (stateType == "sum")
        {
            int sum = EvaluateSum(path.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
            return sum >= expectedValue.ToObject<int>();
        }
        else
        {
            throw new ArgumentException($"Unknown state type: {stateType}");
        }

        return actualValue != null && actualValue.Type == JTokenType.Integer && expectedValue.Type == JTokenType.Integer && (int)actualValue >= (int)expectedValue;
    }

    static int EvaluateSum(IEnumerable<JToken> paths, JObject persistentState, JObject globalState, Dictionary<string, int> preconditionResults, string infoJsonFile)
    {
        int sum = 0;

        foreach (var path in paths)
        {
            if (path.Type == JTokenType.Integer || path.Type == JTokenType.Float)
            {
                sum += path.ToObject<int>();
            }
            else if (path.Type == JTokenType.Array)
            {
                var arr = (JArray)path;
                if (arr.Count == 0)
                    continue;

                string stateType = arr[0].ToString();

                if (stateType == "persistentState" || stateType == "globalState")
                {
                    string key = arr[1].ToString();
                    JToken actualValue = stateType == "persistentState" ? persistentState?[key] : globalState?[key];
                    if (actualValue != null && actualValue.Type == JTokenType.Integer)
                        sum += (int)actualValue;
                }
                else if (stateType == "precondition")
                {
                    string preconditionKey = arr[1].ToString();
                    if (!preconditionResults.TryGetValue(preconditionKey, out int preconditionValue))
                    {
                        var preconditions = LoadPreconditionsFromInfoJson(infoJsonFile);
                        if (preconditions != null && preconditions.TryGetValue(preconditionKey, out var preconditionLogic))
                        {
                            // Recursively evaluate the referenced precondition (can be sum, mult, etc.)
                            if (preconditionLogic[0].ToString() == "sum")
                                preconditionValue = EvaluateSum(preconditionLogic.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
                            else if (preconditionLogic[0].ToString() == "mult")
                                preconditionValue = EvaluateMultiplication(preconditionLogic.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
                            else
                                preconditionValue = EvaluatePrecondition(preconditionLogic, persistentState, globalState, preconditionResults, infoJsonFile) ? 1 : 0;
                            preconditionResults[preconditionKey] = preconditionValue;
                        }
                        else
                        {
                            throw new ArgumentException($"Unknown precondition: {preconditionKey}");
                        }
                    }
                    sum += preconditionValue;
                }
                else if (stateType == "sum")
                {
                    // Nested sum
                    sum += EvaluateSum(arr.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
                }
                else if (stateType == "mult")
                {
                    // Nested multiplication
                    sum += EvaluateMultiplication(arr.Skip(1), persistentState, globalState, preconditionResults, infoJsonFile);
                }
                else
                {
                    throw new ArgumentException($"Unknown state type: {stateType}");
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported operand type in sum: {path.Type}");
            }
        }

        return sum;
    }
}
