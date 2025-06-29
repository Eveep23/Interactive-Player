using System;
using System.Drawing;
using System.Windows.Forms;

public class LoadingForm : Form
{
    public static bool ForceMCSMLoading { get; set; }
    private Timer timer;
    private Image loadingImage;
    private float angle = 0f;
    private int spinnerSize = 128;
    private bool useGif = false;
    private PictureBox gifBox;

    public LoadingForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(41, 41, 41);
        TransparencyKey = Color.FromArgb(41, 41, 41);
        Width = 240;
        Height = 240;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Opacity = 0.8;

        if (ForceMCSMLoading)
        {
            useGif = true;
        }
        else
        {
            // Determine if the current movie folder is inside "MCSM"
            string movieFolder = Utilities.SelectedMovieFolder;
            if (!string.IsNullOrEmpty(movieFolder))
            {
                var dir = new System.IO.DirectoryInfo(movieFolder);
                while (dir != null)
                {
                    if (dir.Name.Equals("MCSM", StringComparison.OrdinalIgnoreCase))
                    {
                        useGif = true;
                        break;
                    }
                    dir = dir.Parent;
                }
            }
        }

        if (useGif)
        {
            int gifSize = 120;
            Opacity = 0.65;

            gifBox = new PictureBox
            {
                Image = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "general", "loading.gif")),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Width = gifSize,
                Height = gifSize,
                Left = (Width - gifSize) / 2,
                Top = (Height - gifSize) / 2
            };
            Controls.Add(gifBox);
        }
        else
        {
            loadingImage = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "general", "loading.png"));

            timer = new Timer { Interval = 16 };
            timer.Tick += (s, e) =>
            {
                angle += 6f;
                if (angle >= 360f) angle -= 360f;
                Invalidate();
            };
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!useGif && timer != null)
            timer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!useGif && timer != null)
        {
            timer.Stop();
            loadingImage.Dispose();
        }
        base.OnFormClosing(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (useGif) return;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        // Center spinner
        int x = (Width - spinnerSize) / 2;
        int y = (Height - spinnerSize) / 2;

        g.TranslateTransform(Width / 2, Height / 2);
        g.RotateTransform(angle);
        g.TranslateTransform(-spinnerSize / 2, -spinnerSize / 2);
        g.DrawImage(loadingImage, 0, 0, spinnerSize, spinnerSize);
    }
}