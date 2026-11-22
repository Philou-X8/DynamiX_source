using DynamiX_HMI.Config;
using DynamiX_HMI.Serial;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace DynamiX_HMI.Menus
{
    /// <summary>
    /// The options page, shown in place of the graphs. Left: every setting (AppSettings) in a property grid,
    /// saved to the XML settings file at each change. Right: the tools that set the sensor calibration from
    /// live readings, the force limit and the ESP32 reboot.
    ///
    /// - The force limit always uses the force calibration, whatever 'Convert to units' says. It is sent to the
    ///   ESP32 as a raw window: the counts for -limit and +limit around the load cell's absolute zero.
    /// - A change that can't be used (a text that is not a number, a zero scale) is put back to its last good value.
    /// Everything runs on the UI thread.
    /// </summary>
    public class OptionPanel : UserControl
    {
        private const int AverageSamples = 3; // live readings come every 200 ms: about the last 0.6 s

        private Action<CommandPacket>? serialCommandHandle;
        private (int min, int max)? lastSentFailsafe;

        private readonly PropertyGrid grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            PropertySort = PropertySort.Categorized,
            ToolbarVisible = false,
            HelpVisible = true,
        };
        private readonly Label labelForce = new Label { AutoSize = true };
        private readonly Label labelPosition = new Label { AutoSize = true };
        private readonly Label labelStatus = new Label { Dock = DockStyle.Fill, AutoSize = false, Height = 40 };
        private readonly TextBox textKnownForce = new TextBox { Text = "100", Width = 70 };
        private readonly TextBox textPositionA = new TextBox { Text = "0", Width = 70 };
        private readonly TextBox textPositionB = new TextBox { Text = "25", Width = 70 };
        private readonly Label labelCaptured = new Label { AutoSize = true, Text = "A: -    B: -" };

        private readonly Queue<(int position, int force)> recent = new Queue<(int, int)>();
        private (double counts, double mm)? pointA, pointB;

        /// <summary> A setting changed (and was saved): the graphs, the analysis and the units must follow. </summary>
        public event Action? SettingsChanged;

        public OptionPanel()
        {
            Dock = DockStyle.Fill;
            grid.SelectedObject = AppSettings.Current;
            grid.PropertyValueChanged += (s, e) => OnSettingEdited(e.ChangedItem?.Label ?? "setting");

            // One tab per job: the settings, the calibration tools, the machine and files
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var pageSettings = new TabPage("Settings");
            pageSettings.Controls.Add(grid);
            tabs.TabPages.Add(pageSettings);
            BuildToolTabs(tabs);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.Controls.Add(tabs, 0, 0);
            layout.Controls.Add(labelStatus, 0, 1);
            Controls.Add(layout);

            if (AppSettings.StartupError.Length > 0)
            {
                Status(AppSettings.StartupError);
            }
            else
            {
                Status("Settings file: " + AppSettings.DefaultPath);
            }
            SetLiveRaw(0, 0, false);
        }

        /// <summary> Where the panel's commands go (the serial link to the ESP32). </summary>
        public void SetSerialHandle(Action<CommandPacket> commandDelegate)
        {
            serialCommandHandle = commandDelegate;
        }

        // ------------------------------------------------------------------ tools

        private static TabPage ToolPage(string title, params Control[] groups)
        {
            var page = new TabPage(title);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            };
            flow.Controls.AddRange(groups);
            page.Controls.Add(flow);
            return page;
        }

        private void BuildToolTabs(TabControl tabs)
        {
            GroupBox live = Group("Live readings");
            live.Controls.Add(Stack(labelForce, labelPosition));

            GroupBox force = Group("Force calibration");
            var zero = Button("Zero force (nothing on the load cell)");
            zero.Click += (s, e) => ZeroForce();
            var setGain = Button("Set gain from the known force");
            setGain.Click += (s, e) => SetForceGain();
            force.Controls.Add(Stack(
                Hint("1. With NOTHING on the load cell: press Zero."),
                zero,
                Hint("2. Put a known weight or force on it, enter it:"),
                Row(new Label { Text = "Known force (N)", AutoSize = true, Anchor = AnchorStyles.Left }, textKnownForce),
                setGain));

            GroupBox position = Group("Position calibration");
            var captureA = Button("Capture A");
            captureA.Click += (s, e) => CapturePosition(true);
            var captureB = Button("Capture B");
            captureB.Click += (s, e) => CapturePosition(false);
            position.Controls.Add(Stack(
                Hint("Move the stroke to two known positions, enter each position, capture it. The scale is computed once both are captured."),
                Row(new Label { Text = "Position A (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, textPositionA, captureA),
                Row(new Label { Text = "Position B (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, textPositionB, captureB),
                labelCaptured));

            GroupBox esp = Group("ESP32");
            var resend = Button("Send the force limit again");
            resend.Click += (s, e) => { SendFailsafe(); Status("Force limit sent"); };
            var reboot = Button("Reboot the ESP32");
            reboot.Click += (s, e) => serialCommandHandle?.Invoke(new CommandPacket(CommandFlags.GUI_force_reboot));
            esp.Controls.Add(Stack(resend, reboot));

            GroupBox files = Group("Files");
            var openFolder = Button("Open the data folder");
            openFolder.Click += (s, e) => OpenDataFolder();
            var reload = Button("Reload the settings file");
            reload.Click += (s, e) => ReloadSettings();
            var reset = Button("Reset the sensor calibration to the defaults");
            reset.Click += (s, e) => ResetCalibration();
            files.Controls.Add(Stack(openFolder, reload, reset));

            tabs.TabPages.Add(ToolPage("Sensor calibration", live, force, position));
            tabs.TabPages.Add(ToolPage("ESP32 and files", esp, files));
        }

        private static GroupBox Group(string text)
        {
            return new GroupBox { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = 330, Padding = new Padding(6) };
        }

        private static Button Button(string text)
        {
            return new Button { Text = text, AutoSize = true, Margin = new Padding(2, 3, 2, 3) };
        }

        private static Label Hint(string text)
        {
            return new Label { Text = text, AutoSize = true, MaximumSize = new Size(310, 0), ForeColor = SystemColors.GrayText };
        }

        private static Control Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
            row.Controls.AddRange(controls);
            return row;
        }

        private static Control Stack(params Control[] controls)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            };
            panel.Controls.AddRange(controls);
            return panel;
        }

        // ------------------------------------------------------------------ live readings

        /// <summary> The newest raw position and force (ESP_live_data), shown and averaged for the calibration tools. </summary>
        public void SetLiveRaw(int position, int force, bool valid = true)
        {
            if (valid)
            {
                recent.Enqueue((position, force));
                while (recent.Count > AverageSamples)
                {
                    recent.Dequeue();
                }
            }
            AppSettings set = AppSettings.Current;
            if (recent.Count == 0)
            {
                labelForce.Text = "Force: no reading yet";
                labelPosition.Text = "Position: no reading yet";
                return;
            }
            double f = recent.Average(r => (double)r.force);
            double p = recent.Average(r => (double)r.position);
            labelForce.Text = string.Format(CultureInfo.CurrentCulture, "Force      {0:0} counts = {1:0.0} N", f, (f - set.ForceZeroCounts) * set.ForceNPerCount);
            labelPosition.Text = string.Format(CultureInfo.CurrentCulture, "Position   {0:0} counts = {1:0.000} mm", p, (p - set.PositionZeroCounts) * set.PositionMmPerCount);
        }

        private bool TryAverage(out double force, out double position)
        {
            force = position = 0;
            if (recent.Count == 0)
            {
                Status("No live reading yet: connect to the machine first");
                return false;
            }
            force = recent.Average(r => (double)r.force);
            position = recent.Average(r => (double)r.position);
            return true;
        }

        // ------------------------------------------------------------------ calibration actions

        private void ZeroForce()
        {
            if (!TryAverage(out double counts, out _))
            {
                return;
            }
            AppSettings set = AppSettings.Current;
            set.ForceZeroCounts = Math.Round(counts, 1);
            set.ForceZeroInfo = Stamp("zero at " + set.ForceZeroCounts.ToString("0.#", CultureInfo.InvariantCulture) + " counts");
            Applied("Force zero set");
        }

        private void SetForceGain()
        {
            if (!TryAverage(out double counts, out _) || !TryNumber(textKnownForce, out double newtons) || newtons == 0)
            {
                Status("Enter the known force in N (not zero)");
                return;
            }
            AppSettings set = AppSettings.Current;
            double delta = counts - set.ForceZeroCounts;
            if (Math.Abs(delta) < 200)
            {
                Status("The reading is too close to the zero: apply the known force first (or press Zero with no load)");
                return;
            }
            set.ForceNPerCount = newtons / delta;
            set.ForceGainInfo = Stamp(newtons.ToString("0.###", CultureInfo.InvariantCulture) + " N at " + counts.ToString("0", CultureInfo.InvariantCulture) + " counts");
            Applied("Force gain set: " + set.ForceNPerCount.ToString("0.0000000", CultureInfo.InvariantCulture) + " N/count");
        }

        private void CapturePosition(bool isA)
        {
            if (!TryAverage(out _, out double counts) || !TryNumber(isA ? textPositionA : textPositionB, out double mm))
            {
                Status("Enter the position in mm");
                return;
            }
            if (isA)
            {
                pointA = (counts, mm);
            }
            else
            {
                pointB = (counts, mm);
            }
            labelCaptured.Text = "A: " + Describe(pointA) + "    B: " + Describe(pointB);
            if (pointA == null || pointB == null)
            {
                Status("Captured. Now capture the other position");
                return;
            }
            double dCounts = pointB.Value.counts - pointA.Value.counts;
            double dMm = pointB.Value.mm - pointA.Value.mm;
            if (Math.Abs(dCounts) < 1000 || dMm == 0)
            {
                Status("The two positions are too close: move the stroke further between them");
                return;
            }
            AppSettings set = AppSettings.Current;
            set.PositionMmPerCount = dMm / dCounts;
            set.PositionZeroCounts = Math.Round(pointA.Value.counts - pointA.Value.mm / set.PositionMmPerCount, 1);
            set.PositionInfo = Stamp("A " + Describe(pointA) + ", B " + Describe(pointB));
            Applied("Position scale set: " + set.PositionMmPerCount.ToString("0.000000000", CultureInfo.InvariantCulture) + " mm/count");
        }

        private static string Describe((double counts, double mm)? point)
        {
            return point == null ? "-" : point.Value.mm.ToString("0.##", CultureInfo.InvariantCulture) + " mm @ " + point.Value.counts.ToString("0", CultureInfo.InvariantCulture);
        }

        private void ResetCalibration()
        {
            if (MessageBox.Show(this, "Put the force and position calibration back to the defaults?", "Reset calibration",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            var defaults = new AppSettings();
            AppSettings set = AppSettings.Current;
            set.ForceNPerCount = defaults.ForceNPerCount;
            set.ForceZeroCounts = defaults.ForceZeroCounts;
            set.PositionMmPerCount = defaults.PositionMmPerCount;
            set.PositionZeroCounts = defaults.PositionZeroCounts;
            set.ForceZeroInfo = set.ForceGainInfo = set.PositionInfo = Stamp("reset to the defaults");
            Applied("Calibration reset");
        }

        private static string Stamp(string how)
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ", " + how;
        }

        /// <summary> After a calibration tool changed the settings: history, file, force limit, graphs. </summary>
        private void Applied(string what)
        {
            AppSettings set = AppSettings.Current;
            try
            {
                set.AppendCalibrationHistory(what);
            }
            catch (Exception ex)
            {
                Status("Calibration history not written: " + ex.Message);
            }
            Commit(what);
        }

        // ------------------------------------------------------------------ settings

        private void OnSettingEdited(string label)
        {
            Commit(label + " changed");
        }

        /// <summary> Validates, saves the file, updates the force limit on the ESP32, and tells the rest of the program. </summary>
        private void Commit(string what)
        {
            AppSettings set = AppSettings.Current;
            set.Sanitize();
            try
            {
                set.Save(AppSettings.DefaultPath);
                Status(what + ". Saved to " + AppSettings.DefaultPath);
            }
            catch (Exception ex)
            {
                Status(what + ", but the settings file could not be written: " + ex.Message);
            }
            grid.Refresh();
            SetLiveRaw(0, 0, false);

            set.ForceLimitCounts(out int lo, out int hi);
            if (lo < hi && lastSentFailsafe != (lo, hi))
            {
                SendFailsafe();
            }
            SettingsChanged?.Invoke();
        }

        private void ReloadSettings()
        {
            AppSettings.Current = AppSettings.Load(AppSettings.DefaultPath, out string error);
            grid.SelectedObject = AppSettings.Current;
            Status(error.Length > 0 ? error : "Settings reloaded from " + AppSettings.DefaultPath);
            AppSettings.Current.ForceLimitCounts(out int lo, out int hi);
            if (lastSentFailsafe != (lo, hi))
            {
                SendFailsafe();
            }
            SettingsChanged?.Invoke();
        }

        private void OpenDataFolder()
        {
            try
            {
                string folder = AppSettings.Current.DataFolder;
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception ex)
            {
                Status("Could not open the data folder: " + ex.Message);
            }
        }

        /// <summary> Sends GUI_set_failsafe: [min][max], int24 big-endian. Also call it on each connection. </summary>
        public void SendFailsafe()
        {
            AppSettings.Current.ForceLimitCounts(out int minForce, out int maxForce);
            if (minForce >= maxForce)
            {
                return;
            }
            byte[] payload =
            {
                (byte)(minForce >> 16), (byte)(minForce >> 8), (byte)minForce,
                (byte)(maxForce >> 16), (byte)(maxForce >> 8), (byte)maxForce,
            };
            // The encoder sends each char as one byte, so Latin1 keeps all 256 values intact.
            serialCommandHandle?.Invoke(new CommandPacket(CommandFlags.GUI_set_failsafe, Encoding.Latin1.GetString(payload)));
            lastSentFailsafe = (minForce, maxForce);
        }

        // ------------------------------------------------------------------ helpers

        private void Status(string text)
        {
            labelStatus.Text = text;
        }

        /// <summary> A number with a decimal point or a decimal comma. </summary>
        private static bool TryNumber(TextBox box, out double value)
        {
            string text = box.Text.Trim().Replace(',', '.');
            bool ok = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
            box.BackColor = ok ? SystemColors.Window : Color.MistyRose;
            return ok;
        }
    }
}
