using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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

        Form settingsForm = new SettingsForm
        {
            Text = "Settings",
            Size = new Size(1400, 980),
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

        CheckBox lowEndHardwareCheckBox = new CheckBox
        {
            Text = "Lower End Modifications",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Arial", 14, FontStyle.Bold),
            Checked = loadedSettings.LowEndHardware,
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
                ControllerIcon = controllerIconComboBox.SelectedItem.ToString(),
                LowEndHardware = lowEndHardwareCheckBox.Checked
            };
            SaveSettings(settings);
            settingsForm.Close();
        };

        ToolTip toolTip = new ToolTip();

        toolTip.SetToolTip(optimizeInteractivesCheckBox, "Flatten interactive folders to remove searching times");
        toolTip.SetToolTip(disableWindowAnimationsCheckBox, "Stop window from moving, such as flying in from the bottom or the Cat Burglar ripple effects, not recommended unless animations are laggy or choices aren't appearing");
        toolTip.SetToolTip(keyboardIconComboBox, "When using a keyboard and mouse, if available, which timer icon do you want it to show?");
        toolTip.SetToolTip(controllerIconComboBox, "When using a controller, if available, which timer icon do you want it to show?");
        toolTip.SetToolTip(lowEndHardwareCheckBox, "Enable modifications for lower end hardware (changes animations and transitions)");
        toolTip.SetToolTip(customStoryChangingNotificationCheckBox, "Change things from the Netflix version (this option is for those looking for a better experience, but not exactly an original one)");

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
        lowEndHardwareCheckBox.Location = new Point(centerX - lowEndHardwareCheckBox.Width / 2, 480);
        disableWindowAnimationsCheckBox.Location = new Point(centerX - disableWindowAnimationsCheckBox.Width / 2, 520);

        extrasTitle.Location = new Point(leftAlignX, 570);
        keyboardIconLabel.Location = new Point(centerX - keyboardIconLabel.Width / 2, 620);
        keyboardIconComboBox.Location = new Point(centerX, 650);

        controllerIconLabel.Location = new Point(centerX - controllerIconLabel.Width / 2, 690);
        controllerIconComboBox.Location = new Point(centerX, 720);

        customStoryChangingNotificationCheckBox.Location = new Point(centerX - customStoryChangingNotificationCheckBox.Width / 2, 760);

        int logoStartX = centerX - youtubePictureBox.Width / 2;
        youtubePictureBox.Location = new Point(logoStartX, 800);
        discordPictureBox.Location = new Point(logoStartX + youtubePictureBox.Width + 20, 800);
        githubPictureBox.Location = new Point(logoStartX + youtubePictureBox.Width + discordPictureBox.Width + 40, 800);

        settingsForm.Controls.Add(audioLanguageTitle);
        settingsForm.Controls.Add(audioLabel);
        settingsForm.Controls.Add(audioComboBox);
        settingsForm.Controls.Add(subtitleLabel);
        settingsForm.Controls.Add(subtitleComboBox);
        settingsForm.Controls.Add(audioOutputLabel);
        settingsForm.Controls.Add(audioOutputComboBox);

        settingsForm.Controls.Add(optimizationTitle);
        settingsForm.Controls.Add(optimizeInteractivesCheckBox);
        settingsForm.Controls.Add(lowEndHardwareCheckBox);
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
public class SettingsForm : Form
{
    private Timer animationTimer;
    private double ribbon1Phase = 0;
    private double ribbon2Phase = 0;

    public SettingsForm()
    {
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        animationTimer = new Timer();
        animationTimer.Interval = 30;
        animationTimer.Tick += (s, e) =>
        {
            ribbon1Phase += 0.018;
            ribbon2Phase += 0.011;
            this.Invalidate(new Rectangle(0, this.ClientSize.Height - 200, this.ClientSize.Width, 200));
        };
        animationTimer.Start();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);

        Rectangle rect = this.ClientRectangle;
        if (rect.Width == 0 || rect.Height == 0)
            return;

        using (SolidBrush bgBrush = new SolidBrush(ColorTranslator.FromHtml("#141414")))
            e.Graphics.FillRectangle(bgBrush, rect);

        DrawRibbonLayer(
            e.Graphics,
            rect,
            baseY: rect.Bottom - 90,
            amplitude: 12,
            segments: 96, 
            phase: ribbon1Phase,
            gradientHeight: 80,
            baseColor: Color.FromArgb(140, 154, 27, 43),
            tipColor: Color.FromArgb(220, 200, 40, 60)
        );
        DrawRibbonLayer(
            e.Graphics,
            rect,
            baseY: rect.Bottom - 50,
            amplitude: 7,
            segments: 96,
            phase: ribbon2Phase,
            gradientHeight: 60,
            baseColor: Color.FromArgb(200, 154, 27, 43),
            tipColor: Color.FromArgb(220, 180, 30, 50)
        );
    }

    private void DrawRibbonLayer(Graphics g, Rectangle rect, int baseY, int amplitude, int segments, double phase, int gradientHeight, Color baseColor, Color tipColor)
    {
        Point[] points = new Point[segments + 1];
        int width = rect.Width;
        double tStep = (double)width / segments;

        double tipT = (phase / (2 * Math.PI)) % 1.0;
        if (tipT < 0) tipT += 1.0;
        int tipIndex = (int)(tipT * segments);

        for (int i = 0; i <= segments; i++)
        {
            double t = (double)i / segments;
            double x = i * tStep;
            double y = baseY
                + Math.Sin(phase + t * 2 * Math.PI) * amplitude * 0.85
                + Math.Sin(phase * 0.5 + t * 2 * Math.PI) * amplitude * 0.15;
            points[i] = new Point((int)x, (int)y);
        }

        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddLines(points);
            path.AddLine(points[segments].X, points[segments].Y, rect.Width, rect.Bottom);
            path.AddLine(rect.Width, rect.Bottom, 0, rect.Bottom);
            path.AddLine(0, rect.Bottom, points[0].X, points[0].Y);
            path.CloseFigure();

            using (PathGradientBrush brush = new PathGradientBrush(path))
            {
                brush.CenterPoint = points[tipIndex];
                brush.CenterColor = tipColor;
                brush.SurroundColors = Enumerable.Repeat(baseColor, path.PointCount).ToArray();

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(brush, path);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && animationTimer != null)
        {
            animationTimer.Dispose();
            animationTimer = null;
        }
        base.Dispose(disposing);
    }
}