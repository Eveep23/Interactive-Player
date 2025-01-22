using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
                Size = new Size(1400, 750),
                StartPosition = FormStartPosition.CenterScreen,
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                BackColor = Color.Black
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

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 50, 0, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            form.Controls.Add(buttonPanel);
            form.Controls.Add(topBarPanel);

            Button continueButton = new Button
            {
                Width = 400,
                Height = 477,
                BackgroundImage = Image.FromFile(defaultBackdropPath),
                BackgroundImageLayout = ImageLayout.Stretch,
                Text = "Continue",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Anchor = AnchorStyles.None
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
                Width = 400,
                Height = 477,
                BackgroundImage = Image.FromFile(defaultBackdropPath),
                BackgroundImageLayout = ImageLayout.Stretch,
                Text = "Restart",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Anchor = AnchorStyles.None
            };

            restartButton.Click += (sender, e) =>
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
                SelectedMovieFolder = null;
            };

            buttonPanel.Controls.Add(continueButton);
            buttonPanel.Controls.Add(restartButton);

            // Center the buttons horizontally
            buttonPanel.Padding = new Padding((form.ClientSize.Width - (continueButton.Width + restartButton.Width)) / 2, 50, 0, 0);

            form.FormClosed += (sender, e) =>
            {
                if (form.DialogResult == DialogResult.Cancel)
                {
                    Utilities.ShowMovieSelectionMenu();
                }
            };

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

            // Merge global state
            foreach (var kvp in globalState)
            {
                saveData.GlobalState[kvp.Key] = kvp.Value;
            }

            // Merge persistent state
            foreach (var kvp in persistentState)
            {
                saveData.PersistentState[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            // Check if the movie folder is "Minecraft Story Mode Ep2"
            string movieFolder = Path.GetDirectoryName(saveFilePath);
            if (movieFolder.EndsWith("Minecraft Story Mode Ep2") || movieFolder.EndsWith("Minecraft Story Mode Ep3") || movieFolder.EndsWith("Minecraft Story Mode Ep5"))
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
            }

            // Create new save data
            saveData = new SaveData
            {
                CurrentSegment = currentSegment,
                GlobalState = globalState,
                PersistentState = persistentState
            };
        }

        // Save the updated or new save data to the file
        File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(saveData, Formatting.Indented));
    }
}
