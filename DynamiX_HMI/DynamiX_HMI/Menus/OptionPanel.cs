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
    public partial class OptionPanel : UserControl
    {
        private const int AverageSamples = 3; // live readings come every 200 ms: about the last 0.6 s

        private Action<CommandPacket>? serialCommandHandle;
        private (int min, int max)? lastSentFailsafe;

        private readonly Queue<(int position, int force)> recent = new Queue<(int, int)>();
        private (double counts, double mm)? pointA, pointB;

        /// <summary> A setting changed (and was saved): the graphs, the analysis and the units must follow. </summary>
        public event Action? SettingsChanged;

        public OptionPanel()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
            grid.SelectedObject = AppSettings.Current;

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

        // ------------------------------------------------------------------ events (wired in OptionPanel.Designer.cs)

        private void grid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
        {
            OnSettingEdited(e.ChangedItem?.Label ?? "setting");
        }

        private void buttonZeroForce_Click(object? sender, EventArgs e)
        {
            ZeroForce();
        }

        private void buttonSetGain_Click(object? sender, EventArgs e)
        {
            SetForceGain();
        }

        private void buttonCaptureA_Click(object? sender, EventArgs e)
        {
            CapturePosition(true);
        }

        private void buttonCaptureB_Click(object? sender, EventArgs e)
        {
            CapturePosition(false);
        }

        private void buttonSendLimit_Click(object? sender, EventArgs e)
        {
            SendFailsafe();
            Status("Force limit sent");
        }

        private void buttonReboot_Click(object? sender, EventArgs e)
        {
            serialCommandHandle?.Invoke(new CommandPacket(CommandFlags.GUI_force_reboot));
        }

        private void buttonOpenFolder_Click(object? sender, EventArgs e)
        {
            OpenDataFolder();
        }

        private void buttonReloadSettings_Click(object? sender, EventArgs e)
        {
            ReloadSettings();
        }

        private void buttonResetCalibration_Click(object? sender, EventArgs e)
        {
            ResetCalibration();
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
