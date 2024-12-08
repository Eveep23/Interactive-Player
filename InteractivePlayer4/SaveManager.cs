using System;
using System.IO;
using Newtonsoft.Json;

public static class SaveManager
{
    public static string LoadSaveFile(string saveFilePath)
    {
        if (File.Exists(saveFilePath))
        {
            Console.WriteLine("Save file detected. Would you like to:");
            Console.WriteLine("1: Continue where you left off");
            Console.WriteLine("2: Restart the Interactive");

            Console.Write("Enter your choice: ");
            if (int.TryParse(Console.ReadLine(), out int choice))
            {
                if (choice == 1)
                {
                    var saveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(saveFilePath));
                    return saveData.CurrentSegment;
                }
                else if (choice == 2)
                {
                    Console.WriteLine("Restarting the Interactive...");
                    return null;
                }
            }

            Console.WriteLine("Invalid choice. Restarting the Interactive...");
        }
        return null;
    }

    public static void SaveProgress(string saveFilePath, string currentSegment)
    {
        var saveData = new SaveData { CurrentSegment = currentSegment };
        File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(saveData, Formatting.Indented));
    }
}