using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static class Utilities
{
    public static string SelectedMovieFolder { get; private set; }

    public static string ShowMovieSelectionMenu(string initialDirectory = null)
    {
        string currentDirectory = initialDirectory ?? Directory.GetCurrentDirectory();
        string[] movieFolders = Directory.GetDirectories(currentDirectory);
        movieFolders = movieFolders.Where(folder =>
            !Path.GetFileName(folder).Equals("libvlc", StringComparison.OrdinalIgnoreCase) &&
            (Directory.GetFiles(folder, "*.mkv").Any() && Directory.GetFiles(folder, "*.json").Any() ||
            Directory.GetFiles(folder, "backdrop.jpg").Any() && Directory.GetFiles(folder, "logo.png").Any())).ToArray();

        if (movieFolders.Length == 0)
        {
            MessageBox.Show("No Interactives found.");
            return null;
        }

        string defaultBackdropPath = Path.Combine(currentDirectory, "general", "Default_backdrop.png");
        string topBarPath = Path.Combine(currentDirectory, "general", "Top_bar.png");
        string logoPath = Path.Combine(currentDirectory, "general", "Interactive_player_logo.png");
        string settingsWheelPath = Path.Combine(currentDirectory, "general", "Settings_Wheel.png");

        Form form = new Form
        {
            Text = "Interactive Player",
            Size = new Size(2000, 750),
            StartPosition = FormStartPosition.CenterScreen,
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
            BackColor = Path.GetFileName(currentDirectory).Equals("MCSM", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#2a262a") : 
                        Path.GetFileName(currentDirectory).Equals("BK", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#3cd8a9") : Color.Black
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

        PictureBox settingsPictureBox = new PictureBox
        {
            Image = Image.FromFile(settingsWheelPath),
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        settingsPictureBox.Click += (sender, e) =>
        {
            SettingsMenu.ShowSettingsMenu();
        };

        topBarPanel.Controls.Add(logoPictureBox);
        topBarPanel.Controls.Add(settingsPictureBox);
        logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        settingsPictureBox.Location = new Point(topBarPanel.Width - settingsPictureBox.Width - 10, (topBarPanel.Height - settingsPictureBox.Height) / 2);
        topBarPanel.Resize += (sender, e) =>
        {
            logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
            settingsPictureBox.Location = new Point(topBarPanel.Width - settingsPictureBox.Width - 10, (topBarPanel.Height - settingsPictureBox.Height) / 2);
        };

        FlowLayoutPanel panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 50, 0, 0) // Center the buttons vertically
        };

        form.Controls.Add(panel);
        form.Controls.Add(topBarPanel);

        foreach (var folder in movieFolders)
        {
            string backdropPath = Directory.GetFiles(folder, "*backdrop.jpg").FirstOrDefault() ?? defaultBackdropPath;
            string movieLogoPath = Directory.GetFiles(folder, "*logo.png").FirstOrDefault();
            string folderName = Path.GetFileName(folder);

            Button button = new Button
            {
                Width = 848,
                Height = 477,
                BackgroundImage = Image.FromFile(backdropPath),
                BackgroundImageLayout = ImageLayout.Stretch,
                Text = movieLogoPath == null ? folderName : string.Empty,
                TextAlign = movieLogoPath == null ? ContentAlignment.MiddleCenter : ContentAlignment.BottomCenter,
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.White
            };

            if (movieLogoPath != null)
            {
                PictureBox movieLogo = new PictureBox
                {
                    Image = Image.FromFile(movieLogoPath),
                    Size = new Size(625, 250),
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BackColor = Color.Transparent,
                    Location = new Point((button.Width - 625) / 2, button.Height - 250 - 10), // Position at bottom center with a 10px margin
                    Enabled = false
                };
                button.Controls.Add(movieLogo);
            }

            button.Click += (sender, e) =>
            {
                if (Directory.GetFiles(folder, "*.mkv").Any() && Directory.GetFiles(folder, "*.json").Any())
                {
                    // This is an interactive folder
                    SelectedMovieFolder = folder;
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                }
                else if (Directory.GetDirectories(folder).Any())
                {
                    // Open another Movie Selection Menu with the movies in the selected folder
                    SelectedMovieFolder = ShowMovieSelectionMenu(folder);
                    if (SelectedMovieFolder != null)
                    {
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                }
                else
                {
                    // This is a regular movie folder
                    SelectedMovieFolder = folder;
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                }
            };

            panel.Controls.Add(button);
        }

        return form.ShowDialog() == DialogResult.OK ? SelectedMovieFolder : null;
    }
}