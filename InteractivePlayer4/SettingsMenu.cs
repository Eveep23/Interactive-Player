using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

public static class SettingsMenu
{
    private static readonly string ConfigFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

    public static void ShowSettingsMenu()
    {
        string backArrowPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Back_arrow.png");
        string topBarPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Top_bar.png");
        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Interactive_player_logo.png");

        Form settingsForm = new Form
        {
            Text = "Settings",
            Size = new Size(1000, 750),
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

        PictureBox backPictureBox = new PictureBox
        {
            Image = Image.FromFile(backArrowPath),
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        // Load settings
        var loadedSettings = LoadSettings();

        // Audio Language Dropdown
        Label audioLabel = new Label
        {
            Text = "Audio Language:",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        ComboBox audioComboBox = new ComboBox
        {
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Arial", 14)
        };
        audioComboBox.Items.AddRange(new string[] { "Czech", "German", "English", "Latin American - Spanish", "European - Spanish", "French", "Hindi", "Hungarian", "Indonesian", "Italian", "Polish", "Brazilian - Portuguese", "Thai", "Turkish", "Ukrainian" });
        audioComboBox.SelectedItem = loadedSettings.AudioLanguage;

        // Subtitle Language Dropdown
        Label subtitleLabel = new Label
        {
            Text = "Subtitle Language:",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        ComboBox subtitleComboBox = new ComboBox
        {
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Arial", 14)
        };
        subtitleComboBox.Items.AddRange(new string[] { "Disabled", "Arabic", "Forced - Czech", "Czech", "Danish", "Forced - German", "SDH - German", "German", "SDH - English", "English", "Latin American (SDH) - Spanish", "Latin American (Forced) - Spanish", "Latin American - Spanish", "European (Forced) - Spanish", "European - Spanish", "Finnish", "Filpino", "Forced - French", "French", "Hebrew", "Croatian", "Latin (Forced) - Hindi", "Forced - Hungarian", "Hungarian", "Forced - Indonesian", "Indonesian", "Forced - Italian", "Italian", "Forced - Polish", "Polish", "Brazilian (SDH) - Portuguese", "Brazilian (Forced) - Portuguese", "Brazilian - Portuguese", "European - Portuguese", "Forced - Thai", "Thai", "Forced - Turkish", "Turkish", "Forced - Ukrainian", "Ukrainian", "Japanese", "Korean", "Dutch", "Romanian", "Russian", "Swedish", "Vietnamese", "Simplified - Chinese", "Traditional - Chinese", "Malay" });
        subtitleComboBox.SelectedItem = loadedSettings.SubtitleLanguage;

        backPictureBox.Click += (sender, e) =>
        {
            var settings = new Settings
            {
                AudioLanguage = audioComboBox.SelectedItem.ToString(),
                SubtitleLanguage = subtitleComboBox.SelectedItem.ToString(),
            };
            SaveSettings(settings);
            settingsForm.Close();
        };

        topBarPanel.Controls.Add(logoPictureBox);
        topBarPanel.Controls.Add(backPictureBox);
        logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        backPictureBox.Location = new Point(10, (topBarPanel.Height - backPictureBox.Height) / 2);
        topBarPanel.Resize += (sender, e) =>
        {
            logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
            backPictureBox.Location = new Point(10, (topBarPanel.Height - backPictureBox.Height) / 2);
        };

        settingsForm.Controls.Add(topBarPanel);

        // Center the labels and dropdowns horizontally
        int centerX = (settingsForm.ClientSize.Width - audioComboBox.Width) / 2;

        audioLabel.Location = new Point(centerX - audioLabel.Width / 2, 150);
        audioComboBox.Location = new Point(centerX, 180);

        subtitleLabel.Location = new Point(centerX - subtitleLabel.Width / 2, 230);
        subtitleComboBox.Location = new Point(centerX, 260);

        settingsForm.Controls.Add(audioLabel);
        settingsForm.Controls.Add(audioComboBox);
        settingsForm.Controls.Add(subtitleLabel);
        settingsForm.Controls.Add(subtitleComboBox);

        settingsForm.ShowDialog();
    }

    private static Settings LoadSettings()
    {
        if (File.Exists(ConfigFilePath))
        {
            string json = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<Settings>(json);
        }
        return new Settings
        {
            AudioLanguage = "English",
            SubtitleLanguage = "Disabled",
        };
    }

    private static void SaveSettings(Settings settings)
    {
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(ConfigFilePath, json);
    }
}
