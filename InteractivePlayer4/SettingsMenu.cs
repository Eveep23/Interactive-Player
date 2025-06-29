using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing.Drawing2D;

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
            Size = new Size(1400, 940),
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

        GradientLabel audioLanguageTitle = new GradientLabel
        {
            Text = "Audio/Language",
            ForeColor = Color.White,
            AutoSize = false,
            Font = new Font("Arial", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Size = new Size(400, 40),
            GradientEnd = settingsForm.BackColor
        };

        GradientLabel optimizationTitle = new GradientLabel
        {
            Text = "Optimization",
            ForeColor = Color.White,
            AutoSize = false,
            Font = new Font("Arial", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Size = new Size(400, 40),
            GradientEnd = settingsForm.BackColor
        };

        GradientLabel extrasTitle = new GradientLabel
        {
            Text = "Extras",
            ForeColor = Color.White,
            AutoSize = false,
            Font = new Font("Arial", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Size = new Size(400, 40),
            GradientEnd = settingsForm.BackColor
        };

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
        subtitleComboBox.Items.AddRange(new string[] { "Disabled", "Arabic", "Czech", "Danish", "SDH - German", "German", "SDH - English", "English", "Latin American (SDH) - Spanish", "Latin American (Forced) - Spanish", "Latin American - Spanish", "European (Forced) - Spanish", "European - Spanish", "Finnish", "Filpino", "French", "Hebrew", "Croatian", "Latin (Forced) - Hindi", "Hungarian", "Indonesian", "Italian", "Polish", "Brazilian (SDH) - Portuguese", "Brazilian (Forced) - Portuguese", "Brazilian - Portuguese", "European - Portuguese", "Forced - Thai", "Thai", "Turkish", "Ukrainian", "Japanese", "Korean", "Dutch", "Romanian", "Russian", "Swedish", "Vietnamese", "Simplified - Chinese", "Traditional - Chinese", "Malay" });
        subtitleComboBox.SelectedItem = loadedSettings.SubtitleLanguage;
        
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

        Label keyboardIconLabel = new Label
        {
            Text = "Keyboard Icon:",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        ComboBox keyboardIconComboBox = new ComboBox
        {
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Arial", 14)
        };
        keyboardIconComboBox.Items.AddRange(new string[] { "Cursor", "Hand" });
        keyboardIconComboBox.SelectedItem = loadedSettings.KeyboardIcon ?? "Cursor";

        Label controllerIconLabel = new Label
        {
            Text = "Controller Icon:",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        ComboBox controllerIconComboBox = new ComboBox
        {
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Arial", 14)
        };
        controllerIconComboBox.Items.AddRange(new string[] { "Gamepad", "Remote" });
        controllerIconComboBox.SelectedItem = loadedSettings.ControllerIcon ?? "Gamepad";

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
                DisableWindowAnimations = disableWindowAnimationsCheckBox.Checked,
                KeyboardIcon = keyboardIconComboBox.SelectedItem.ToString(),
                ControllerIcon = controllerIconComboBox.SelectedItem.ToString()
            };
            SaveSettings(settings);
            settingsForm.Close();
        };

        ToolTip toolTip = new ToolTip();

        toolTip.SetToolTip(optimizeInteractivesCheckBox, "Flatten interactive folders to remove searching times");
        toolTip.SetToolTip(disableWindowAnimationsCheckBox, "Stop window from moving, such as flying in from bottom or the Cat Burglar ripple effects, not recommended unless animations are really laggy or choices aren't appearing");
        toolTip.SetToolTip(keyboardIconComboBox, "When using a keyboard and mouse, if available, which timer icon do you want it to show?");
        toolTip.SetToolTip(controllerIconComboBox, "When using a controller, if available, which timer icon do you want it to show?");
        toolTip.SetToolTip(customStoryChangingNotificationCheckBox, "Change things from the Netflix version (this option is for those looking for the original experience, not a better one)");

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

        int centerX = (settingsForm.ClientSize.Width - audioComboBox.Width) / 2;

        int leftAlignX = centerX - audioLabel.Width / 2;

        audioLanguageTitle.Location = new Point(leftAlignX, 120);
        audioLabel.Location = new Point(centerX - audioLabel.Width / 2, 170);
        audioComboBox.Location = new Point(centerX, 200);

        subtitleLabel.Location = new Point(centerX - subtitleLabel.Width / 2, 240);
        subtitleComboBox.Location = new Point(centerX, 270);

        audioOutputLabel.Location = new Point(centerX - audioOutputLabel.Width / 2, 310);
        audioOutputComboBox.Location = new Point(centerX, 340);

        optimizationTitle.Location = new Point(leftAlignX, 390);
        optimizeInteractivesCheckBox.Location = new Point(centerX - optimizeInteractivesCheckBox.Width / 2, 440);
        disableWindowAnimationsCheckBox.Location = new Point(centerX - disableWindowAnimationsCheckBox.Width / 2, 480);

        extrasTitle.Location = new Point(leftAlignX, 530);
        keyboardIconLabel.Location = new Point(centerX - keyboardIconLabel.Width / 2, 580);
        keyboardIconComboBox.Location = new Point(centerX, 610);

        controllerIconLabel.Location = new Point(centerX - controllerIconLabel.Width / 2, 650);
        controllerIconComboBox.Location = new Point(centerX, 680);

        customStoryChangingNotificationCheckBox.Location = new Point(centerX - customStoryChangingNotificationCheckBox.Width / 2, 720);

        int logoStartX = centerX - youtubePictureBox.Width / 2;
        youtubePictureBox.Location = new Point(logoStartX, 760);
        discordPictureBox.Location = new Point(logoStartX + youtubePictureBox.Width + 20, 760);
        githubPictureBox.Location = new Point(logoStartX + youtubePictureBox.Width + discordPictureBox.Width + 40, 760);

        settingsForm.Controls.Add(audioLanguageTitle);
        settingsForm.Controls.Add(audioLabel);
        settingsForm.Controls.Add(audioComboBox);
        settingsForm.Controls.Add(subtitleLabel);
        settingsForm.Controls.Add(subtitleComboBox);
        settingsForm.Controls.Add(audioOutputLabel);
        settingsForm.Controls.Add(audioOutputComboBox);

        settingsForm.Controls.Add(optimizationTitle);
        settingsForm.Controls.Add(optimizeInteractivesCheckBox);
        settingsForm.Controls.Add(disableWindowAnimationsCheckBox);

        settingsForm.Controls.Add(extrasTitle);
        settingsForm.Controls.Add(keyboardIconLabel);
        settingsForm.Controls.Add(keyboardIconComboBox);
        settingsForm.Controls.Add(controllerIconLabel);
        settingsForm.Controls.Add(controllerIconComboBox);
        settingsForm.Controls.Add(customStoryChangingNotificationCheckBox);

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
            DisableWindowAnimations = false,
            KeyboardIcon = "Cursor",
            ControllerIcon = "Gamepad"
        };
    }

    private static void SaveSettings(Settings settings)
    {
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(ConfigFilePath, json);
    }
}

public class GradientLabel : Label
{
    public Color GradientStart { get; set; } = ColorTranslator.FromHtml("#9a1b2b");
    public Color GradientEnd { get; set; } = ColorTranslator.FromHtml("#141414");

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var brush = new LinearGradientBrush(
            this.ClientRectangle,
            GradientStart,
            GradientEnd,
            LinearGradientMode.Horizontal))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }
        // Draw the text over the gradient
        TextRenderer.DrawText(
            e.Graphics,
            this.Text,
            this.Font,
            this.ClientRectangle,
            this.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}