using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public static class SaveManager
{
    public static string SelectedMovieFolder { get; private set; }

    public static void SaveSnapshot(string saveFilePath)
    {
        string movieFolder = Path.GetDirectoryName(saveFilePath);
        string snapshotsPath = Path.Combine(movieFolder, "snapshots.json");

        // Load current save data
        if (!File.Exists(saveFilePath))
            return;

        var currentSave = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(saveFilePath));

        // Load or create the snapshots file
        Dictionary<string, SaveData> snapshots;
        if (File.Exists(snapshotsPath))
        {
            snapshots = JsonConvert.DeserializeObject<Dictionary<string, SaveData>>(File.ReadAllText(snapshotsPath));
        }
        else
        {
            snapshots = new Dictionary<string, SaveData>();
        }

        // Find the next available snapshot key
        int nextIndex = 1;
        while (snapshots.ContainsKey($"snapshot{nextIndex}"))
            nextIndex++;

        string snapshotKey = $"snapshot{nextIndex}";
        snapshots[snapshotKey] = currentSave;

        // Save back to snapshots.json
        File.WriteAllText(snapshotsPath, JsonConvert.SerializeObject(snapshots, Formatting.Indented));
    }

    public static string LoadSaveFile(string saveFilePath, bool skipContinueMenu = false)
    {
        if (!File.Exists(saveFilePath))
            return null;

        string movieFolder = Path.GetDirectoryName(saveFilePath) ?? "";
        bool isBKorTQ = movieFolder.Contains("Battle Kitty") || movieFolder.Contains("Trivia Quest");
        string specificSaveFilePath = null;
        if (isBKorTQ)
        {
            string folder = movieFolder.Contains("Battle Kitty") ? "BK" : "TQ";
            specificSaveFilePath = Path.Combine(Directory.GetCurrentDirectory(), folder, folder == "BK" ? "bk_save.json" : "tq_save.json");
        }

        // If caller asked to skip the continue menu, read from the central BK/TQ save if present, otherwise from the episode save
        string effectiveSaveFilePath = saveFilePath;
        if (isBKorTQ && specificSaveFilePath != null && File.Exists(specificSaveFilePath))
        {
            effectiveSaveFilePath = specificSaveFilePath;
        }

        if (!File.Exists(effectiveSaveFilePath))
            return null;

        if (skipContinueMenu)
        {
            var saveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(effectiveSaveFilePath));
            SelectedMovieFolder = saveData.CurrentSegment;
            return SelectedMovieFolder;
        }

            string defaultBackdropPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Save_backdrop.png");
            string topBarPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Top_bar.png");
            string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Interactive_player_logo.png");

            Form form = new Form
            {
                Text = "Interactive Player",
                Size = new Size(500, 400),
                StartPosition = FormStartPosition.CenterScreen,
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                BackColor = ColorTranslator.FromHtml("#141414")
            };

            Panel topBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackgroundImage = Image.FromFile(topBarPath),
                BackgroundImageLayout = ImageLayout.Stretch
            };

            PictureBox logoPictureBox = new PictureBox
            {
                Image = Image.FromFile(logoPath),
                SizeMode = PictureBoxSizeMode.AutoSize,
                BackColor = Color.Transparent
            };

            topBarPanel.Controls.Add(logoPictureBox);
            logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
            topBarPanel.Resize += (sender, e) =>
            {
                logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
            };

            form.Controls.Add(topBarPanel);

            Button continueButton = new Button
            {
                Width = 334,
                Height = 60,
                BackgroundImage = Image.FromFile(Path.Combine(Directory.GetCurrentDirectory(), "general", "Continue_Button.png")),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };

            continueButton.Click += (sender, e) =>
            {
                var saveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(saveFilePath));
                form.DialogResult = DialogResult.OK;
                form.Close();
                SelectedMovieFolder = saveData.CurrentSegment;
            };

            Button restartButton = new Button
            {
                Width = 334,
                Height = 60,
                BackgroundImage = Image.FromFile(Path.Combine(Directory.GetCurrentDirectory(), "general", "Restart_Button.png")),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };

            restartButton.Click += (sender, e) =>
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
                SelectedMovieFolder = null;
            };

            TableLayoutPanel buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = false
            };

            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60F)); // Top spacing
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Continue button
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Restart button
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Bottom spacing

            buttonPanel.Controls.Add(continueButton, 0, 1);
            buttonPanel.Controls.Add(restartButton, 0, 2);

            continueButton.Anchor = AnchorStyles.None;
            restartButton.Anchor = AnchorStyles.None;

            form.Controls.Add(buttonPanel);

            form.FormClosed += (sender, e) =>
            {
                if (form.DialogResult == DialogResult.Cancel)
                {
                    Utilities.ShowMovieSelectionMenu();
                }
            };

            return form.ShowDialog() == DialogResult.OK ? SelectedMovieFolder : null;
    }

    public static SaveData LoadSaveData(string saveFilePath)
    {
        string movieFolder = Path.GetDirectoryName(saveFilePath) ?? "";
        bool isBKorTQ = movieFolder.Contains("Battle Kitty") || movieFolder.Contains("Trivia Quest");
        string specificSaveFilePath = null;
        if (isBKorTQ)
        {
            string folder = movieFolder.Contains("Battle Kitty") ? "BK" : "TQ";
            specificSaveFilePath = Path.Combine(Directory.GetCurrentDirectory(), folder, folder == "BK" ? "bk_save.json" : "tq_save.json");
        }

        string effectiveSaveFilePath = saveFilePath;
        if (isBKorTQ && specificSaveFilePath != null && File.Exists(specificSaveFilePath))
        {
            effectiveSaveFilePath = specificSaveFilePath;
        }

        if (File.Exists(effectiveSaveFilePath))
        {
            return JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(effectiveSaveFilePath));
        }
        return null;
    }

    public static void SaveProgress(string saveFilePath, string currentSegment, Dictionary<string, object> globalState, Dictionary<string, object> persistentState)
    {
        SaveData saveData;

        string movieFolder = Path.GetDirectoryName(saveFilePath) ?? "";
        bool isTQ = movieFolder.Contains("Trivia Quest");
        bool isBK = movieFolder.Contains("Battle Kitty");

        string specificSaveFilePath = null;
        if (isBK || isTQ)
        {
            string folder = movieFolder.Contains("Battle Kitty") ? "BK" : "TQ";
            specificSaveFilePath = Path.Combine(Directory.GetCurrentDirectory(), folder, folder == "BK" ? "bk_save.json" : "tq_save.json");
        }

        if (File.Exists(saveFilePath))
        {
            // Load existing save data
            saveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(saveFilePath));

            // Update the current segment
            saveData.CurrentSegment = currentSegment;
            saveData.GlobalState = globalState;
            saveData.PersistentState = persistentState;

            // Save the updated save data to the file
            File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(saveData, Formatting.Indented));
        }
        else
        {
            // Check if the movie folder is "Minecraft Story Mode Ep2", "Minecraft Story Mode Ep3", "Minecraft Story Mode Ep4", or "Minecraft Story Mode Ep5"
            if (movieFolder.EndsWith("Minecraft Story Mode Ep2") || movieFolder.EndsWith("Minecraft Story Mode Ep3") || movieFolder.EndsWith("Minecraft Story Mode Ep4") || movieFolder.EndsWith("Minecraft Story Mode Ep5"))
            {
                // Look for the save file in "Minecraft Story Mode Ep1"
                string previousEpisodeFolder = Path.Combine(Directory.GetParent(movieFolder).FullName, "Minecraft Story Mode Ep1");
                string previousSaveFilePath = Path.Combine(previousEpisodeFolder, "save.json");

                if (File.Exists(previousSaveFilePath))
                {
                    // Load the persistent state from the previous episode's save file
                    var previousSaveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(previousSaveFilePath));
                    foreach (var kvp in previousSaveData.PersistentState)
                    {
                        if (persistentState.ContainsKey(kvp.Key))
                        {
                            persistentState[kvp.Key] = kvp.Value;
                        }
                    }
                }

                // Additional check for "Minecraft Story Mode Ep4" to retrieve "Armor" state from "Minecraft Story Mode Ep3"
                if (movieFolder.EndsWith("Minecraft Story Mode Ep4"))
                {
                    string ep3Folder = Path.Combine(Directory.GetParent(movieFolder).FullName, "Minecraft Story Mode Ep3");
                    string ep3SaveFilePath = Path.Combine(ep3Folder, "save.json");

                    if (File.Exists(ep3SaveFilePath))
                    {
                        var ep3SaveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(ep3SaveFilePath));
                        if (ep3SaveData.PersistentState.ContainsKey("Armor"))
                        {
                            persistentState["Armor"] = ep3SaveData.PersistentState["Armor"];
                        }
                    }
                }
            }

            if (Path.GetFileName(movieFolder).Equals("Headspace Unwind Your Mind", StringComparison.OrdinalIgnoreCase))
            {
                string[] keysToShuffle = { "p_s2d", "p_s2c", "p_s2b", "p_s2a" };
                var rng = new Random();

                foreach (var key in keysToShuffle)
                {
                    if (persistentState.ContainsKey(key) && persistentState[key] is Newtonsoft.Json.Linq.JArray arr)
                    {
                        // Convert to list, shuffle, and assign back
                        var list = arr.ToObject<List<string>>();
                        int n = list.Count;
                        while (n > 1)
                        {
                            n--;
                            int k = rng.Next(n + 1);
                            var value = list[k];
                            list[k] = list[n];
                            list[n] = value;
                        }
                        persistentState[key] = new Newtonsoft.Json.Linq.JArray(list);
                    }
                    else if (persistentState.ContainsKey(key) && persistentState[key] is List<object> objList)
                    {
                        // If it's a List<object>, try to shuffle as strings
                        var strList = objList.Select(o => o.ToString()).ToList();
                        int n = strList.Count;
                        while (n > 1)
                        {
                            n--;
                            int k = rng.Next(n + 1);
                            var value = strList[k];
                            strList[k] = strList[n];
                            strList[n] = value;
                        }
                        persistentState[key] = strList;
                    }
                }
            }

            // Create new save data
            saveData = new SaveData
            {
                CurrentSegment = currentSegment,
                GlobalState = globalState,
                PersistentState = persistentState
            };

            // Save the new save data to the file
            File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(saveData, Formatting.Indented));
        }

        if (isTQ && specificSaveFilePath != null)
        {
            try
            {
                // Load existing central save (if any) and merge so we don't lose keys from other episodes
                SaveData existingCentral = null;
                if (File.Exists(specificSaveFilePath))
                {
                    try
                    {
                        existingCentral = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(specificSaveFilePath));
                    }
                    catch
                    {
                        existingCentral = null;
                    }
                }

                if (existingCentral == null)
                {
                    existingCentral = new SaveData
                    {
                        GlobalState = new Dictionary<string, object>(),
                        PersistentState = new Dictionary<string, object>()
                    };
                }
                if (existingCentral.GlobalState == null) existingCentral.GlobalState = new Dictionary<string, object>();
                if (existingCentral.PersistentState == null) existingCentral.PersistentState = new Dictionary<string, object>();

                string episodeSavedKey;
                string folderName = Path.GetFileName(movieFolder) ?? "";
                Match m = Regex.Match(folderName, @"Ep(?:isode)?\s*(\d{1,2})", RegexOptions.IgnoreCase);
                if (!m.Success) m = Regex.Match(folderName, @"\bE(\d{1,2})\b", RegexOptions.IgnoreCase);
                if (!m.Success) m = Regex.Match(folderName, @"\bEp(\d{1,2})\b", RegexOptions.IgnoreCase);
                if (m.Success)
                    episodeSavedKey = $"Ep{m.Groups[1].Value}Saved";
                else
                {
                    var sanitized = Regex.Replace(folderName, @"[^\w]", "_");
                    episodeSavedKey = $"{sanitized}_Saved";
                }

                bool alreadyMarked = false;
                if (existingCentral.PersistentState.TryGetValue(episodeSavedKey, out var flagObj))
                {
                    if (flagObj is bool b && b) alreadyMarked = true;
                    else if (flagObj is string s && bool.TryParse(s, out bool parsed)) alreadyMarked = parsed;
                }

                if (!alreadyMarked)
                {
                    if (persistentState != null)
                    {
                        foreach (var kvp in persistentState)
                        {
                            if (!existingCentral.PersistentState.ContainsKey(kvp.Key))
                                existingCentral.PersistentState[kvp.Key] = kvp.Value;
                        }
                    }

                    if (globalState != null)
                    {
                        foreach (var kvp in globalState)
                        {
                            if (!existingCentral.GlobalState.ContainsKey(kvp.Key))
                                existingCentral.GlobalState[kvp.Key] = kvp.Value;
                        }
                    }

                    try
                    {
                        string infoJsonPath = Path.Combine(movieFolder, "info.json");
                        if (File.Exists(infoJsonPath))
                        {
                            var infoJson = JObject.Parse(File.ReadAllText(infoJsonPath));
                            var video = infoJson["jsonGraph"]?["videos"]?.First?.First;
                            var stateHistory = video?["interactiveVideoMoments"]?["value"]?["stateHistory"];

                            var initialPersistent = stateHistory?["persistent"]?.ToObject<Dictionary<string, object>>();
                            var initialGlobal = stateHistory?["global"]?.ToObject<Dictionary<string, object>>();

                            if (initialPersistent != null)
                            {
                                foreach (var kvp in initialPersistent)
                                {
                                    if (!existingCentral.PersistentState.ContainsKey(kvp.Key))
                                        existingCentral.PersistentState[kvp.Key] = kvp.Value;
                                }
                            }

                            if (initialGlobal != null)
                            {
                                foreach (var kvp in initialGlobal)
                                {
                                    if (!existingCentral.GlobalState.ContainsKey(kvp.Key))
                                        existingCentral.GlobalState[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                    catch
                    { }

                    // Mark the episode as processed so this isn't repeated.
                    existingCentral.PersistentState[episodeSavedKey] = true;
                }

                // Merge global state (preserve existing keys not present in the current episode)
                var mergedGlobal = new Dictionary<string, object>(existingCentral.GlobalState);
                if (globalState != null)
                {
                    foreach (var kvp in globalState)
                    {
                        mergedGlobal[kvp.Key] = kvp.Value;
                    }
                }

                // Merge persistent state (preserve existing keys not present in the current episode)
                var mergedPersistent = new Dictionary<string, object>(existingCentral.PersistentState);
                if (persistentState != null)
                {
                    foreach (var kvp in persistentState)
                    {
                        mergedPersistent[kvp.Key] = kvp.Value;
                    }
                }

                // Decide CurrentSegment for central save: prefer existing, fallback to provided
                string centralSegment = existingCentral.CurrentSegment;
                if (string.IsNullOrEmpty(centralSegment) && !string.IsNullOrEmpty(currentSegment))
                    centralSegment = currentSegment;

                var specificSaveData = new SaveData
                {
                    CurrentSegment = centralSegment,
                    GlobalState = mergedGlobal,
                    PersistentState = mergedPersistent
                };

                // Ensure directory exists
                var dir = Path.GetDirectoryName(specificSaveFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(specificSaveFilePath, JsonConvert.SerializeObject(specificSaveData, Formatting.Indented));
            }
            catch
            { }
        }

        // Save the states to the Battle Kitty or Trivia Quest save file if applicable
        if (isBK && specificSaveFilePath != null)
        {
            try
            {
                // Load existing central save (if any) and merge so we don't lose keys from other episodes
                SaveData existingCentral = null;
                if (File.Exists(specificSaveFilePath))
                {
                    try
                    {
                        existingCentral = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(specificSaveFilePath));
                    }
                    catch
                    {
                        existingCentral = null;
                    }
                }

                if (existingCentral == null)
                {
                    existingCentral = new SaveData
                    {
                        GlobalState = new Dictionary<string, object>(),
                        PersistentState = new Dictionary<string, object>()
                    };
                }
                if (existingCentral.GlobalState == null) existingCentral.GlobalState = new Dictionary<string, object>();
                if (existingCentral.PersistentState == null) existingCentral.PersistentState = new Dictionary<string, object>();

                // Merge global state (preserve existing keys not present in the current episode)
                var mergedGlobal = new Dictionary<string, object>(existingCentral.GlobalState);
                if (globalState != null)
                {
                    foreach (var kvp in globalState)
                    {
                        mergedGlobal[kvp.Key] = kvp.Value;
                    }
                }

                // Merge persistent state (preserve existing keys not present in the current episode)
                var mergedPersistent = new Dictionary<string, object>(existingCentral.PersistentState);
                if (persistentState != null)
                {
                    foreach (var kvp in persistentState)
                    {
                        mergedPersistent[kvp.Key] = kvp.Value;
                    }
                }

                // Decide CurrentSegment for central save: prefer existing, fallback to provided
                string centralSegment = existingCentral.CurrentSegment;
                if (string.IsNullOrEmpty(centralSegment) && !string.IsNullOrEmpty(currentSegment))
                    centralSegment = currentSegment;

                var specificSaveData = new SaveData
                {
                    CurrentSegment = centralSegment,
                    GlobalState = mergedGlobal,
                    PersistentState = mergedPersistent
                };

                // Ensure directory exists
                var dir = Path.GetDirectoryName(specificSaveFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(specificSaveFilePath, JsonConvert.SerializeObject(specificSaveData, Formatting.Indented));
            }
            catch
            { }
        }
    }
}
