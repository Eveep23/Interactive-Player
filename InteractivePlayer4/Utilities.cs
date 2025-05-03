using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public static class Utilities
{
    public static string SelectedMovieFolder { get; private set; }

    private static Dictionary<string, Image> compositeImageCache = new Dictionary<string, Image>();

    private static Image CreateCompositeImage(string backdropPath, string logoPath)
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
                        // Scale the logo to 1.5 times the backdrop width
                        int logoWidth = (int)(compositeImage.Width / 1.35);
                        int logoHeight = logo.Height * logoWidth / logo.Width; // Maintain aspect ratio
                        int logoX = (compositeImage.Width - logoWidth) / 2; // Center horizontally
                        int logoY = compositeImage.Height - logoHeight - 20;

                        g.DrawImage(logo, logoX, logoY, logoWidth, logoHeight);
                    }
                }
            }
            return compositeImage;
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
            Padding = new Padding(0, 50, 0, 0) // Center the buttons vertically
        };

        Label footerLabel = new Label
        {
            Text = "Interactive Player 1.5.57 Preview developed by Eveep23",
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
            SettingsMenu.ShowSettingsMenu();
        };

        addButtonPictureBox.Click += (sender, e) =>
        {
            InstallInteractives.ShowInstallInteractivesMenu();
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

        if (movieFolders.Length == 0)
        {
            MessageBox.Show("No Interactives Installed (Found).");
        }

        // Read the JSON files in the Packs folder and extract the "Category" field
        var folderCategories = new Dictionary<string, string>();
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
            }
        }

        // Group the movie folders based on the "Category" field
        var groupedFolders = movieFolders.GroupBy(folder =>
        {
            var folderName = Path.GetFileName(folder);
            return folderCategories.TryGetValue(folderName, out var category) ? category : "Uncategorized";
        }).OrderBy(g => g.Key);

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
            int rowPanelWidth = (buttonWidth + buttonSpacing) * totalButtons - buttonSpacing;

            Panel rowPanel = new Panel
            {
                Height = 300,
                Width = Math.Max(rowPanelWidth, rowContainer.Width - 100),
                AutoScroll = false,
                Location = new Point(50, 0)
            };

            // Add left and right navigation buttons
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


            // Add scrolling functionality to the buttons with smooth animation and "boing" effect
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

                // Generate or retrieve the composite image
                if (!compositeImageCache.TryGetValue(folder, out Image compositeImage))
                {
                    compositeImage = CreateCompositeImage(backdropPath, movieLogoPath);
                    compositeImageCache[folder] = compositeImage;
                }

                RoundedButton button = new RoundedButton
                {
                    Width = buttonWidth,
                    Height = 238,
                    BackgroundImage = compositeImage,
                    BackgroundImageLayout = ImageLayout.Stretch,
                    Text = string.IsNullOrEmpty(movieLogoPath) ? folderName : string.Empty,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 0 },
                    Location = new Point(xOffset, 30)
                };

                button.Click += (sender, e) =>
                {
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

                rowPanel.Controls.Add(button);
                xOffset += buttonWidth + buttonSpacing;
            }
        }

        mainPanel.Controls.Add(footerLabel);
        mainPanel.Controls.Add(footerPanel);

        return form.ShowDialog() == DialogResult.OK ? SelectedMovieFolder : null;
    }
}

public class RoundedButton : Button
{
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
    }
}