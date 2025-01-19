using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Threading;

public static class InstallInteractives
{
    public static void ShowInstallInteractivesMenu()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string backArrowPath = Path.Combine(currentDirectory, "general", "Back_arrow.png");
        string topBarPath = Path.Combine(currentDirectory, "general", "Top_bar.png");
        string logoPath = Path.Combine(currentDirectory, "general", "Interactive_player_logo.png");
        string packsDirectory = Path.Combine(currentDirectory, "Packs");
        string installButtonPath = Path.Combine(currentDirectory, "general", "Install_Button.png");
        string uninstallButtonPath = Path.Combine(currentDirectory, "general", "Uninstall_Button.png");
        string bigInstallButtonPath = Path.Combine(currentDirectory, "general", "Big_Install_Button.png");
        string bigUninstallButtonPath = Path.Combine(currentDirectory, "general", "Big_Uninstall_Button.png");

        Form form = new Form
        {
            Text = "Install Interactives",
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

        PictureBox backPictureBox = new PictureBox
        {
            Image = Image.FromFile(backArrowPath),
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        backPictureBox.Click += (sender, e) =>
        {
            Application.Restart();
            Environment.Exit(0);
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

        form.Controls.Add(topBarPanel);

        // Create a TableLayoutPanel to split the screen in half
        TableLayoutPanel mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, topBarPanel.Height, 0, 0)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        FlowLayoutPanel leftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10)
        };

        Panel rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
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
            BackColor = Color.Black,
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Arial", 20, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10)
        };

        Label descriptionLabel = new Label
        {
            ForeColor = Color.White,
            BackColor = Color.Black,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Arial", 14),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(10),
            MaximumSize = new Size(rightPanel.Width + 450, 0)
        };

        PictureBox actionButton = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        rightPanel.Controls.Add(actionButton);
        rightPanel.Controls.Add(descriptionLabel);
        rightPanel.Controls.Add(titleLabel);
        rightPanel.Controls.Add(displayPictureBox);

        // Center the actionButton horizontally within the rightPanel
        rightPanel.Resize += (sender, e) =>
        {
            actionButton.Location = new Point((rightPanel.Width - actionButton.Width) / 2, rightPanel.Height - actionButton.Height - 10);
        };

        // Add .intpak files to the left panel
        if (Directory.Exists(packsDirectory))
        {
            var intpakFiles = Directory.GetFiles(packsDirectory, "*.intpak");
            foreach (var file in intpakFiles)
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                string correspondingFolder;

                // Check for specific "Minecraft Story Mode" episodes
                if (fileNameWithoutExtension.StartsWith("Minecraft Story Mode Ep"))
                {
                    correspondingFolder = Path.Combine(currentDirectory, "MCSM", fileNameWithoutExtension);
                }
                else
                {
                    correspondingFolder = Path.Combine(currentDirectory, fileNameWithoutExtension);
                }

                bool isInstalled;
                if (fileNameWithoutExtension.StartsWith("Minecraft Story Mode Ep"))
                {
                    // Check if there are any JSON files in the folder
                    isInstalled = Directory.Exists(correspondingFolder) && Directory.GetFiles(correspondingFolder, "*.json").Any();
                }
                else
                {
                    // Check if the folder exists
                    isInstalled = Directory.Exists(correspondingFolder);
                }

                string buttonImagePath = isInstalled ? uninstallButtonPath : installButtonPath;

                Panel filePanel = new Panel
                {
                    Width = (int)(leftPanel.ClientSize.Width * 3.25),
                    Height = 80,
                    BackColor = Color.Black,
                    Margin = new Padding(5)
                };

                PictureBox buttonPictureBox = new PictureBox
                {
                    Image = Image.FromFile(buttonImagePath),
                    SizeMode = PictureBoxSizeMode.AutoSize,
                    BackColor = Color.Transparent,
                    Location = new Point(5, (filePanel.Height - 50) / 2),
                    Cursor = Cursors.Hand
                };

                Label fileLabel = new Label
                {
                    Text = fileNameWithoutExtension,
                    ForeColor = Color.White,
                    BackColor = Color.Black,
                    AutoSize = true,
                    MaximumSize = new Size(filePanel.Width - buttonPictureBox.Width - 15, 0),
                    Padding = new Padding(5),
                    Margin = new Padding(5),
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(buttonPictureBox.Width + 10, (filePanel.Height - 17) / 2)
                };

                buttonPictureBox.Click += (sender, e) =>
                {
                    string jsonFilePath = Path.Combine(packsDirectory, fileNameWithoutExtension + ".json");
                    string pngFilePath = Path.Combine(packsDirectory, fileNameWithoutExtension + ".png");

                    if (File.Exists(jsonFilePath) && File.Exists(pngFilePath))
                    {
                        var jsonData = JObject.Parse(File.ReadAllText(jsonFilePath));
                        string title = jsonData["title"]?.ToString();
                        string description = jsonData["description"]?.ToString();

                        displayPictureBox.Image = Image.FromFile(pngFilePath);
                        titleLabel.Text = title;
                        descriptionLabel.Text = description;

                        string bigButtonImagePath = isInstalled ? bigUninstallButtonPath : bigInstallButtonPath;
                        actionButton.Image = Image.FromFile(bigButtonImagePath);
                        actionButton.Location = new Point((rightPanel.Width - actionButton.Width) / 2, rightPanel.Height - actionButton.Height - 10);

                        actionButton.Click += (s, ev) =>
                        {
                            if (isInstalled)
                            {
                                // Uninstall action: delete the folder and restart
                                Directory.Delete(correspondingFolder, true);
                                Application.Restart();
                                Environment.Exit(0);
                            }
                            else
                            {
                                // Install action: open file dialog and install
                                Thread thread = new Thread(() =>
                                {
                                    using (OpenFileDialog openFileDialog = new OpenFileDialog())
                                    {
                                        openFileDialog.Filter = "Video Files|*.mkv;*.mp4";
                                        openFileDialog.Title = "Select the Internal Video";

                                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                                        {
                                            string selectedVideoFile = openFileDialog.FileName;

                                            // Ensure the corresponding folder exists
                                            if (!Directory.Exists(correspondingFolder))
                                            {
                                                Directory.CreateDirectory(correspondingFolder);
                                            }

                                            // Extract the .intpak file to the corresponding folder
                                            System.IO.Compression.ZipFile.ExtractToDirectory(file, correspondingFolder);

                                            // Create the direct.json file
                                            string directJsonContent = $"{{\n  \"Directory\": \"{selectedVideoFile.Replace("\\", "\\\\")}\"\n}}";
                                            File.WriteAllText(Path.Combine(correspondingFolder, "direct.json"), directJsonContent);

                                            // Restart
                                            Application.Restart();
                                            Environment.Exit(0);
                                        }
                                    }
                                });

                                thread.SetApartmentState(ApartmentState.STA);
                                thread.Start();
                            }
                        };
                    }
                };

                filePanel.Controls.Add(buttonPictureBox);
                filePanel.Controls.Add(fileLabel);
                leftPanel.Controls.Add(filePanel);
            }
        }

        mainPanel.Controls.Add(leftPanel, 0, 0);
        mainPanel.Controls.Add(rightPanel, 1, 0);
        form.Controls.Add(mainPanel);

        form.ShowDialog();
    }
}
