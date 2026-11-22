using DynamiX_HMI.Config;
using System.Globalization;
using System.Text;

namespace DynamiX_HMI.Analysis
{
    /// <summary>
    /// A run is a sweep: every speed of a sequence, measured one after the other. It holds all the calibrated
    /// samples, which speed each one belongs to (-1 = none: speeding up, slowing down), and what the ESP32
    /// measured before the run. Saved as one raw data file that can be re-analysed with other settings.
    /// </summary>
    public class TestRun
    {
        public string Name = "";
        public string Notes = "";
        public DateTime Date = DateTime.Now;
        public RunCalibration Calibration = new RunCalibration();
        public List<StepInfo> Steps = new List<StepInfo>();
        public List<Sample> Samples = new List<Sample>();
        public List<short> StepOf = new List<short>();   // one per sample: index in Steps, or -1
        public string FilePath = "";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Signature = "# DynamiX run";

        /// <summary>Marks the samples inside a speed's section [start, end] (s) as belonging to that speed.</summary>
        public void AssignStep(int step, double startS, double endS)
        {
            for (int i = 0; i < Samples.Count; i++)
            {
                double t = Samples[i].T;
                if (t >= startS && t <= endS)
                {
                    StepOf[i] = (short)step;
                }
            }
        }

        /// <summary>Same, for samples that arrive after the section ended: only looks at the newest ones.</summary>
        public void AssignStepFrom(int step, double startS, double endS, int fromIndex)
        {
            for (int i = Math.Max(0, fromIndex); i < Samples.Count; i++)
            {
                double t = Samples[i].T;
                if (t >= startS && t <= endS)
                {
                    StepOf[i] = (short)step;
                }
            }
        }

        public void AddSamples(IEnumerable<Sample> samples)
        {
            foreach (Sample s in samples)
            {
                Samples.Add(s);
                StepOf.Add(-1);
            }
        }

        /// <summary>Filters and cuts every speed into cycles, then summarises each speed. Speeds without data are left out.</summary>
        public List<StepResult> Analyze(AppSettings set)
        {
            var results = new List<StepResult>();
            int n = Samples.Count;
            int i = 0;
            var ranges = new Dictionary<int, (int a, int b)>();
            while (i < n)
            {
                int step = StepOf[i];
                if (step < 0)
                {
                    i++;
                    continue;
                }
                int j = i;
                while (j + 1 < n && StepOf[j + 1] == step)
                {
                    j++;
                }
                ranges[step] = ranges.TryGetValue(step, out var old) ? (Math.Min(old.a, i), j) : (i, j);
                i = j + 1;
            }

            foreach (StepInfo info in Steps)
            {
                if (!ranges.TryGetValue(info.Index, out var range) || range.b - range.a < 20)
                {
                    continue;
                }
                var cycles = new List<ProcessedCycle>();
                foreach (var (c0, c1) in Analyzer.FindCycles(Samples, range.a, range.b, set, Calibration))
                {
                    if (c1 - c0 >= 20)
                    {
                        cycles.Add(Analyzer.Process(Samples, c0, c1, set, Calibration));
                    }
                }
                if (cycles.Count > 0)
                {
                    results.Add(Analyzer.Summarize(info, cycles));
                }
            }
            return results;
        }

        // ------------------------------------------------------------------ file

        /// <summary>Writes the raw data file. Position and force are as measured (static force NOT removed). Throws on I/O errors.</summary>
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                w.WriteLine(Signature + " v1");
                w.WriteLine("# name: " + OneLine(Name));
                w.WriteLine("# date: " + Date.ToString("s", Inv));
                w.WriteLine("# notes: " + OneLine(Notes));
                w.WriteLine("# bottom_position_mm: " + Calibration.BottomMm.ToString("R", Inv));
                w.WriteLine("# top_position_mm: " + Calibration.TopMm.ToString("R", Inv));
                w.WriteLine("# bottom_force_N: " + Calibration.BottomForceN.ToString("R", Inv));
                w.WriteLine("# top_force_N: " + Calibration.TopForceN.ToString("R", Inv));
                w.WriteLine("# empty_force_N: " + Calibration.EmptyForceN.ToString("R", Inv));
                foreach (StepInfo s in Steps)
                {
                    w.WriteLine("# step: " + string.Join(", ", s.Index.ToString(Inv), s.DriveHz.ToString("R", Inv),
                        s.PlannedCycles.ToString(Inv), s.StartS.ToString("R", Inv), s.EndS.ToString("R", Inv),
                        Temp(s.TempStartC), Temp(s.TempEndC), s.TargetSpeedMmS.ToString("R", Inv)));
                }
                w.WriteLine("# position and force are as measured: + = compression, static force (preload, gas) NOT removed");
                w.WriteLine("time_s,position_mm,force_N,step");
                for (int i = 0; i < Samples.Count; i++)
                {
                    Sample s = Samples[i];
                    w.Write(s.T.ToString("0.000000", Inv));
                    w.Write(',');
                    w.Write(s.X.ToString("0.0000", Inv));
                    w.Write(',');
                    w.Write(s.F.ToString("0.000", Inv));
                    w.Write(',');
                    w.WriteLine(StepOf[i].ToString(Inv));
                }
            }
            FilePath = path;
        }

        /// <summary>Reads only the header (name, calibration, speeds): fast, for the run list.</summary>
        public static TestRun LoadHeader(string path)
        {
            return Load(path, true);
        }

        public static TestRun Load(string path)
        {
            return Load(path, false);
        }

        private static TestRun Load(string path, bool headerOnly)
        {
            var run = new TestRun { FilePath = path, Name = Path.GetFileNameWithoutExtension(path) };
            foreach (string line in File.ReadLines(path))
            {
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == '#')
                {
                    ReadHeaderLine(run, line);
                    continue;
                }
                if (!char.IsDigit(line[0]) && line[0] != '-') // the column header
                {
                    if (headerOnly)
                    {
                        break;
                    }
                    continue;
                }
                string[] c = line.Split(',');
                if (c.Length < 4)
                {
                    continue;
                }
                run.Samples.Add(new Sample(D(c[0]), D(c[1]), D(c[2])));
                run.StepOf.Add(short.Parse(c[3], Inv));
            }
            return run;
        }

        private static void ReadHeaderLine(TestRun run, string line)
        {
            int colon = line.IndexOf(':');
            if (colon < 0)
            {
                return;
            }
            string key = line.Substring(1, colon - 1).Trim();
            string val = line.Substring(colon + 1).Trim();
            switch (key)
            {
                case "name": run.Name = val; break;
                case "notes": run.Notes = val; break;
                case "date": DateTime.TryParse(val, Inv, DateTimeStyles.None, out run.Date); break;
                case "bottom_position_mm": run.Calibration.BottomMm = D(val); break;
                case "top_position_mm": run.Calibration.TopMm = D(val); break;
                case "bottom_force_N": run.Calibration.BottomForceN = D(val); break;
                case "top_force_N": run.Calibration.TopForceN = D(val); break;
                case "empty_force_N": run.Calibration.EmptyForceN = D(val); break;
                case "step":
                    string[] f = val.Split(',');
                    if (f.Length >= 7)
                    {
                        run.Steps.Add(new StepInfo
                        {
                            Index = int.Parse(f[0].Trim(), Inv),
                            DriveHz = D(f[1]),
                            PlannedCycles = int.Parse(f[2].Trim(), Inv),
                            StartS = D(f[3]),
                            EndS = D(f[4]),
                            TempStartC = f[5].Trim().Length == 0 ? double.NaN : D(f[5]),
                            TempEndC = f[6].Trim().Length == 0 ? double.NaN : D(f[6]),
                            TargetSpeedMmS = f.Length >= 8 ? D(f[7]) : 0,
                        });
                    }
                    break;
            }
        }

        /// <summary>True if the file starts like a saved run (so summaries and other CSV files are not listed).</summary>
        public static bool IsRunFile(string path)
        {
            try
            {
                using var r = new StreamReader(path);
                string? first = r.ReadLine();
                return first != null && first.StartsWith(Signature);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A file name from the date, with a number added if another run already has it.</summary>
        public static string MakeFileName(string folder, DateTime date)
        {
            string stem = Path.Combine(folder, date.ToString("yyyyMMdd_HHmmss", Inv));
            string path = stem + ".csv";
            for (int n = 2; File.Exists(path); n++)
            {
                path = stem + "_" + n + ".csv";
            }
            return path;
        }

        private static string Temp(double celsius)
        {
            return double.IsNaN(celsius) ? "" : celsius.ToString("0.00", Inv);
        }

        private static double D(string s)
        {
            return double.Parse(s.Trim(), NumberStyles.Float, Inv);
        }

        private static string OneLine(string s)
        {
            return (s ?? "").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
