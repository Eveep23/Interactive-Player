﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

public static class SaveManager
{
    public static string SelectedMovieFolder { get; private set; }

    public static string LoadSaveFile(string saveFilePath)
    {
        if (File.Exists(saveFilePath))
        {
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

            // Adjust the TableLayoutPanel for better vertical positioning of buttons
            TableLayoutPanel buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = false
            };

            // Set the row and column styles to position the buttons higher
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60F)); // Top spacing
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Continue button
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Restart button
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Bottom spacing

            // Add the buttons to the TableLayoutPanel
            buttonPanel.Controls.Add(continueButton, 0, 1);
            buttonPanel.Controls.Add(restartButton, 0, 2);

            // Center the buttons horizontally by setting their Anchor property
            continueButton.Anchor = AnchorStyles.None;
            restartButton.Anchor = AnchorStyles.None;

            // Add the TableLayoutPanel to the form
            form.Controls.Add(buttonPanel);


            form.FormClosed += (sender, e) =>
            {
                if (form.DialogResult == DialogResult.Cancel)
                {
                    Utilities.ShowMovieSelectionMenu();
                }
            };

            // Check if the folder name contains "Battle Kitty" or "Trivia Quest"
            if (Path.GetDirectoryName(saveFilePath).Contains("Battle Kitty") || Path.GetDirectoryName(saveFilePath).Contains("Trivia Quest"))
            {
                string folder = Path.GetDirectoryName(saveFilePath).Contains("Battle Kitty") ? "BK" : "TQ";
                string specificSaveFilePath = Path.Combine(Directory.GetCurrentDirectory(), folder, folder == "BK" ? "bk_save.json" : "tq_save.json");

                if (File.Exists(specificSaveFilePath))
                {
                    var saveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(specificSaveFilePath));
                    var currentSaveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(saveFilePath));

                    // Merge states
                    foreach (var kvp in saveData.GlobalState)
                    {
                        currentSaveData.GlobalState[kvp.Key] = kvp.Value;
                    }
                    foreach (var kvp in saveData.PersistentState)
                    {
                        currentSaveData.PersistentState[kvp.Key] = kvp.Value;
                    }

                    // Save the merged data back to the current save file
                    File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(currentSaveData, Formatting.Indented));
                }
            }

            return form.ShowDialog() == DialogResult.OK ? SelectedMovieFolder : null;
        }
        return null;
    }

    public static SaveData LoadSaveData(string saveFilePath)
    {
        if (File.Exists(saveFilePath))
        {
            return JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(saveFilePath));
        }
        return null;
    }

    public static void SaveProgress(string saveFilePath, string currentSegment, Dictionary<string, object> globalState, Dictionary<string, object> persistentState)
    {
        SaveData saveData;

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
            string movieFolder = Path.GetDirectoryName(saveFilePath);
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

            // Check if the folder name contains "Battle Kitty" or "Trivia Quest"
            if (movieFolder.Contains("Battle Kitty") || movieFolder.Contains("Trivia Quest"))
            {
                string folder = movieFolder.Contains("Battle Kitty") ? "BK" : "TQ";
                string specificSaveFilePath = Path.Combine(Directory.GetCurrentDirectory(), folder, folder == "BK" ? "bk_save.json" : "tq_save.json");

                if (File.Exists(specificSaveFilePath))
                {
                    var specificSaveData = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(specificSaveFilePath));

                    foreach (var kvp in specificSaveData.PersistentState)
                    {
                        persistentState[kvp.Key] = kvp.Value;
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

        // Save the states to the Battle Kitty or Trivia Quest save file if applicable
        if (Path.GetDirectoryName(saveFilePath).Contains("Battle Kitty") || Path.GetDirectoryName(saveFilePath).Contains("Trivia Quest"))
        {
            string folder = Path.GetDirectoryName(saveFilePath).Contains("Battle Kitty") ? "BK" : "TQ";
            string specificSaveFilePath = Path.Combine(Directory.GetCurrentDirectory(), folder, folder == "BK" ? "bk_save.json" : "tq_save.json");

            var specificSaveData = new SaveData
            {
                GlobalState = globalState,
                PersistentState = persistentState
            };

            File.WriteAllText(specificSaveFilePath, JsonConvert.SerializeObject(specificSaveData, Formatting.Indented));
        }
    }
}
