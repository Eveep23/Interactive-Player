using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Diagnostics;

public static class SettingsMenu
{
    private static readonly string ConfigFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

    public static void ShowSettingsMenu()
    {
        string backArrowPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Back_arrow.png");
        string topBarPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Top_bar.png");
        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Interactive_player_logo.png");
        string youtubeLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Youtube_Logo.png");
        string discordLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Discord_Logo.png");
        string githubLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Github_Logo.png");

        Form settingsForm = new Form
        {
            Text = "Settings",
            Size = new Size(1400, 750),
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
        audioComboBox.Items.AddRange(new string[] { "Arabic", "Czech", "German", "English", "Latin American - Spanish", "European - Spanish", "French", "Hindi", "Hungarian", "Indonesian", "Italian", "Polish", "Brazilian - Portuguese", "European - Portuguese", "Thai", "Turkish", "Ukrainian" });
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
        
        // Audio Output Dropdown
        Label audioOutputLabel = new Label
        {
            Text = "Audio Output:",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        ComboBox audioOutputComboBox = new ComboBox
        {
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Arial", 14)
        };
        audioOutputComboBox.Items.AddRange(new string[] { "Original", "Stereo", "Headphones" });
        audioOutputComboBox.SelectedItem = loadedSettings.AudioOutput ?? "Original";

        // Custom Story Changing Notification Checkbox
        CheckBox customStoryChangingNotificationCheckBox = new CheckBox
        {
            Text = "Custom Emulator Modifications",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            Checked = loadedSettings.CustomStoryChangingNotification,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };

        // "Optimize Interactives" Checkbox
        CheckBox optimizeInteractivesCheckBox = new CheckBox
        {
            Text = "Optimize Interactives",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            Checked = loadedSettings.OptimizeInteractives,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };

        // "Disable Window Animations" Checkbox
        CheckBox disableWindowAnimationsCheckBox = new CheckBox
        {
            Text = "Disable Window Animations",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            Checked = loadedSettings.DisableWindowAnimations,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };

        // Social Media Logos
        PictureBox youtubePictureBox = new PictureBox
        {
            Image = Image.FromFile(youtubeLogoPath),
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        youtubePictureBox.Click += (sender, e) => Process.Start("https://www.youtube.com/@eveep23");

        PictureBox discordPictureBox = new PictureBox
        {
            Image = Image.FromFile(discordLogoPath),
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        discordPictureBox.Click += (sender, e) => Process.Start("https://discord.gg/E4CbrXETsW");

        PictureBox githubPictureBox = new PictureBox
        {
            Image = Image.FromFile(githubLogoPath),
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        githubPictureBox.Click += (sender, e) => Process.Start("https://github.com/Eveep23/Interactive-Player");

        backPictureBox.Click += (sender, e) =>
        {
            var settings = new Settings
            {
                AudioLanguage = audioComboBox.SelectedItem.ToString(),
                SubtitleLanguage = subtitleComboBox.SelectedItem.ToString(),
                CustomStoryChangingNotification = customStoryChangingNotificationCheckBox.Checked,
                OptimizeInteractives = optimizeInteractivesCheckBox.Checked,
                AudioOutput = audioOutputComboBox.SelectedItem.ToString(),
                DisableWindowAnimations = disableWindowAnimationsCheckBox.Checked
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

        audioOutputLabel.Location = new Point(centerX - audioOutputLabel.Width / 2, 310);
        audioOutputComboBox.Location = new Point(centerX, 340);

        customStoryChangingNotificationCheckBox.Location = new Point(centerX - customStoryChangingNotificationCheckBox.Width / 2, 390);

        optimizeInteractivesCheckBox.Location = new Point(centerX - optimizeInteractivesCheckBox.Width / 2, 440);

        disableWindowAnimationsCheckBox.Location = new Point(centerX - disableWindowAnimationsCheckBox.Width / 2, 490);

        // Position social media logos
        int logoStartX = centerX - youtubePictureBox.Width / 2;
        youtubePictureBox.Location = new Point(logoStartX, 540);
        discordPictureBox.Location = new Point(logoStartX + youtubePictureBox.Width + 20, 540);
        githubPictureBox.Location = new Point(logoStartX + youtubePictureBox.Width + discordPictureBox.Width + 40, 540);

        settingsForm.Controls.Add(audioLabel);
        settingsForm.Controls.Add(audioComboBox);
        settingsForm.Controls.Add(subtitleLabel);
        settingsForm.Controls.Add(subtitleComboBox);
        settingsForm.Controls.Add(audioOutputLabel);
        settingsForm.Controls.Add(audioOutputComboBox);
        settingsForm.Controls.Add(customStoryChangingNotificationCheckBox);
        settingsForm.Controls.Add(optimizeInteractivesCheckBox);
        settingsForm.Controls.Add(disableWindowAnimationsCheckBox);
        settingsForm.Controls.Add(youtubePictureBox);
        settingsForm.Controls.Add(discordPictureBox);
        settingsForm.Controls.Add(githubPictureBox);

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
            CustomStoryChangingNotification = true,
            OptimizeInteractives = true,
            DisableWindowAnimations = false
        };
    }

    private static void SaveSettings(Settings settings)
    {
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(ConfigFilePath, json);
    }
}