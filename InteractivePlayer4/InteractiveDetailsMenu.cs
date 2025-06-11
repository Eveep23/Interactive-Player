using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

public static class InteractiveDetailsMenu
{
    public static void ShowInteractiveDetailsMenu(string interactiveFolder)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string topBarPath = Path.Combine(currentDirectory, "general", "Top_bar.png");
        string logoPath = Path.Combine(currentDirectory, "general", "Interactive_player_logo.png");
        string backArrowPath = Path.Combine(currentDirectory, "general", "Back_arrow.png");
        string packsDirectory = Path.Combine(currentDirectory, "Packs");
        string installButtonPath = Path.Combine(currentDirectory, "general", "Big_Install_Button.png");
        string uninstallButtonPath = Path.Combine(currentDirectory, "general", "Big_Uninstall_Button.png");
        string updateButtonPath = Path.Combine(currentDirectory, "general", "Big_Update_Button.png");

        Form detailsForm = new Form
        {
            Text = "Interactive Details",
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

        backPictureBox.Click += (sender, e) =>
        {
            detailsForm.Close();
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

        detailsForm.Controls.Add(topBarPanel);

        // Create a TableLayoutPanel to split the screen into two panels
        TableLayoutPanel mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#141414"),
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, topBarPanel.Height, 0, 0)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        // Left panel (details)
        Panel leftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#141414"),
            Padding = new Padding(10)
        };

        PictureBox displayPictureBox = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Top,
            Height = 300,
            BackColor = Color.Transparent
        };

        Label titleLabel = new Label
        {
            ForeColor = Color.White,
            BackColor = ColorTranslator.FromHtml("#141414"),
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Arial", 20, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10)
        };

        Label descriptionLabel = new Label
        {
            ForeColor = Color.White,
            BackColor = ColorTranslator.FromHtml("#141414"),
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Arial", 14),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(10),
            MaximumSize = new Size(leftPanel.Width + 450, 0)
        };

        leftPanel.Controls.Add(descriptionLabel);
        leftPanel.Controls.Add(titleLabel);
        leftPanel.Controls.Add(displayPictureBox);

        // Load details from the Packs folder
        string folderName = Path.GetFileName(interactiveFolder);
        string jsonFilePath = Path.Combine(packsDirectory, folderName + ".json");
        string pngFilePath = Path.Combine(packsDirectory, folderName + ".png");

        if (File.Exists(jsonFilePath) && File.Exists(pngFilePath))
        {
            var jsonData = JObject.Parse(File.ReadAllText(jsonFilePath));
            string title = jsonData["title"]?.ToString();
            string description = jsonData["description"]?.ToString();

            displayPictureBox.Image = Image.FromFile(pngFilePath);
            titleLabel.Text = title;
            descriptionLabel.Text = description;
        }

        // Right panel (actions)
        Panel rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#141414"),
            Padding = new Padding(10)
        };

        PictureBox actionButton = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        // Determine the action (install, update, or uninstall)
        bool isInstalled = Directory.Exists(interactiveFolder);
        string buttonImagePath = installButtonPath;

        if (isInstalled)
        {
            string buildJsonPath = Path.Combine(interactiveFolder, "build.txt");
            int currentBuild = 0;
            int newBuild = 0;

            if (File.Exists(buildJsonPath))
            {
                var buildJsonData = JObject.Parse(File.ReadAllText(buildJsonPath));
                currentBuild = buildJsonData["build"]?.ToObject<int>() ?? 0;
            }

            if (File.Exists(jsonFilePath))
            {
                var installJsonData = JObject.Parse(File.ReadAllText(jsonFilePath));
                newBuild = installJsonData["build"]?.ToObject<int>() ?? 0;
            }

            if (newBuild > currentBuild)
            {
                buttonImagePath = updateButtonPath;
            }
            else
            {
                buttonImagePath = uninstallButtonPath;
            }
        }

        actionButton.Image = Image.FromFile(buttonImagePath);
        actionButton.Location = new Point((rightPanel.Width - actionButton.Width) / 2, rightPanel.Height - actionButton.Height - 10);

        actionButton.Click += (sender, e) =>
        {
            if (buttonImagePath == uninstallButtonPath)
            {
                // Uninstall: delete the folder and restart
                Directory.Delete(interactiveFolder, true);
                MessageBox.Show(detailsForm, "Interactive uninstalled successfully.");
                Application.Restart();
                Environment.Exit(0);
            }
            else if (buttonImagePath == updateButtonPath)
            {
                // Update: delete all files except direct.json, save.json, snapshots.json, and video file
                var filesToKeep = new[] { "direct.json", "save.json", "snapshots.json" };
                var videoExtensions = new[] { ".mkv", ".mp4" };

                foreach (var filePath in Directory.GetFiles(interactiveFolder))
                {
                    string fileName = Path.GetFileName(filePath);
                    string fileExtension = Path.GetExtension(filePath);

                    if (!filesToKeep.Contains(fileName) && !videoExtensions.Contains(fileExtension))
                    {
                        File.Delete(filePath);
                    }
                }

                // Extract the .intpak file to a temporary directory
                string tempDirectory = Path.Combine(currentDirectory, "temp");
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
                Directory.CreateDirectory(tempDirectory);
                string intpakFile = Path.Combine(packsDirectory, folderName + ".intpak");
                System.IO.Compression.ZipFile.ExtractToDirectory(intpakFile, tempDirectory);

                // Move the extracted files to the interactive folder
                foreach (var tempFilePath in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
                {
                    string relativePath = tempFilePath.Substring(tempDirectory.Length + 1);
                    string destFilePath = Path.Combine(interactiveFolder, relativePath);
                    string destDirectory = Path.GetDirectoryName(destFilePath);

                    if (!Directory.Exists(destDirectory))
                    {
                        Directory.CreateDirectory(destDirectory);
                    }

                    if (File.Exists(destFilePath))
                    {
                        File.Delete(destFilePath);
                    }

                    File.Move(tempFilePath, destFilePath);
                }

                // Delete the temporary directory
                Directory.Delete(tempDirectory, true);

                // Create the build.txt file
                int newBuild = JObject.Parse(File.ReadAllText(jsonFilePath))["build"]?.ToObject<int>() ?? 0;
                string buildJsonContent = $"{{\n  \"build\": {newBuild}\n}}";
                File.WriteAllText(Path.Combine(interactiveFolder, "build.txt"), buildJsonContent);

                MessageBox.Show(detailsForm, "Interactive updated successfully.");
                Application.Restart();
                Environment.Exit(0);
            }
            else
            {
                // Install: open file dialog and install
                Thread thread = new Thread(() =>
                {
                    using (OpenFileDialog openFileDialog = new OpenFileDialog())
                    {
                        openFileDialog.Filter = "Video Files|*.mkv;*.mp4";
                        openFileDialog.Title = "Select the Internal Video";

                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            string selectedVideoFile = openFileDialog.FileName;

                            // Ensure the interactive folder exists
                            if (!Directory.Exists(interactiveFolder))
                            {
                                Directory.CreateDirectory(interactiveFolder);
                            }

                            // Extract the .intpak file to the interactive folder
                            string intpakFile = Path.Combine(packsDirectory, folderName + ".intpak");
                            System.IO.Compression.ZipFile.ExtractToDirectory(intpakFile, interactiveFolder);

                            // Create the direct.json file
                            string directJsonContent = $"{{\n  \"Directory\": \"{selectedVideoFile.Replace("\\", "\\\\")}\"\n}}";
                            File.WriteAllText(Path.Combine(interactiveFolder, "direct.json"), directJsonContent);

                            // Create the build.txt file
                            int newBuild = JObject.Parse(File.ReadAllText(jsonFilePath))["build"]?.ToObject<int>() ?? 0;
                            string buildJsonContent = $"{{\n  \"build\": {newBuild}\n}}";
                            File.WriteAllText(Path.Combine(interactiveFolder, "build.txt"), buildJsonContent);

                            MessageBox.Show(detailsForm, "Interactive installed successfully.");
                            Application.Restart();
                            Environment.Exit(0);
                        }
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        };

        rightPanel.Controls.Add(actionButton);
        rightPanel.Resize += (sender, e) =>
        {
            actionButton.Location = new Point(
                (rightPanel.Width - actionButton.Width) / 2,
                (rightPanel.Height - actionButton.Height) / 2
            );
        };

        mainPanel.Controls.Add(leftPanel, 0, 0);
        mainPanel.Controls.Add(rightPanel, 1, 0);
        detailsForm.Controls.Add(mainPanel);

        detailsForm.ShowDialog();
    }
}
