using System;
using System.Drawing;
using System.Windows.Forms;

public class LoadingForm : Form
{
    private Timer timer;
    private Image loadingImage;
    private float angle = 0f;
    private int spinnerSize = 128;

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

        loadingImage = Image.FromFile(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "general", "loading.png"));

        timer = new Timer { Interval = 16 };
        timer.Tick += (s, e) =>
        {
            angle += 6f;
            if (angle >= 360f) angle -= 360f;
            Invalidate();
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        timer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        timer.Stop();
        loadingImage.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
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