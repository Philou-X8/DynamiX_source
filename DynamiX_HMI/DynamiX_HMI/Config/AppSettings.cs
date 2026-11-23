using System.ComponentModel;
using System.Globalization;
using System.Xml.Serialization;

namespace DynamiX_HMI.Config
{
    public enum UnitSystem { SI, Imperial }

    /// <summary>
    /// Every user setting, saved as an XML file (settings.xml in %AppData%\DynamiX). The Options page shows
    /// this class in a PropertyGrid, so the attributes (category, description) are the page's text.
    /// Internally everything is N, mm and mm/s: the units only change what is displayed and exported.
    /// </summary>
    public class AppSettings
    {
        // ------------------------------------------------------------------ 1. General
        [Category("1. General"), DisplayName("Units"), Description("SI: N, mm, mm/s.  Imperial: lbf, in, in/s. Only changes what is displayed and exported.")]
        public UnitSystem Units { get; set; } = UnitSystem.SI;

        [Category("1. General"), DisplayName("Data folder"), Description("Where the sensor calibration history and the runs are kept: runs are saved in its Runs sub-folder.")]
        public string DataFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DynamiX");

        [Category("1. General"), DisplayName("Save runs automatically"), Description("Every finished (or interrupted) run is written to the runs folder as a raw data file, so it shows up in the run list next time.")]
        public bool AutoSaveRuns { get; set; } = true;

        [Category("1. General"), DisplayName("Live view refresh (ms)"), Description("How often the live graphs are redrawn (50 to 1000). Raise it on a slow PC.")]
        public int ScreenRefreshMs { get; set; } = 200;

        // ------------------------------------------------------------------ 2. Machine
        [Category("2. Machine"), DisplayName("Bottom is at high position"), Description("Which end of the position sensor's range is the bottom of the stroke. Must match BOTTOM_AT_HIGH_POSITION in the firmware's config.h (false there as shipped).")]
        public bool BottomAtHighPosition { get; set; } = false;

        [Category("2. Machine"), DisplayName("Top of stroke is compression"), Description("True: moving towards the top compresses the shock, so a positive speed is compression. False: the opposite.")]
        public bool TopIsCompression { get; set; } = true;

        [Category("2. Machine"), DisplayName("Dead centre hysteresis (mm)"), Description("The position must reverse by this much for a dead centre to count when the cycles are cut out of a speed's data.")]
        public double CycleHysteresisMm { get; set; } = 2.0;

        // ------------------------------------------------------------------ 3. Analysis
        [Category("3. Analysis"), DisplayName("Filter cut-off (Hz)"), Description("Zero-phase low-pass filter (no time shift) applied to force and position before the speed is computed. 0 = no filtering. Lower smooths more but hides fast detail; keep it well above the highest feature you want to see. Ignored if above 0.45 x the sample rate (about 650 samples per second).")]
        public double FilterCutoffHz { get; set; } = 150;

        [Category("3. Analysis"), DisplayName("Speed window (ms)"), Description("The speed is the slope of a line fitted through the position over this window. Wider = smoother speed, rounder peaks.")]
        public double VelocityWindowMs { get; set; } = 8;

        [Category("3. Analysis"), DisplayName("Position delay (ms)"), Description("Fixed delay of the position sensor relative to the load cell. The position is advanced by this much. Use it to close a small loop in the force/speed curve.")]
        public double PositionDelayMs { get; set; } = 0;

        [Category("3. Analysis"), DisplayName("Absolute speed"), Description("True: compression and rebound are both drawn at positive speed (force keeps its sign: compression above zero, rebound below). False: signed speed, positive = compression.")]
        public bool AbsoluteSpeed { get; set; } = true;

        [Category("3. Analysis"), DisplayName("Remove static force"), Description("True: the static force (preload at the bottom plus the gas spring, from the run's calibration) is subtracted, so the graphs show the damping force only. False: the force as measured, less the TAR empty load.")]
        public bool RemoveStaticForce { get; set; } = true;

        [Category("3. Analysis"), DisplayName("Average the cycles"), Description("True: the Force/Speed graph shows the average of a speed's cycles. False: every cycle is drawn, which shows how repeatable a detail is.")]
        public bool AverageCycles { get; set; } = true;

        // ------------------------------------------------------------------ 4. Safety
        [Category("4. Safety"), DisplayName("Force limit (N)"), Description("The motor stops when the force is further than this from the load cell's absolute zero. Counted from the hardware calibration below (never from the TAR), and sent to the ESP32 at every connection and change.")]
        public double ForceLimitN { get; set; } = 3000;

        // ------------------------------------------------------------------ 5. Sensor calibration
        [Category("5. Sensor calibration"), DisplayName("Convert to units"), Description("Debug switch. Unchecked: the raw ADC counts go through unchanged (the graphs then show counts). The force limit always uses the calibration.")]
        public bool ConvertUnits { get; set; } = true;

        // Defaults: measured on the machine (the other team's calibration window). The firmware negates the
        // force (INVERT_FORCE), so the force slope is positive here and the zero is negated.
        [Category("5. Sensor calibration"), DisplayName("Force (N per count)"), Description("Newtons per ADC count. Positive when compression reads positive (the firmware inverts the load cell). Set it with Set gain in the tools.")]
        public double ForceNPerCount { get; set; } = 0.0050718;

        [Category("5. Sensor calibration"), DisplayName("Force zero (counts)"), Description("ADC counts at zero force: nothing on the load cell. Set it with Zero force in the tools.")]
        public double ForceZeroCounts { get; set; } = -18469;

        [Category("5. Sensor calibration"), DisplayName("Position (mm per count)"), Description("Millimetres per ADC count of the position sensor.")]
        public double PositionMmPerCount { get; set; } = 0.000056267;

        [Category("5. Sensor calibration"), DisplayName("Position zero (counts)"), Description("ADC counts at position 0 mm. Only shifts the displayed position: the analysis measures from the bottom of the stroke.")]
        public double PositionZeroCounts { get; set; } = 767909;

        [Category("5. Sensor calibration"), DisplayName("Force zero set"), Description("When and how the force zero was last set."), ReadOnly(true)]
        public string ForceZeroInfo { get; set; } = "default";

        [Category("5. Sensor calibration"), DisplayName("Force gain set"), Description("When and how the force gain was last set."), ReadOnly(true)]
        public string ForceGainInfo { get; set; } = "default";

        [Category("5. Sensor calibration"), DisplayName("Position set"), Description("When and how the position calibration was last set."), ReadOnly(true)]
        public string PositionInfo { get; set; } = "default";

        // ------------------------------------------------------------------ 6. Display
        [Category("6. Display"), DisplayName("Shift graph colours"), Description("Each speed of a run is drawn with the hue of the run's colour moved down by this much more than the speed before it (0 to 255 steps around the colour wheel, 255 = a full turn). 0 = every speed has the run's colour. A speed whose colour was picked by hand keeps it.")]
        public int GraphColourHueShift { get; set; } = 16;

        // ------------------------------------------------------------------ conversions (not saved)

        /// <summary>The settings in use. Loaded at start-up, changed by the Options page.</summary>
        private static string startupError = "";

        [XmlIgnore, Browsable(false)]
        public static AppSettings Current { get; set; } = LoadAtStartup();

        /// <summary>Why the settings file could not be read at start-up ("" if it could).</summary>
        [XmlIgnore, Browsable(false)]
        public static string StartupError => startupError;

        private static AppSettings LoadAtStartup()
        {
            AppSettings loaded = Load(DefaultPath, out string error);
            startupError = error;
            return loaded;
        }

        /// <summary>Force in N from raw counts (the counts unchanged when conversion is off).</summary>
        public double ForceN(double counts)
        {
            return ConvertUnits ? (counts - ForceZeroCounts) * ForceNPerCount : counts;
        }

        public double PositionMm(double counts)
        {
            return ConvertUnits ? (counts - PositionZeroCounts) * PositionMmPerCount : counts;
        }

        /// <summary>Raw counts for a force in N. Always the calibration, whatever Convert to units says.</summary>
        public double ForceCounts(double newtons)
        {
            return ForceZeroCounts + newtons / ForceNPerCount;
        }

        /// <summary>Force window for the ESP32 in raw counts: +/- ForceLimitN around the absolute zero.</summary>
        public void ForceLimitCounts(out int lo, out int hi)
        {
            double a = ForceCounts(-ForceLimitN), b = ForceCounts(ForceLimitN);
            lo = ClampInt24(Math.Min(a, b)); // a negative slope swaps the ends
            hi = ClampInt24(Math.Max(a, b));
        }

        private static int ClampInt24(double value)
        {
            return (int)Math.Round(Math.Clamp(value, -8388608.0, 8388607.0));
        }

        [XmlIgnore, Browsable(false)]
        public string RunsFolder => Path.Combine(DataFolder, "Runs");

        [XmlIgnore, Browsable(false)]
        public string CalibrationHistoryPath => Path.Combine(DataFolder, "sensor_calibration_history.csv");

        /// <summary> Puts every value back in its allowed range. Called after each edit and each load. </summary>
        public void Sanitize()
        {
            ScreenRefreshMs = Math.Clamp(ScreenRefreshMs, 50, 1000);
            GraphColourHueShift = Math.Clamp(GraphColourHueShift, 0, 255);
            CycleHysteresisMm = Math.Max(0.01, Fix(CycleHysteresisMm, 2.0));
            FilterCutoffHz = Math.Max(0, Fix(FilterCutoffHz, 150));
            VelocityWindowMs = Math.Max(1, Fix(VelocityWindowMs, 8));
            PositionDelayMs = Math.Clamp(Fix(PositionDelayMs, 0), -50, 50);
            ForceLimitN = Math.Max(1, Fix(ForceLimitN, 3000));
            if (!double.IsFinite(ForceNPerCount) || ForceNPerCount == 0) ForceNPerCount = 0.0050718;
            if (!double.IsFinite(PositionMmPerCount) || PositionMmPerCount == 0) PositionMmPerCount = 0.000056267;
            ForceZeroCounts = Fix(ForceZeroCounts, -18469);
            PositionZeroCounts = Fix(PositionZeroCounts, 767909);
            if (string.IsNullOrWhiteSpace(DataFolder))
            {
                DataFolder = new AppSettings().DataFolder;
            }
        }

        private static double Fix(double value, double fallback)
        {
            return double.IsFinite(value) ? value : fallback;
        }

        // ------------------------------------------------------------------ file

        public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamiX", "settings.xml");

        /// <summary> Reads the file; missing or unreadable: the defaults (error says why, "" when fine). </summary>
        public static AppSettings Load(string path, out string error)
        {
            error = "";
            if (File.Exists(path))
            {
                try
                {
                    using FileStream stream = File.OpenRead(path);
                    AppSettings? loaded = new XmlSerializer(typeof(AppSettings)).Deserialize(stream) as AppSettings;
                    if (loaded != null)
                    {
                        loaded.Sanitize();
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    error = "Settings file unreadable (" + ex.Message + "): defaults used";
                }
            }
            return new AppSettings();
        }

        /// <summary> Writes a temporary file first, so a crash can't leave half a file. Throws on I/O errors. </summary>
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            using (FileStream stream = File.Create(temp))
            {
                new XmlSerializer(typeof(AppSettings)).Serialize(stream, this);
            }
            File.Move(temp, path, true);
        }

        /// <summary> Appends the sensor calibration to sensor_calibration_history.csv, one line per change. </summary>
        public void AppendCalibrationHistory(string what)
        {
            string path = CalibrationHistoryPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bool header = !File.Exists(path);
            CultureInfo inv = CultureInfo.InvariantCulture;
            using StreamWriter w = new StreamWriter(path, true);
            if (header)
            {
                w.WriteLine("date,change,ForceNPerCount,ForceZeroCounts,PositionMmPerCount,PositionZeroCounts");
            }
            w.WriteLine(string.Join(",", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv),
                "\"" + what.Replace("\"", "'") + "\"",
                ForceNPerCount.ToString("R", inv), ForceZeroCounts.ToString("R", inv),
                PositionMmPerCount.ToString("R", inv), PositionZeroCounts.ToString("R", inv)));
        }
    }

    /// <summary>Unit conversion for display and export.</summary>
    public static class Units
    {
        public const double NPerLbf = 4.4482216, MmPerIn = 25.4;
        public static double Force(double n, UnitSystem u) => u == UnitSystem.SI ? n : n / NPerLbf;
        public static double Length(double mm, UnitSystem u) => u == UnitSystem.SI ? mm : mm / MmPerIn;
        public static string ForceUnit(UnitSystem u) => u == UnitSystem.SI ? "N" : "lbf";
        public static string LengthUnit(UnitSystem u) => u == UnitSystem.SI ? "mm" : "in";
        public static string SpeedUnit(UnitSystem u) => u == UnitSystem.SI ? "mm/s" : "in/s";
    }
}
