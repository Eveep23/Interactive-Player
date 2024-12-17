using System;
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

    public static void SaveProgress(string saveFilePath, string currentSegment)
    {
        var saveData = new SaveData { CurrentSegment = currentSegment };
        File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(saveData, Formatting.Indented));
    }
}
