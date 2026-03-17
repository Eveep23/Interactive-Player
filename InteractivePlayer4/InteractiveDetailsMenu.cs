using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

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
        string settingsButtonPath = Path.Combine(currentDirectory, "general", "Big_Settings_Button.png");

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

        Label detailsLabel = new Label
        {
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoSize = true,
            Font = new Font("Arial", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomRight,
            Visible = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        displayPictureBox.Controls.Add(detailsLabel);

        detailsLabel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            var rect = detailsLabel.ClientRectangle;

            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                rect,
                Color.FromArgb(0, 154, 27, 43),
                Color.FromArgb(255, 154, 27, 43),
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            {
                g.FillRectangle(brush, rect);
            }

            TextRenderer.DrawText(
                g,
                detailsLabel.Text,
                detailsLabel.Font,
                rect,
                detailsLabel.ForeColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            );
        };

        void PositionDetailsLabel()
        {
            detailsLabel.Location = new Point(
                Math.Max(0, displayPictureBox.Width - detailsLabel.Width - 67),
                Math.Max(0, displayPictureBox.Height - detailsLabel.Height - 3)
            );
        }
        displayPictureBox.Resize += (s, e) => PositionDetailsLabel();
        detailsLabel.SizeChanged += (s, e) => PositionDetailsLabel();

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
            string details = jsonData["details"]?.ToString();

            displayPictureBox.Image = Image.FromFile(pngFilePath);
            titleLabel.Text = title;
            descriptionLabel.Text = description;
            detailsLabel.Text = details;
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

        PictureBox settingsButton = null;

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

            settingsButton = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.AutoSize,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            settingsButton.Image = Image.FromFile(settingsButtonPath);
            settingsButton.Click += (s, e) =>
            {
                try
                {
                    using (var win = new InteractiveSettingsForm(interactiveFolder))
                    {
                        win.ShowDialog(detailsForm);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(detailsForm, "Failed to open settings window: " + ex.Message);
                }
            };
        }

        actionButton.Image = Image.FromFile(buttonImagePath);

        rightPanel.Controls.Add(actionButton);
        if (settingsButton != null)
        {
            rightPanel.Controls.Add(settingsButton);
        }

        void PositionRightPanelButtons()
        {
            const int spacing = 20;
            if (settingsButton != null)
            {
                int totalWidth = actionButton.Width + spacing + settingsButton.Width;
                int startX = Math.Max(0, (rightPanel.Width - totalWidth) / 2);
                int yAction = Math.Max(0, (rightPanel.Height - actionButton.Height) / 2);
                int ySettings = Math.Max(0, (rightPanel.Height - settingsButton.Height) / 2);

                actionButton.Location = new Point(startX, yAction);
                settingsButton.Location = new Point(startX + actionButton.Width + spacing, ySettings);
            }
            else
            {
                actionButton.Location = new Point(
                    (rightPanel.Width - actionButton.Width) / 2,
                    (rightPanel.Height - actionButton.Height) / 2
                );
            }
        }

        rightPanel.Resize += (sender, e) => PositionRightPanelButtons();
        detailsForm.Shown += (s, e) => PositionRightPanelButtons();

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

                // Check the OptimizeInteractives setting
                string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                bool optimizeInteractives = true;

                if (File.Exists(configFilePath))
                {
                    var configData = JObject.Parse(File.ReadAllText(configFilePath));
                    optimizeInteractives = configData["OptimizeInteractives"]?.ToObject<bool>() ?? true;
                }

                if (optimizeInteractives)
                {
                    // Flatten the directory structure
                    InstallInteractives.FlattenDirectoryStructure(interactiveFolder);
                }

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

                            // Check the OptimizeInteractives setting
                            string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                            bool optimizeInteractives = true;

                            if (File.Exists(configFilePath))
                            {
                                var configData = JObject.Parse(File.ReadAllText(configFilePath));
                                optimizeInteractives = configData["OptimizeInteractives"]?.ToObject<bool>() ?? true;
                            }

                            if (optimizeInteractives)
                            {
                                // Flatten the directory structure
                                InstallInteractives.FlattenDirectoryStructure(interactiveFolder);
                            }

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
        
        mainPanel.Controls.Add(leftPanel, 0, 0);
        mainPanel.Controls.Add(rightPanel, 1, 0);
        detailsForm.Controls.Add(mainPanel);

        detailsForm.ShowDialog();
    }
}

public class InteractiveSettingsForm : SettingsForm
{
    public string MovieFolder { get; }

    public InteractiveSettingsForm(string movieFolder)
    {
        MovieFolder = movieFolder;

        string currentDirectory = Directory.GetCurrentDirectory();
        string topBarPath = Path.Combine(currentDirectory, "general", "Top_bar.png");
        string logoPath = Path.Combine(currentDirectory, "general", "Interactive_player_logo.png");

        Text = "Interactive Settings";
        Size = new Size(500, 420);
        StartPosition = FormStartPosition.CenterParent;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BackColor = ColorTranslator.FromHtml("#141414");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        Panel topBarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 100,
            BackgroundImage = File.Exists(topBarPath) ? Image.FromFile(topBarPath) : null,
            BackgroundImageLayout = ImageLayout.Stretch,
            BackColor = Color.Transparent
        };

        PictureBox logoPictureBox = new PictureBox
        {
            Image = File.Exists(logoPath) ? Image.FromFile(logoPath) : null,
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent
        };

        topBarPanel.Controls.Add(logoPictureBox);
        topBarPanel.Resize += (s, e) =>
        {
            logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        };
        logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);

        Controls.Add(topBarPanel);

        var contentPanel = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var titleLabel = new Label
        {
            Text = "Interactive Settings",
            ForeColor = Color.White,
            AutoSize = false,
            Font = new Font("Arial", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Size = new Size(420, 28),
            BackColor = Color.Transparent
        };

        var directoryLabel = new Label
        {
            Text = "Directory:",
            ForeColor = Color.White,
            Font = new Font("Arial", 14, FontStyle.Bold),
            AutoSize = true
        };

        var directoryTextBox = new TextBox
        {
            Width = 320,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Arial", 12),
        };

        var directoryBrowse = new Button
        {
            Text = "Browse...",
            Width = 100,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = ColorTranslator.FromHtml("#d22230"),
            ForeColor = Color.White
        };
        directoryBrowse.FlatAppearance.BorderSize = 0;

        directoryBrowse.Click += (s, e) =>
        {
            string selected = ShowOpenFileDialogSta("Select Video File", "Video Files|*.mkv;*.mp4|All Files|*.*");
            if (!string.IsNullOrEmpty(selected))
            {
                directoryTextBox.Text = selected;
            }
        };

        var overrideLabel = new Label
        {
            Text = "Override Subtitles:",
            ForeColor = Color.White,
            Font = new Font("Arial", 14, FontStyle.Bold),
            AutoSize = true
        };

        var overrideComboBox = new ComboBox
        {
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Arial", 12)
        };

        overrideComboBox.Items.Add("No");
        overrideComboBox.Items.Add("Choose File...");
        overrideComboBox.SelectedIndex = 0;

        int previousSubtitleIndex = overrideComboBox.SelectedIndex;

        overrideComboBox.SelectedIndexChanged += (s, e) =>
        {
            if (overrideComboBox.SelectedItem != null && overrideComboBox.SelectedItem.ToString() == "Choose File...")
            {
                string path = ShowOpenFileDialogSta("Select Subtitle File", "Subtitle Files|*.srt;*.vtt;*.ass;*.sub|All Files|*.*");
                if (!string.IsNullOrEmpty(path))
                {
                    if (!overrideComboBox.Items.Contains(path))
                    {
                        int insertIndex = Math.Max(overrideComboBox.Items.Count - 1, 1);
                        overrideComboBox.Items.Insert(insertIndex, path);
                    }
                    overrideComboBox.SelectedItem = path;
                    previousSubtitleIndex = overrideComboBox.SelectedIndex;
                }
                else
                {
                    overrideComboBox.SelectedIndex = previousSubtitleIndex;
                }
            }
            else
            {
                previousSubtitleIndex = overrideComboBox.SelectedIndex;
            }
        };

        var fileCachingCheckBox = new CheckBox
        {
            Text = "File Caching",
            ForeColor = Color.White,
            Font = new Font("Arial", 14, FontStyle.Bold),
            AutoSize = true,
            Checked = true
        };

        var saveButton = new Button
        {
            Text = "Save",
            Width = 140,
            Height = 36,
            Anchor = AnchorStyles.Bottom,
            BackColor = ColorTranslator.FromHtml("#d22230"),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        saveButton.FlatAppearance.BorderSize = 0;

        try
        {
            string directJsonPath = Path.Combine(MovieFolder, "direct.json");
            if (File.Exists(directJsonPath))
            {
                var direct = JObject.Parse(File.ReadAllText(directJsonPath));
                var dirVal = direct["Directory"]?.ToString() ?? string.Empty;
                directoryTextBox.Text = dirVal;

                var subtitleVal = direct["Subtitle"]?.ToString();
                if (!string.IsNullOrWhiteSpace(subtitleVal))
                {
                    if (!overrideComboBox.Items.Contains(subtitleVal))
                        overrideComboBox.Items.Insert(Math.Max(overrideComboBox.Items.Count - 1, 1), subtitleVal);
                    overrideComboBox.SelectedItem = subtitleVal;
                    previousSubtitleIndex = overrideComboBox.SelectedIndex;
                }
                else
                {
                    overrideComboBox.SelectedIndex = 0; // "No"
                    previousSubtitleIndex = 0;
                }

                bool? cached = direct["File Caching"]?.ToObject<bool?>() ?? direct["FileCaching"]?.ToObject<bool?>();
                if (cached.HasValue)
                    fileCachingCheckBox.Checked = cached.Value;
            }
        }
        catch
        { }

        saveButton.Click += (s, e) =>
        {
            try
            {
                var direct = new JObject
                {
                    ["Directory"] = directoryTextBox.Text ?? string.Empty,
                    ["Subtitle"] = (overrideComboBox.SelectedItem != null && overrideComboBox.SelectedItem.ToString() != "No" && overrideComboBox.SelectedItem.ToString() != "Choose File...")
                                    ? overrideComboBox.SelectedItem.ToString()
                                    : string.Empty,
                    ["File Caching"] = fileCachingCheckBox.Checked
                };

                string directJsonPath = Path.Combine(MovieFolder, "direct.json");

                File.WriteAllText(directJsonPath, direct.ToString(Newtonsoft.Json.Formatting.Indented));

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to save settings: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        contentPanel.Controls.Add(titleLabel);
        contentPanel.Controls.Add(directoryLabel);
        contentPanel.Controls.Add(directoryTextBox);
        contentPanel.Controls.Add(directoryBrowse);
        contentPanel.Controls.Add(overrideLabel);
        contentPanel.Controls.Add(overrideComboBox);
        contentPanel.Controls.Add(fileCachingCheckBox);
        contentPanel.Controls.Add(saveButton);

        Controls.Add(contentPanel);

        this.Shown += (s, e) =>
        {
            int centerX = (this.ClientSize.Width - 360) / 2;

            titleLabel.Location = new Point(centerX, 36);
            titleLabel.Size = new Size(360, 30);

            directoryLabel.Location = new Point(centerX, titleLabel.Bottom + 30);
            directoryTextBox.Location = new Point(centerX, directoryLabel.Bottom + 8);
            directoryTextBox.Width = 360 - 120;
            directoryBrowse.Location = new Point(directoryTextBox.Right + 8, directoryTextBox.Top - 2);

            overrideLabel.Location = new Point(centerX, directoryTextBox.Bottom + 18);
            overrideComboBox.Location = new Point(centerX + 60, overrideLabel.Bottom + 8);
            int maxComboWidth = Math.Max(160, (360 - 60 - 20));
            overrideComboBox.Width = Math.Min(directoryTextBox.Width, maxComboWidth);

            fileCachingCheckBox.Location = new Point(centerX, overrideComboBox.Bottom + 18);

            saveButton.Location = new Point((this.ClientSize.Width - saveButton.Width) / 2, this.ClientSize.Height - saveButton.Height - 18);
        };

        this.Resize += (s, e) =>
        {
            var saveBtn = saveButton;
            saveBtn.Location = new Point((this.ClientSize.Width - saveBtn.Width) / 2, this.ClientSize.Height - saveBtn.Height - 18);
        };
    }

    private static string ShowOpenFileDialogSta(string title, string filter)
    {
        string result = null;
        var thread = new Thread(() =>
        {
            try
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = title;
                    ofd.Filter = filter;
                    ofd.RestoreDirectory = true;
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        result = ofd.FileName;
                    }
                }
            }
            catch
            {
                result = null;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        return result;
    }
}