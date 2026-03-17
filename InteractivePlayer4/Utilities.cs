using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static class Utilities
{
    public static string SelectedMovieFolder { get; private set; }
    public static bool LowEndHardware { get; private set; }

    /*
    private static Dictionary<string, Image> compositeImageCache = new Dictionary<string, Image>();

    private static Image CreateCompositeImage(string backdropPath, string logoPath, bool centerLogo = false)
    {
        // Load the backdrop image
        using (Image backdrop = Image.FromFile(backdropPath))
        {
            Bitmap compositeImage = new Bitmap(backdrop.Width, backdrop.Height);
            using (Graphics g = Graphics.FromImage(compositeImage))
            {
                // Draw the backdrop
                g.DrawImage(backdrop, 0, 0, backdrop.Width, backdrop.Height);

                // If a logo exists, draw it on top of the backdrop
                if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                {
                    using (Image logo = Image.FromFile(logoPath))
                    {
                        int logoWidth = (int)(compositeImage.Width / 1.35);
                        int logoHeight = logo.Height * logoWidth / logo.Width;
                        int logoX;
                        int logoY = compositeImage.Height - logoHeight - 20;

                        if (centerLogo)
                        {
                            logoX = (compositeImage.Width - logoWidth) / 2;
                        }
                        else
                        {
                            logoX = 35; // Padding from the left
                        }

                        g.DrawImage(logo, logoX, logoY, logoWidth, logoHeight);
                    }
                }
            }
            return compositeImage;
        }
    }
    */
    public static Image LoadImageUnlocked(string path)
    {
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                ms.Position = 0;
                return Image.FromStream(ms);
            }
        }
    }
    public static string ShowMovieSelectionMenu(string initialDirectory = null)
    {
        string currentDirectory = initialDirectory ?? Directory.GetCurrentDirectory();
        string mainDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string packsDirectory = Path.Combine(mainDirectory, "Packs");
        string[] movieFolders = Directory.GetDirectories(currentDirectory);
        movieFolders = movieFolders.Where(folder =>
            !Path.GetFileName(folder).Equals("libvlc", StringComparison.OrdinalIgnoreCase) &&
            (Directory.GetFiles(folder, "*.mkv").Concat(Directory.GetFiles(folder, "*.mp4")).Any() && Directory.GetFiles(folder, "*.json").Any() ||
            Directory.GetFiles(folder, "direct.json").Any() ||
            Directory.GetFiles(folder, "backdrop.jpg").Any() && Directory.GetFiles(folder, "logo.png").Any())).ToArray();

        string defaultBackdropPath = Path.Combine(currentDirectory, "general", "Default_backdrop.png");
        string topBarPath = Path.Combine(currentDirectory, "general", "Top_bar.png");
        string logoPath = Path.Combine(currentDirectory, "general", "Interactive_player_logo.png");
        string settingsWheelPath = Path.Combine(currentDirectory, "general", "Settings_Wheel.png");
        string addButtonPath = Path.Combine(currentDirectory, "general", "Add_Button.png");
        string configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

        try
        {
            var config = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(configPath));
            LowEndHardware = config["LowEndHardware"]?.ToObject<bool>() ?? false;
        }
        catch
        {
            LowEndHardware = false;
        }

        Form form = new Form
        {
            Text = "Interactive Player",
            Size = new Size(1400, 750),
            StartPosition = FormStartPosition.CenterScreen,
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
            BackColor = Path.GetFileName(currentDirectory).Equals("MCSM", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#2a262a") :
                        Path.GetFileName(currentDirectory).Equals("BK", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#3cd8a9") : ColorTranslator.FromHtml("#141414"),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
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

        PictureBox addButtonPictureBox = new PictureBox
        {
            Image = Image.FromFile(addButtonPath),
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        FlowLayoutPanel mainPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 30, 0, 0)
        };

        Label footerLabel = new Label
        {
            Text = "Interactive Player 2.0.64 Preview developed by Eveep23",
            Font = new Font("Arial", 10, FontStyle.Italic),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Height = 30,
            AutoSize = true,
            Width = mainPanel.Width - 20
        };

        Panel footerPanel = new Panel
        {
            Height = 50,
            Dock = DockStyle.Bottom
        };

        settingsPictureBox.Click += (sender, e) =>
        {
            form.Hide();
            SettingsMenu.ShowSettingsMenu();
            form.Show();
        };

        addButtonPictureBox.Click += (sender, e) =>
        {
            form.Hide();
            InstallInteractives.ShowInstallInteractivesMenu();
            form.Show();
        };

        topBarPanel.Controls.Add(logoPictureBox);
        topBarPanel.Controls.Add(settingsPictureBox);
        topBarPanel.Controls.Add(addButtonPictureBox);
        logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
        settingsPictureBox.Location = new Point(topBarPanel.Width - settingsPictureBox.Width - 10, (topBarPanel.Height - settingsPictureBox.Height) / 2);
        addButtonPictureBox.Location = new Point(10, (topBarPanel.Height - addButtonPictureBox.Height) / 2);
        topBarPanel.Resize += (sender, e) =>
        {
            logoPictureBox.Location = new Point((topBarPanel.Width - logoPictureBox.Width) / 2, (topBarPanel.Height - logoPictureBox.Height) / 2);
            settingsPictureBox.Location = new Point(topBarPanel.Width - settingsPictureBox.Width - 10, (topBarPanel.Height - settingsPictureBox.Height) / 2);
            addButtonPictureBox.Location = new Point(10, (topBarPanel.Height - addButtonPictureBox.Height) / 2);
        };

        form.Controls.Add(mainPanel);
        form.Controls.Add(topBarPanel);

        /*
        if (movieFolders.Length == 0)
        {
            MessageBox.Show("No Interactives Installed (Found).");
        }
        */

        // Read the JSON files in the Packs folder and extract the "Category" field
        var folderCategories = new Dictionary<string, string>();
        var packJsons = new Dictionary<string, JObject>();
        if (Directory.Exists(packsDirectory))
        {
            var jsonFiles = Directory.GetFiles(packsDirectory, "*.json");
            foreach (var jsonFile in jsonFiles)
            {
                var jsonData = JObject.Parse(File.ReadAllText(jsonFile));
                var category = jsonData["Category"]?.ToString();
                var folderName = Path.GetFileNameWithoutExtension(jsonFile);
                if (!string.IsNullOrEmpty(category))
                {
                    folderCategories[folderName] = category;
                }
                packJsons[folderName] = jsonData;
            }
        }

        // Group the movie folders based on the "Category" field
        var groupedFolders = movieFolders.GroupBy(folder =>
        {
            var folderName = Path.GetFileName(folder);
            return folderCategories.TryGetValue(folderName, out var category) ? category : "Uncategorized";
        }).OrderBy(g => g.Key, new NaturalStringComparer());

        var packButtonsByCategory = new Dictionary<string, List<Control>>();
        if (Directory.Exists(packsDirectory))
        {
            var packJsonFiles = Directory.GetFiles(packsDirectory, "*.json");
            foreach (var packJsonFile in packJsonFiles)
            {
                string[] excludedGrayscalePacks = {
                    "A Date With Markiplier",
                    "Triviaverse",
                    "In Space With Markiplier Part 1",
                    "In Space With Markiplier Part 2"
                };

                var packName = Path.GetFileNameWithoutExtension(packJsonFile);

                // If a folder with this name already exists, skip (it will be shown as a folder)
                if (movieFolders.Any(f => Path.GetFileName(f).Equals(packName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (excludedGrayscalePacks.Contains(packName, StringComparer.OrdinalIgnoreCase))
                    continue;

                // Filtering logic
                string currentFolderName = Path.GetFileName(currentDirectory);
                bool show = true;

                if (currentDirectory == Directory.GetCurrentDirectory())
                {
                    // Main menu: filter out packs with these substrings
                    if (packName.Contains("Battle Kitty E") ||
                        packName.Contains("You vs Wild EP") ||
                        packName.Contains("Minecraft Story Mode Ep") ||
                        packName.Contains("Trivia Quest E"))
                    {
                        show = false;
                    }
                }
                else if (currentFolderName.Equals("BK", StringComparison.OrdinalIgnoreCase))
                {
                    show = packName.Contains("Battle Kitty E");
                }
                else if (currentFolderName.Equals("TQ", StringComparison.OrdinalIgnoreCase))
                {
                    show = packName.Contains("Trivia Quest E");
                }
                else if (currentFolderName.Equals("YvW", StringComparison.OrdinalIgnoreCase))
                {
                    show = packName.Contains("You vs Wild EP");
                }
                // else: show all packs

                if (!show)
                    continue;

                var jsonData = packJsons[packName];
                var category = jsonData["Category"]?.ToString() ?? "Uncategorized";
                var title = jsonData["title"]?.ToString() ?? packName;
                var description = jsonData["description"]?.ToString() ?? "";
                var imagePath = Path.Combine(packsDirectory, packName + ".png");
                Image packImage = null;
                if (File.Exists(imagePath))
                {
                    packImage = Image.FromFile(imagePath);
                }
                else
                {
                    packImage = new Bitmap(424, 238); // fallback blank
                }

                // Create a button for the pack
                RoundedButton packButton = new RoundedButton
                {
                    Width = 424,
                    Height = 238,
                    BackgroundImage = GrayscaleImage(packImage),
                    BackgroundImageLayout = ImageLayout.Stretch,
                    Text = string.Empty,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 0 },
                    Tag = packName,
                    IsGrayscaled = true
                };

                // Tooltip for title/description
                var toolTip = new ToolTip();
                toolTip.SetToolTip(packButton, $"{title}");

                packButton.Click += (sender, e) =>
                {
                    // Open InteractiveDetailsMenu for this pack
                    string folderPath = Path.Combine(currentDirectory, packName);
                    InteractiveDetailsMenu.ShowInteractiveDetailsMenu(folderPath);
                };

                if (!packButtonsByCategory.ContainsKey(category))
                    packButtonsByCategory[category] = new List<Control>();
                packButtonsByCategory[category].Add(packButton);
            }
        }

        foreach (var group in groupedFolders)
        {
            Label groupLabel = new Label
            {
                Text = group.Key.ToString(),
                Font = new Font("Arial", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(10, 10, 10, 0)
            };
            mainPanel.Controls.Add(groupLabel);

            Panel rowContainer = new Panel
            {
                Height = 300,
                Width = 1360,
                AutoScroll = false
            };

            // Calculate the total width of the rowPanel based on the number of buttons
            int buttonWidth = 424;
            int buttonSpacing = 10;
            int totalButtons = group.Count();
            if (packButtonsByCategory.ContainsKey(group.Key))
                totalButtons += packButtonsByCategory[group.Key].Count;
            int rowPanelWidth = (buttonWidth + buttonSpacing) * totalButtons - buttonSpacing;

            Panel rowPanel = new Panel
            {
                Height = 300,
                Width = Math.Max(rowPanelWidth, rowContainer.Width - 100),
                AutoScroll = false,
                Location = new Point(50, 0)
            };

            Button leftButton = new Button
            {
                Width = 50,
                Height = 240,
                BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "Left_Arrow.png")),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                   BorderSize = 0,
                   MouseDownBackColor = Color.Transparent,
                    MouseOverBackColor = Color.Transparent
                },
                BackColor = Color.Transparent,
                Location = new Point(0, (rowContainer.Height - 240) / 2)
            };

            Button rightButton = new Button
            {
                Width = 50,
                Height = 240,
                BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "Right_Arrow.png")),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                  BorderSize = 0,
                  MouseDownBackColor = Color.Transparent,
                  MouseOverBackColor = Color.Transparent
                },
                BackColor = Color.Transparent,
                Location = new Point(rowContainer.Width - 50, (rowContainer.Height - 240) / 2)
            };

            Timer scrollTimer = new Timer { Interval = 15 };
            int scrollStart = 0;
            int scrollTarget = 0;
            int scrollDuration = 500;
            int elapsedTime = 0;

            leftButton.Click += (sender, e) =>
            {
                scrollStart = rowPanel.Left;
                scrollTarget = Math.Min(rowPanel.Left + 500, 50);
                elapsedTime = 0;

                scrollTimer.Start();
            };

            rightButton.Click += (sender, e) =>
            {
                scrollStart = rowPanel.Left;
                scrollTarget = Math.Max(rowPanel.Left - 500, rowContainer.Width - rowPanel.Width - 50);
                elapsedTime = 0;

                scrollTimer.Start();
            };

            // Timer tick event for smooth scrolling with easing
            scrollTimer.Tick += (sender, e) =>
            {
                elapsedTime += scrollTimer.Interval;
                double t = (double)elapsedTime / scrollDuration;

                if (t >= 1.0)
                {
                    rowPanel.Left = scrollTarget;
                    scrollTimer.Stop();
                }
                else
                {
                    double overshoot = 1.70158;
                    t = t - 1;
                    double easedT = (t * t * ((overshoot + 1) * t + overshoot) + 1);

                    rowPanel.Left = (int)(scrollStart + (scrollTarget - scrollStart) * easedT);
                }

                UpdateArrowVisibility();
            };

            void UpdateArrowVisibility()
            {
                leftButton.Visible = rowPanel.Left < 50;

                rightButton.Visible = rowPanel.Width > rowContainer.Width &&
                                      rowPanel.Left > rowContainer.Width - rowPanel.Width - 50;
            }


            UpdateArrowVisibility();

            rowContainer.Controls.Add(leftButton);
            rowContainer.Controls.Add(rightButton);
            rowContainer.Controls.Add(rowPanel);
            mainPanel.Controls.Add(rowContainer);

            int xOffset = 0;
            foreach (var folder in group.OrderBy(f => Path.GetFileName(f)))
            {
                string backdropPath = Directory.GetFiles(folder, "*backdrop.jpg").FirstOrDefault() ?? defaultBackdropPath;
                string movieLogoPath = Directory.GetFiles(folder, "*logo.png").FirstOrDefault();
                string folderName = Path.GetFileName(folder);

                bool isSpecialFolder = folderName.Equals("BK", StringComparison.OrdinalIgnoreCase)
                    || folderName.Equals("MCSM", StringComparison.OrdinalIgnoreCase)
                    || folderName.Equals("TQ", StringComparison.OrdinalIgnoreCase)
                    || folderName.Equals("YvW", StringComparison.OrdinalIgnoreCase);

                bool isEmptyInteractiveFolder;
                if (isSpecialFolder)
                {
                    int subfolderCount = Directory.GetDirectories(folder).Length;
                    isEmptyInteractiveFolder = subfolderCount == 1;
                }
                else
                {
                    isEmptyInteractiveFolder = !File.Exists(Path.Combine(folder, "build.txt"));
                }

                bool centerLogo = false;
                string[] centerLogoFolders = {"YvW", "A Date With Markiplier", "You vs Wild EP1", "You vs Wild EP2", "You vs Wild EP3", "You vs Wild EP4", "You vs Wild EP5", "You vs Wild EP6", "You vs Wild EP7", "You vs Wild EP8", "MCSM", "Black Mirror Bandersnatch", "Captain Underpants Epic Choice-o-Rama"};
                if (centerLogoFolders.Contains(folderName, StringComparer.OrdinalIgnoreCase) ||
                    (folderName.StartsWith("You vs Wild EP", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(folderName.Substring("You vs Wild EP".Length), out int epNum) &&
                     epNum >= 1 && epNum <= 8))
                {
                    centerLogo = true;
                }

                RoundedButton button = new RoundedButton
                {
                    Width = buttonWidth,
                    Height = 238,
                    BackgroundImage = isEmptyInteractiveFolder
                        ? GrayscaleImage(LoadImageUnlocked(backdropPath))
                        : LoadImageUnlocked(backdropPath),
                    BackgroundImageLayout = ImageLayout.Stretch,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 0 },
                    Location = new Point(xOffset, 30),
                    IsGrayscaled = isEmptyInteractiveFolder,
                    LogoPath = isEmptyInteractiveFolder ? null : movieLogoPath,
                    CenterLogo = centerLogo
                };

                button.MouseDown += (sender, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        bool isInteractive =
                            (Directory.GetFiles(folder, "*.mkv").Concat(Directory.GetFiles(folder, "*.mp4")).Any() && Directory.GetFiles(folder, "*.json").Any()) ||
                            Directory.GetFiles(folder, "direct.json").Any();

                        if (isInteractive)
                        {
                            InteractiveDetailsMenu.ShowInteractiveDetailsMenu(folder);
                        }
                    }
                };

                button.Click += (sender, e) =>
                {
                    // Check for update before proceeding
                    if (CheckAndPromptForUpdate(folder, packsDirectory))
                    {
                        return;
                    }

                    if (Path.GetFileName(folder).Equals("MCSM", StringComparison.OrdinalIgnoreCase))
                    {
                        form.Hide();
                        var mcsmMenu = new InteractivePlayer.MCSMMenu();
                        if (mcsmMenu.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(mcsmMenu.SelectedEpisodeFolder))
                        {
                            SelectedMovieFolder = mcsmMenu.SelectedEpisodeFolder;
                            form.DialogResult = DialogResult.OK;
                            form.Close();
                        }
                        form.Show();
                        return;
                    }

                    if (Directory.GetFiles(folder, "*.mkv").Concat(Directory.GetFiles(folder, "*.mp4")).Any() && Directory.GetFiles(folder, "*.json").Any() ||
                                            Directory.GetFiles(folder, "direct.json").Any())
                    {
                        // This is an interactive folder
                        SelectedMovieFolder = folder;
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                    else if (Directory.GetDirectories(folder).Any())
                    {
                        // Open another Movie Selection Menu with the movies in the selected folder
                        form.Hide();
                        SelectedMovieFolder = ShowMovieSelectionMenu(folder);
                        if (SelectedMovieFolder != null)
                        {
                            form.DialogResult = DialogResult.OK;
                            form.Close();
                        }
                        form.Show();
                    }
                    else
                    {
                        // This is a regular movie folder
                        SelectedMovieFolder = folder;
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                };

                rowPanel.Controls.Add(button);
                xOffset += buttonWidth + buttonSpacing;
            }

            if (packButtonsByCategory.ContainsKey(group.Key))
            {
                foreach (var packButton in packButtonsByCategory[group.Key])
                {
                    packButton.Location = new Point(xOffset, 30);
                    rowPanel.Controls.Add(packButton);
                    xOffset += buttonWidth + buttonSpacing;
                }
            }
        }

        var usedCategories = new HashSet<string>(groupedFolders.Select(g => g.Key));
        foreach (var kvp in packButtonsByCategory)
        {
            if (usedCategories.Contains(kvp.Key))
                continue;

            Label groupLabel = new Label
            {
                Text = kvp.Key,
                Font = new Font("Arial", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(10, 10, 10, 0)
            };
            mainPanel.Controls.Add(groupLabel);

            Panel rowContainer = new Panel
            {
                Height = 300,
                Width = 1360,
                AutoScroll = false
            };

            int buttonWidth = 424;
            int buttonSpacing = 10;
            int totalButtons = kvp.Value.Count;
            int rowPanelWidth = (buttonWidth + buttonSpacing) * totalButtons - buttonSpacing;

            Panel rowPanel = new Panel
            {
                Height = 300,
                Width = Math.Max(rowPanelWidth, rowContainer.Width - 100),
                AutoScroll = false,
                Location = new Point(50, 0)
            };

            Button leftButton = new Button
            {
                Width = 50,
                Height = 240,
                BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "Left_Arrow.png")),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                   BorderSize = 0,
                   MouseDownBackColor = Color.Transparent,
                    MouseOverBackColor = Color.Transparent
                },
                BackColor = Color.Transparent,
                Location = new Point(0, (rowContainer.Height - 240) / 2)
            };

            Button rightButton = new Button
            {
                Width = 50,
                Height = 240,
                BackgroundImage = Image.FromFile(Path.Combine(currentDirectory, "general", "Right_Arrow.png")),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                  BorderSize = 0,
                  MouseDownBackColor = Color.Transparent,
                  MouseOverBackColor = Color.Transparent
                },
                BackColor = Color.Transparent,
                Location = new Point(rowContainer.Width - 50, (rowContainer.Height - 240) / 2)
            };

            Timer scrollTimer = new Timer { Interval = 15 };
            int scrollStart = 0;
            int scrollTarget = 0;
            int scrollDuration = 500;
            int elapsedTime = 0;

            leftButton.Click += (sender, e) =>
            {
                scrollStart = rowPanel.Left;
                scrollTarget = Math.Min(rowPanel.Left + 500, 50);
                elapsedTime = 0;

                scrollTimer.Start();
            };

            rightButton.Click += (sender, e) =>
            {
                scrollStart = rowPanel.Left;
                scrollTarget = Math.Max(rowPanel.Left - 500, rowContainer.Width - rowPanel.Width - 50);
                elapsedTime = 0;

                scrollTimer.Start();
            };

            scrollTimer.Tick += (sender, e) =>
            {
                elapsedTime += scrollTimer.Interval;
                double t = (double)elapsedTime / scrollDuration;

                if (t >= 1.0)
                {
                    rowPanel.Left = scrollTarget;
                    scrollTimer.Stop();
                }
                else
                {
                    double overshoot = 1.70158;
                    t = t - 1;
                    double easedT = (t * t * ((overshoot + 1) * t + overshoot) + 1);

                    rowPanel.Left = (int)(scrollStart + (scrollTarget - scrollStart) * easedT);
                }

                UpdateArrowVisibility();
            };

            void UpdateArrowVisibility()
            {
                leftButton.Visible = rowPanel.Left < 50;

                rightButton.Visible = rowPanel.Width > rowContainer.Width &&
                                      rowPanel.Left > rowContainer.Width - rowPanel.Width - 50;
            }

            UpdateArrowVisibility();

            rowContainer.Controls.Add(leftButton);
            rowContainer.Controls.Add(rightButton);
            rowContainer.Controls.Add(rowPanel);
            mainPanel.Controls.Add(rowContainer);

            int xOffset = 0;
            foreach (var packButton in kvp.Value)
            {
                packButton.Location = new Point(xOffset, 30);
                rowPanel.Controls.Add(packButton);
                xOffset += buttonWidth + buttonSpacing;
            }
        }

        mainPanel.Controls.Add(footerLabel);
        mainPanel.Controls.Add(footerPanel);

        return form.ShowDialog() == DialogResult.OK ? SelectedMovieFolder : null;
    }

    private static Bitmap GrayscaleImage(Image src)
    {
        Bitmap grayBmp = new Bitmap(src.Width, src.Height);
        using (Graphics g = Graphics.FromImage(grayBmp))
        {
            var colorMatrix = new System.Drawing.Imaging.ColorMatrix(
                new float[][]
                {
                new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                new float[] {0,      0,      0,      1, 0},
                new float[] {0,      0,      0,      0, 1}
                });
            var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attributes);
        }
        return grayBmp;
    }

    public static bool CheckAndPromptForUpdate(string folder, string packsDirectory)
    {
        string folderName = Path.GetFileName(folder);
        string buildTxtPath = Path.Combine(folder, "build.txt");
        string packJsonPath = Path.Combine(packsDirectory, folderName + ".json");

        if (File.Exists(buildTxtPath) && File.Exists(packJsonPath))
        {
            int currentBuild = 0;
            int newBuild = 0;
            try
            {
                var buildData = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(buildTxtPath));
                currentBuild = buildData["build"]?.ToObject<int>() ?? 0;
            }
            catch { }
            try
            {
                var packData = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(packJsonPath));
                newBuild = packData["build"]?.ToObject<int>() ?? 0;
            }
            catch { }

            if (newBuild > currentBuild)
            {
                var result = MessageBox.Show(
                    "An update is available for this interactive. Not updating can make an interactive break or even unplayable. Would you like to update now? ",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    InteractiveDetailsMenu.ShowInteractiveDetailsMenu(folder);
                    return true;
                }
            }
        }
        return false;
    }
}

public class NaturalStringComparer : IComparer<string>
{
    public int Compare(string a, string b)
    {
        if (a == b) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                long numA = 0, numB = 0;
                int startI = i, startJ = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                long.TryParse(a.Substring(startI, i - startI), out numA);
                long.TryParse(b.Substring(startJ, j - startJ), out numB);
                if (numA != numB)
                    return numA.CompareTo(numB);
            }
            else
            {
                int cmp = a[i].CompareTo(b[j]);
                if (cmp != 0)
                    return cmp;
                i++;
                j++;
            }
        }
        return a.Length.CompareTo(b.Length);
    }
}

public class RoundedButton : Button
{
    private bool _isHovered = false;
    public bool IsGrayscaled { get; set; } = false;

    private Image _cachedLogoImage = null;
    private bool _logoLoaded = false;

    private Timer _gradientTimer;
    private float _gradientAngle = 0f;
    private float _outlineAlpha = 0f;
    private const float FadeSpeed = 0.2f;

    public float LogoScale { get; private set; } = 0.8f;
    private float _logoTargetScale = 0.8f;
    private Timer _logoAnimTimer;
    private const float LogoAnimSpeed = 0.08f;
    public bool CenterLogo { get; set; } = false;

    public string LogoPath { get; set; }
    public string BackdropPath { get; set; }

    public RoundedButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        _gradientTimer = new Timer();
        _gradientTimer.Interval = 50;
        _gradientTimer.Tick += (s, e) =>
        {
            if (_isHovered)
            {
                _gradientAngle += 3f;
                if (_gradientAngle >= 360f) _gradientAngle -= 360f;
            }

            float target = _isHovered ? 1f : 0f;
            if (Math.Abs(_outlineAlpha - target) > 0.01f)
            {
                if (_outlineAlpha < target)
                    _outlineAlpha = Math.Min(_outlineAlpha + FadeSpeed, 1f);
                else
                    _outlineAlpha = Math.Max(_outlineAlpha - FadeSpeed, 0f);
                Invalidate();
            }
            else
            {
                _outlineAlpha = target;
                if (_outlineAlpha == 0f)
                    _gradientTimer.Stop();
            }

            if (_isHovered || _outlineAlpha > 0f)
                Invalidate();
        };

        _logoAnimTimer = new Timer();
        _logoAnimTimer.Interval = 15;
        _logoAnimTimer.Tick += (s, e) =>
        {
            if (Math.Abs(LogoScale - _logoTargetScale) > 0.01f)
            {
                LogoScale += (_logoTargetScale - LogoScale) * LogoAnimSpeed;
                if (Math.Abs(LogoScale - _logoTargetScale) < 0.01f)
                    LogoScale = _logoTargetScale;
                Invalidate();
            }
            else
            {
                LogoScale = _logoTargetScale;
                _logoAnimTimer.Stop();
            }
        };
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        if (!Utilities.LowEndHardware)
        {
            _logoTargetScale = 1.0f;
            _logoAnimTimer.Start();
        }
        _gradientTimer.Start();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        if (!Utilities.LowEndHardware)
        {
            _logoTargetScale = 0.8f;
            _logoAnimTimer.Start();
        }
        _gradientTimer.Start();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);

        GraphicsPath graphicsPath = new GraphicsPath();
        graphicsPath.AddArc(0, 0, 20, 20, 180, 90);
        graphicsPath.AddArc(Width - 20, 0, 20, 20, 270, 90);
        graphicsPath.AddArc(Width - 20, Height - 20, 20, 20, 0, 90);
        graphicsPath.AddArc(0, Height - 20, 20, 20, 90, 90);
        graphicsPath.CloseAllFigures();
        this.Region = new Region(graphicsPath);

        if (_outlineAlpha > 0.01f)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (IsGrayscaled)
            {
                Rectangle rect = new Rectangle(0, 0, Width, Height);
                Color darkerLightGray = Color.FromArgb(180, 180, 180);
                Color darkerGray = Color.FromArgb(80, 80, 80);
                using (var brush = new LinearGradientBrush(
                    rect,
                    darkerLightGray,
                    darkerGray,
                    _gradientAngle))
                using (var pen = new Pen(Color.FromArgb((int)(_outlineAlpha * 255), darkerLightGray), 9))
                {
                    ColorBlend blend = new ColorBlend(2);
                    blend.Colors = new Color[]
                    {
                    Color.FromArgb((int)(_outlineAlpha * 255), darkerLightGray),
                    Color.FromArgb((int)(_outlineAlpha * 255), darkerGray)
                    };
                    blend.Positions = new float[] { 0f, 1f };
                    brush.InterpolationColors = blend;

                    pen.Brush = brush;
                    pevent.Graphics.DrawPath(pen, graphicsPath);
                }
            }
            else
            {
                Rectangle rect = new Rectangle(0, 0, Width, Height);
                using (var brush = new LinearGradientBrush(
                    rect,
                    ColorTranslator.FromHtml("#d22230"),
                    ColorTranslator.FromHtml("#ffffff"),
                    _gradientAngle))
                using (var pen = new Pen(Color.FromArgb((int)(_outlineAlpha * 255), 255, 255, 255), 9))
                {
                    ColorBlend blend = new ColorBlend(2);
                    blend.Colors = new Color[]
                    {
                        Color.FromArgb((int)(_outlineAlpha * 255), ColorTranslator.FromHtml("#d22230")),
                        Color.FromArgb((int)(_outlineAlpha * 255), ColorTranslator.FromHtml("#ffffff"))
                    };
                    blend.Positions = new float[] { 0f, 1f };
                    brush.InterpolationColors = blend;

                    pen.Brush = brush;
                    pevent.Graphics.DrawPath(pen, graphicsPath);
                }
            }
        }

        // Cache logo image
        if (!string.IsNullOrEmpty(LogoPath) && System.IO.File.Exists(LogoPath))
        {
            if (!_logoLoaded || _cachedLogoImage == null)
            {
                _cachedLogoImage?.Dispose();
                _cachedLogoImage = Utilities.LoadImageUnlocked(LogoPath);
                _logoLoaded = true;
            }
        }
        else
        {
            _cachedLogoImage?.Dispose();
            _cachedLogoImage = null;
            _logoLoaded = false;
        }

        if (_cachedLogoImage != null)
        {
            int baseLogoWidth = (int)(Width / 1.35);
            int logoWidth = (int)(baseLogoWidth * LogoScale);
            int logoHeight = _cachedLogoImage.Height * logoWidth / _cachedLogoImage.Width;

            int logoX, logoY;
            if (CenterLogo)
            {
                logoX = (Width - logoWidth) / 2;
                logoY = Height - logoHeight - 20;
            }
            else
            {
                logoX = 35 - (int)((1.0f - LogoScale) * logoWidth * 0.5f);
                logoY = Height - logoHeight - 20;
            }

            pevent.Graphics.DrawImage(_cachedLogoImage, logoX, logoY, logoWidth, logoHeight);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cachedLogoImage?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class ArrowOverlayPanel : Panel
{
    public Image LeftArrowImage { get; set; }
    public Image RightArrowImage { get; set; }
    public event EventHandler LeftArrowClick;
    public event EventHandler RightArrowClick;

    public ArrowOverlayPanel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (LeftArrowImage != null)
        {
            var leftRect = new Rectangle(0, (Height - 240) / 2, 50, 240);
            e.Graphics.DrawImage(LeftArrowImage, leftRect);
        }
        if (RightArrowImage != null)
        {
            var rightRect = new Rectangle(Width - 50, (Height - 240) / 2, 50, 240);
            e.Graphics.DrawImage(RightArrowImage, rightRect);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var leftRect = new Rectangle(0, (Height - 240) / 2, 50, 240);
        var rightRect = new Rectangle(Width - 50, (Height - 240) / 2, 50, 240);

        if (leftRect.Contains(e.Location))
            LeftArrowClick?.Invoke(this, EventArgs.Empty);
        else if (rightRect.Contains(e.Location))
            RightArrowClick?.Invoke(this, EventArgs.Empty);
        else
            base.OnMouseDown(e); // Let clicks through elsewhere
    }

    protected override void WndProc(ref Message m)
    {
        // Let mouse events pass through except for arrow areas
        const int WM_NCHITTEST = 0x84;
        if (m.Msg == WM_NCHITTEST)
        {
            var pos = PointToClient(Cursor.Position);
            var leftRect = new Rectangle(0, (Height - 240) / 2, 50, 240);
            var rightRect = new Rectangle(Width - 50, (Height - 240) / 2, 50, 240);

            if (!leftRect.Contains(pos) && !rightRect.Contains(pos))
            {
                m.Result = (IntPtr)2; // HTTRANSPARENT
                return;
            }
        }
        base.WndProc(ref m);
    }
}