using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace InteractivePlayer
{
    internal class ChangeChoices
    {
        private static IntPtr vlcHandle = IntPtr.Zero;
        private static Timer alignTimer;
        private static UIManager.RECT lastRect;
        private static Form activeForm;

        public static void ShowChangeChoicesMenu(string saveFilePath, string infoJsonFile, Action<SaveData, int?> onSnapshotSelected)
        {
            string movieFolder = Path.GetDirectoryName(saveFilePath);
            string snapshotsPath = Path.Combine(movieFolder, "snapshots.json");

            if (!File.Exists(snapshotsPath))
            {
                MessageBox.Show("No choices made.");
                return;
            }

            var snapshots = JsonConvert.DeserializeObject<Dictionary<string, SaveData>>(File.ReadAllText(snapshotsPath));
            if (snapshots == null || snapshots.Count == 0)
            {
                MessageBox.Show("No choices made.");
                return;
            }

            var segmentInfo = GetSegmentDescriptionsAndStartTimes(infoJsonFile);

            var orderedKeys = snapshots.Keys
                .OrderBy(k => int.Parse(k.Replace("snapshot", "")))
                .ToList();

            Form form = new Form
            {
                Text = "Change Choices",
                Size = new Size(400, 600),
                StartPosition = FormStartPosition.Manual,
                BackColor = ColorTranslator.FromHtml("#141414"),
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                FormBorderStyle = FormBorderStyle.None,
                Opacity = 0.93,
                ShowInTaskbar = false
            };

            ListBox listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 14),
                BackColor = ColorTranslator.FromHtml("#222"),
                ForeColor = Color.White,
                DrawMode = DrawMode.OwnerDrawFixed
            };
            using (var g = listBox.CreateGraphics())
            {
                listBox.ItemHeight = (int)g.MeasureString("Sample", listBox.Font).Height + 8;
            }

            listBox.DrawItem += (sender, e) =>
            {
                e.DrawBackground();
                if (e.Index >= 0)
                {
                    string text = listBox.Items[e.Index].ToString();
                    using (Brush brush = new SolidBrush(listBox.ForeColor))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        e.Graphics.DrawString(
                            text,
                            listBox.Font,
                            brush,
                            e.Bounds,
                            sf
                        );
                    }
                }
                e.DrawFocusRectangle();
            };

            var filteredKeys = orderedKeys
            .Where(key =>
            {
                var segmentId = snapshots[key].CurrentSegment;
                return segmentInfo.ContainsKey(segmentId) && !string.IsNullOrWhiteSpace(segmentInfo[segmentId].Description);
            })
            .ToList();

            foreach (var key in filteredKeys)
            {
                var segmentId = snapshots[key].CurrentSegment;
                string desc = segmentInfo[segmentId].Description;
                listBox.Items.Add($"{desc}");
            }

            listBox.Items.Add("Never mind");

            listBox.DoubleClick += (sender, e) =>
            {
                int selectedIndex = listBox.SelectedIndex;
                if (selectedIndex < 0) return;

                // If "Never mind" is selected, just close the form
                if (selectedIndex == listBox.Items.Count - 1)
                {
                    form.DialogResult = DialogResult.Cancel;
                    form.Close();
                    return;
                }

                string selectedKey = filteredKeys[selectedIndex];
                SaveData selectedSnapshot = snapshots[selectedKey];
                int? startTimeMs = null;
                if (segmentInfo.ContainsKey(selectedSnapshot.CurrentSegment))
                    startTimeMs = segmentInfo[selectedSnapshot.CurrentSegment].StartTimeMs;

                File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(selectedSnapshot, Formatting.Indented));

                var keysToRemove = orderedKeys.Skip(orderedKeys.IndexOf(selectedKey)).ToList();
                foreach (var key in keysToRemove)
                {
                    snapshots.Remove(key);
                }
                File.WriteAllText(snapshotsPath, JsonConvert.SerializeObject(snapshots, Formatting.Indented));

                onSnapshotSelected?.Invoke(selectedSnapshot, startTimeMs);

                form.DialogResult = DialogResult.OK;
                form.Close();
            };

            form.Controls.Add(listBox);

            CenterFormOnVLC(form);

            StartAlignTimer(form);

            form.FormClosed += (s, e) =>
            {
                StopAlignTimer();
            };

            form.ShowDialog();
        }

        private static void CenterFormOnVLC(Form form)
        {
            vlcHandle = FindWindow(null, "VLC (Direct3D11 output)");
            if (vlcHandle != IntPtr.Zero)
            {
                UIManager.RECT rect;
                if (GetWindowRect(vlcHandle, out rect))
                {
                    int vlcWidth = rect.Right - rect.Left;
                    int vlcHeight = rect.Bottom - rect.Top;
                    int formLeft = rect.Left + (vlcWidth - form.Width) / 2;
                    int formTop = rect.Top + (vlcHeight - form.Height) / 2;
                    form.Left = formLeft;
                    form.Top = formTop;
                    lastRect = rect;
                }
            }
        }

        private static void StartAlignTimer(Form form)
        {
            activeForm = form;
            if (alignTimer == null)
            {
                alignTimer = new Timer();
                alignTimer.Interval = 3000;
                alignTimer.Tick += (s, e) =>
                {
                    if (activeForm == null || activeForm.IsDisposed)
                        return;

                    if (vlcHandle == IntPtr.Zero)
                        vlcHandle = FindWindow(null, "VLC (Direct3D11 output)");

                    if (vlcHandle != IntPtr.Zero)
                    {
                        UIManager.RECT rect;
                        if (GetWindowRect(vlcHandle, out rect))
                        {
                            if (rect.Left != lastRect.Left || rect.Top != lastRect.Top ||
                                rect.Right != lastRect.Right || rect.Bottom != lastRect.Bottom)
                            {
                                int vlcWidth = rect.Right - rect.Left;
                                int vlcHeight = rect.Bottom - rect.Top;
                                int formLeft = rect.Left + (vlcWidth - activeForm.Width) / 2;
                                int formTop = rect.Top + (vlcHeight - activeForm.Height) / 2;
                                activeForm.Invoke(new Action(() =>
                                {
                                    activeForm.Left = formLeft;
                                    activeForm.Top = formTop;
                                }));
                                lastRect = rect;
                            }
                        }
                    }
                };
            }
            alignTimer.Start();
        }

        private static void StopAlignTimer()
        {
            if (alignTimer != null)
            {
                alignTimer.Stop();
                alignTimer.Dispose();
                alignTimer = null;
            }
            activeForm = null;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out UIManager.RECT lpRect);

        private static Dictionary<string, (string Description, int? StartTimeMs)> GetSegmentDescriptionsAndStartTimes(string infoJsonFile)
        {
            var result = new Dictionary<string, (string, int?)>();
            if (!File.Exists(infoJsonFile))
                return result;

            var json = File.ReadAllText(infoJsonFile);
            var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);

            var videos = jObj?["jsonGraph"]?["videos"] as Newtonsoft.Json.Linq.JObject;
            if (videos != null && videos.Properties().Any())
            {
                var firstVideo = videos.Properties().First().Value;
                var choicePoints = firstVideo?["interactiveVideoMoments"]?["value"]?["playerControls"]?["choicePointsMetadata"]?["choicePoints"] as Newtonsoft.Json.Linq.JObject;
                if (choicePoints != null)
                {
                    foreach (var prop in choicePoints.Properties())
                    {
                        var desc = prop.Value["description"]?.ToString();
                        int? startTimeMs = null;
                        if (prop.Value["startTimeMs"] != null && int.TryParse(prop.Value["startTimeMs"].ToString(), out int ms))
                            startTimeMs = ms;
                        result[prop.Name] = (desc, startTimeMs);
                    }
                }
            }
            return result;
        }
    }
}