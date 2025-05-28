using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.ComponentModel;

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
        string updateButtonPath = Path.Combine(currentDirectory, "general", "Update_Button.png");
        string bigInstallButtonPath = Path.Combine(currentDirectory, "general", "Big_Install_Button.png");
        string bigUninstallButtonPath = Path.Combine(currentDirectory, "general", "Big_Uninstall_Button.png");
        string bigUpdateButtonPath = Path.Combine(currentDirectory, "general", "Big_Update_Button.png");

        Form form = new Form
        {
            Text = "Install Interactives",
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
            form.Close();
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
            BackColor = ColorTranslator.FromHtml("#141414"),
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
                else if (fileNameWithoutExtension.StartsWith("Battle Kitty E"))
                {
                    correspondingFolder = Path.Combine(currentDirectory, "BK", fileNameWithoutExtension);
                }
                else if (fileNameWithoutExtension.StartsWith("You vs Wild EP"))
                {
                    correspondingFolder = Path.Combine(currentDirectory, "YvW", fileNameWithoutExtension);
                }
                else if (fileNameWithoutExtension.StartsWith("Trivia Quest E"))
                {
                    correspondingFolder = Path.Combine(currentDirectory, "TQ", fileNameWithoutExtension);
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
                else if (fileNameWithoutExtension.StartsWith("Battle Kitty E"))
                {
                    // Check if there are any JSON files in the folder
                    isInstalled = Directory.Exists(correspondingFolder) && Directory.GetFiles(correspondingFolder, "*.json").Any();
                }
                else if (fileNameWithoutExtension.StartsWith("You vs Wild EP"))
                {
                    // Check if there are any JSON files in the folder
                    isInstalled = Directory.Exists(correspondingFolder) && Directory.GetFiles(correspondingFolder, "*.json").Any();
                }
                else if (fileNameWithoutExtension.StartsWith("Trivia Quest E"))
                {
                    // Check if there are any JSON files in the folder
                    isInstalled = Directory.Exists(correspondingFolder) && Directory.GetFiles(correspondingFolder, "*.json").Any();
                }
                else
                {
                    // Check if the folder exists
                    isInstalled = Directory.Exists(correspondingFolder);
                }

                string buttonImagePath = installButtonPath;
                string bigButtonImagePath = bigInstallButtonPath;

                if (isInstalled)
                {
                    string buildJsonPath = Path.Combine(correspondingFolder, "build.txt");
                    int currentBuild = 0;
                    int newBuild = 0;

                    if (File.Exists(buildJsonPath))
                    {
                        var buildJsonData = JObject.Parse(File.ReadAllText(buildJsonPath));
                        currentBuild = buildJsonData["build"]?.ToObject<int>() ?? 0;
                    }

                    string installJsonPath = Path.Combine(packsDirectory, fileNameWithoutExtension + ".json");
                    if (File.Exists(installJsonPath))
                    {
                        var installJsonData = JObject.Parse(File.ReadAllText(installJsonPath));
                        newBuild = installJsonData["build"]?.ToObject<int>() ?? 0;
                    }

                    if (newBuild > currentBuild)
                    {
                        buttonImagePath = updateButtonPath;
                        bigButtonImagePath = bigUpdateButtonPath;
                    }
                    else
                    {
                        buttonImagePath = uninstallButtonPath;
                        bigButtonImagePath = bigUninstallButtonPath;
                    }
                }

                Panel filePanel = new Panel
                {
                    Width = (int)(leftPanel.ClientSize.Width * 3.25),
                    Height = 80,
                    BackColor = ColorTranslator.FromHtml("#141414"),
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
                    BackColor = ColorTranslator.FromHtml("#141414"),
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
                        int newBuild = jsonData["build"]?.ToObject<int>() ?? 0;

                        displayPictureBox.Image = Image.FromFile(pngFilePath);
                        titleLabel.Text = title;
                        descriptionLabel.Text = description;

                        actionButton.Image = Image.FromFile(bigButtonImagePath);
                        actionButton.Location = new Point((rightPanel.Width - actionButton.Width) / 2, rightPanel.Height - actionButton.Height - 10);

                        // Remove previous event handlers
                        ClearClickEventHandlers(actionButton);

                        actionButton.Click += (s, ev) =>
                        {
                            if (buttonImagePath == uninstallButtonPath)
                            {
                                // Uninstall action: delete the folder and restart
                                Directory.Delete(correspondingFolder, true);
                                Application.Restart();
                                Environment.Exit(0);
                            }
                            else if (buttonImagePath == updateButtonPath)
                            {
                                // Update action: delete all files except direct.json, save.json, and video files
                                var filesToKeep = new[] { "direct.json", "save.json" };
                                var videoExtensions = new[] { ".mkv", ".mp4" };

                                foreach (var filePath in Directory.GetFiles(correspondingFolder))
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
                                System.IO.Compression.ZipFile.ExtractToDirectory(file, tempDirectory);

                                // Move the extracted files to the corresponding folder
                                foreach (var tempFilePath in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
                                {
                                    string relativePath = tempFilePath.Substring(tempDirectory.Length + 1);
                                    string destFilePath = Path.Combine(correspondingFolder, relativePath);
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

                                // Check the OptimizeInteractives setting
                                string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                                bool optimizeInteractives = true; // Default to true if the setting is missing

                                if (File.Exists(configFilePath))
                                {
                                    var configData = JObject.Parse(File.ReadAllText(configFilePath));
                                    optimizeInteractives = configData["OptimizeInteractives"]?.ToObject<bool>() ?? true;
                                }

                                if (optimizeInteractives)
                                {
                                    // Flatten the directory structure
                                    FlattenDirectoryStructure(correspondingFolder);
                                }

                                // Create the build.txt file
                                string buildJsonContent = $"{{\n  \"build\": {newBuild}\n}}";
                                File.WriteAllText(Path.Combine(correspondingFolder, "build.txt"), buildJsonContent);

                                // Restart
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

                                            // Check the OptimizeInteractives setting
                                            string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                                            bool optimizeInteractives = true; // Default to true if the setting is missing

                                            if (File.Exists(configFilePath))
                                            {
                                                var configData = JObject.Parse(File.ReadAllText(configFilePath));
                                                optimizeInteractives = configData["OptimizeInteractives"]?.ToObject<bool>() ?? true;
                                            }

                                            if (optimizeInteractives)
                                            {
                                                // Flatten the directory structure
                                                FlattenDirectoryStructure(correspondingFolder);
                                            }

                                            // Create the direct.json file
                                            string directJsonContent = $"{{\n  \"Directory\": \"{selectedVideoFile.Replace("\\", "\\\\")}\"\n}}";
                                            File.WriteAllText(Path.Combine(correspondingFolder, "direct.json"), directJsonContent);

                                            // Create the build.txt file
                                            string buildJsonContent = $"{{\n  \"build\": {newBuild}\n}}";
                                            File.WriteAllText(Path.Combine(correspondingFolder, "build.txt"), buildJsonContent);

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

    private static void ClearClickEventHandlers(Control control)
    {
        var fieldInfo = typeof(Control).GetField("EventClick", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var eventHandlerList = (EventHandlerList)typeof(Control).GetProperty("Events", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(control, null);
        var eventKey = fieldInfo.GetValue(null);
        eventHandlerList.RemoveHandler(eventKey, eventHandlerList[eventKey]);
    }

    private static void FlattenDirectoryStructure(string rootFolder)
    {
        try
        {
            // Get all subdirectories in a bottom-up order
            var subDirectories = Directory.GetDirectories(rootFolder, "*", SearchOption.AllDirectories)
                                           .OrderByDescending(d => d.Length) // Process deeper directories first
                                           .ToList();

            foreach (var subDirectory in subDirectories)
            {
                if (!Directory.Exists(subDirectory))
                {
                    // Skip if the directory no longer exists
                    continue;
                }

                // Move all files in the subdirectory to the root folder
                foreach (var file in Directory.GetFiles(subDirectory))
                {
                    string destinationFile = Path.Combine(rootFolder, Path.GetFileName(file));

                    try
                    {
                        // Move the file to the root folder
                        if (File.Exists(destinationFile))
                        {
                            File.Delete(destinationFile); // Overwrite if the file already exists
                        }
                        File.Move(file, destinationFile);
                    }
                    catch (Exception ex)
                    {
                        // Log or handle file-specific errors
                        Console.WriteLine($"Error moving file '{file}' to '{destinationFile}': {ex.Message}");
                    }
                }

                try
                {
                    // Delete the empty subdirectory
                    if (!Directory.EnumerateFileSystemEntries(subDirectory).Any())
                    {
                        Directory.Delete(subDirectory, true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting directory '{subDirectory}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error flattening directory structure: {ex.Message}");
        }
    }
}

