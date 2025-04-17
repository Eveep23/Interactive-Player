using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

public class ConsoleWindow : Form
{
    private RichTextBox outputBox;

    public ConsoleWindow()
    {
        // Set up the form
        this.Text = "Interactive Player Console";
        this.Width = 800;
        this.Height = 600;
        this.KeyPreview = true; // Enable key preview for the form

        // Output box
        outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = System.Drawing.Color.Black,
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("Consolas", 10)
        };
        this.Controls.Add(outputBox);

        // Redirect console output to this window
        Console.SetOut(new ConsoleTextWriter(outputBox));
    }

    public void AppendOutput(string message)
    {
        if (outputBox.InvokeRequired)
        {
            outputBox.Invoke(new Action(() => AppendOutput(message)));
        }
        else
        {
            outputBox.AppendText(message + Environment.NewLine);
            outputBox.ScrollToCaret();
        }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Space:
                Console.WriteLine("Space key pressed.");
                // Add logic to handle the Space key
                break;

            case Keys.L:
                Console.WriteLine("L key pressed.");
                // Add logic to handle the L key
                break;

            case Keys.S:
                Console.WriteLine("S key pressed.");
                // Add logic to handle the S key
                break;

            case Keys.C:
                Console.WriteLine("C key pressed.");
                // Add logic to handle the C key
                break;

            default:
                return base.ProcessCmdKey(ref msg, keyData); // Let the base class handle other keys
        }

        return true; // Indicate that the key was handled
    }

    private class ConsoleTextWriter : TextWriter
    {
        private readonly RichTextBox _outputBox;

        public ConsoleTextWriter(RichTextBox outputBox)
        {
            _outputBox = outputBox;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (_outputBox.InvokeRequired)
            {
                _outputBox.Invoke(new Action(() => _outputBox.AppendText(value.ToString())));
            }
            else
            {
                _outputBox.AppendText(value.ToString());
            }
        }

        public override void Write(string value)
        {
            if (_outputBox.InvokeRequired)
            {
                _outputBox.Invoke(new Action(() => _outputBox.AppendText(value)));
            }
            else
            {
                _outputBox.AppendText(value);
            }
        }

        public override void WriteLine(string value)
        {
            Write(value + Environment.NewLine);
        }
    }
}
