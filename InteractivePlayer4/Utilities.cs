using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static class Utilities
{
    public static string SelectedMovieFolder { get; private set; }

    public static string ShowMovieSelectionMenu()
    {
        string[] movieFolders = Directory.GetDirectories(Directory.GetCurrentDirectory());
        movieFolders = movieFolders.Where(folder =>
            !Path.GetFileName(folder).Equals("libvlc", StringComparison.OrdinalIgnoreCase) &&
            Directory.GetFiles(folder, "*.mkv").Any() &&
            Directory.GetFiles(folder, "*.json").Any()).ToArray();

        if (movieFolders.Length == 0)
        {
            MessageBox.Show("No Interactives found.");
            return null;
        }

        string defaultBackdropPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Default_backdrop.png");
        string topBarPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Top_bar.png");
        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "general", "Interactive_player_logo.png");

        Form form = new Form
        {
            Text = "Interactive Player",
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

        topBarPanel.Controls.Add(logoPictureBox);
        logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        topBarPanel.Resize += (sender, e) =>
        {
            logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        };

        FlowLayoutPanel panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 50, 0, 0) // Add padding to center the buttons vertically
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
                    Enabled = false // Make the PictureBox non-interactive
                };
                button.Controls.Add(movieLogo);
            }

            button.Click += (sender, e) =>
            {
                SelectedMovieFolder = folder;
                form.DialogResult = DialogResult.OK;
                form.Close();
            };

            panel.Controls.Add(button);
        }

        return form.ShowDialog() == DialogResult.OK ? SelectedMovieFolder : null;
    }
}