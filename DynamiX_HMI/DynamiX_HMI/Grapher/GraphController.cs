using DynamiX_HMI.Analysis;
using DynamiX_HMI.Config;
using DynamiX_HMI.Serial;
using System.Diagnostics;
using System.Text;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// Owns the data of the graphs. Raw points come in, the ESP32's separators cut them into speeds, and each
    /// run (a sweep of speeds) is kept in the RunPanel list, analysed, and saved as a raw data file.
    /// It drives the two views: the GraphPanel (what is drawn) and the RunPanel (which runs and speeds).
    /// Everything runs on the UI thread.
    /// </summary>
    internal class GraphController
    {
        private const int MaxRunSamples = 3_000_000;   // safety cap, about 80 minutes at full rate
        private const int DebugPoints = 16;            // debug tab: how many of the latest points are listed

        private record PendingSection(int Index, double Start, double End, double TempStart, double TempEnd);

        private readonly RunPanel runPanel;
        private readonly GraphPanel graphPanel;
        private readonly Action<string> say;
        private readonly LiveBuffer liveBuffer = new LiveBuffer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double lastPointArrivalS = double.NegativeInfinity;

        // the run being measured
        private RunEntry? current;
        private List<(double targetMmS, double hz, int cycles)> plan = new List<(double, double, int)>();
        private readonly Queue<PendingSection> pending = new Queue<PendingSection>();
        private int sectionsSeen;
        private double boundaryTempC = double.NaN;

        /// <summary> The active project's folder ("" = no project: nothing can be measured). </summary>
        public string ProjectFolder { get; private set; } = "";
        public bool HasProject => ProjectFolder.Length > 0;

        public GraphController(RunPanel runs, GraphPanel graph, Action<string> message)
        {
            runPanel = runs;
            graphPanel = graph;
            say = message;
            graphPanel.SetLiveBuffer(liveBuffer);

            runPanel.SelectionChanged = RefreshGraphs;
            runPanel.ExportRequested = () => ExportSummary(runPanel.CheckedRuns().ToList());
            runPanel.ExportRunRequested = run => ExportSummary(new List<RunEntry> { run });
            runPanel.SavePointsRequested = SavePoints;
            runPanel.ImportRequested = ImportFiles;
            runPanel.DeleteRequested = DeleteRun;
        }

        /// <summary> True between BeginRun() and FinishRun(): points are arriving for a run that isn't closed yet. </summary>
        public bool RunInProgress => current != null;

        // ------------------------------------------------------------------ live data

        /// <summary> ESP_live_data: the 5 Hz readings. Only drawn when no full rate points are coming (idle). </summary>
        public void AddLiveData(int rawPosition, int rawForce)
        {
            double now = clock.Elapsed.TotalSeconds;
            if (now - lastPointArrivalS > 1.0)
            {
                AppSettings set = AppSettings.Current;
                liveBuffer.Add(now, set.ForceN(rawForce), set.PositionMm(rawPosition));
            }
        }

        // ------------------------------------------------------------------ a run

        /// <summary>
        /// Start of a run: a new entry at the top of the run list. 'sequencePlan' is what was sent to the ESP32 (one
        /// entry per speed, in order), 'calibration' what it measured before the run, 'tempC' the damper temperature
        /// (0 = none). 'name' is the name typed for the run ("" = the date and time); a name already used in the
        /// project gets " 1", " 2"... added.
        /// </summary>
        public void BeginRun(List<(double targetMmS, double hz, int cycles)> sequencePlan, RunCalibration calibration, double tempC, string name)
        {
            if (current != null)
            {
                FinishRun(); // never happens in the normal flow: be safe
            }
            plan = sequencePlan;
            pending.Clear();
            sectionsSeen = 0;
            boundaryTempC = ToTemp(tempC);

            DateTime now = DateTime.Now;
            name = UniqueName(name.Length > 0 ? name : TestRun.DefaultName(now), null);
            var run = new TestRun { Name = name, Date = now, Calibration = calibration };
            current = new RunEntry(run) { Loaded = true, InProgress = true };
            runPanel.AddEntry(current, true);
        }

        /// <summary>
        /// The name, or the name with " n" added (n = 1, 2...) if another run of the list or a file of the project
        /// folder already has it. 'except' is a run that doesn't count (the one being named).
        /// </summary>
        private string UniqueName(string wanted, RunEntry? except)
        {
            bool Taken(string n) =>
                runPanel.Entries.Any(r => r != except && string.Equals(r.Display, n, StringComparison.OrdinalIgnoreCase))
                || File.Exists(TestRun.FilePathFor(ProjectFolder, n));
            if (!Taken(wanted))
            {
                return wanted;
            }
            for (int i = 1; ; i++)
            {
                string candidate = wanted + " " + i;
                if (!Taken(candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary> Raw points from the ESP32: converted with the sensor calibration, drawn live, and kept for the run. </summary>
        public void AddRawPoints(List<DataPointRaw> rawPoints)
        {
            if (rawPoints.Count == 0)
            {
                return;
            }
            AppSettings set = AppSettings.Current;
            var samples = new List<Sample>(rawPoints.Count);
            foreach (DataPointRaw p in rawPoints)
            {
                samples.Add(new Sample(p.time * 1e-6, set.PositionMm(p.position), set.ForceN(p.force)));
            }

            // live view: the newest point lands on the arrival time of the packet
            double now = clock.Elapsed.TotalSeconds;
            double newest = samples[samples.Count - 1].T;
            foreach (Sample s in samples)
            {
                liveBuffer.Add(now - (newest - s.T), s.F, s.X);
            }
            lastPointArrivalS = now;
            graphPanel.SetDebugText(DebugText(samples));

            if (current != null && current.Run.Samples.Count < MaxRunSamples)
            {
                current.Run.AddSamples(samples);
                EmitReadySections(false);
            }
        }

        /// <summary>
        /// ESP_sequence_separator: payload is the start and the end timestamp (4 bytes each, big-endian, microseconds
        /// like the raw points) of a finished speed. 'tempC' is the damper temperature now (0 = none).
        /// Returns false if the payload is too short.
        /// </summary>
        public bool TryAddSequenceSeparator(CommandPacket command, double tempC)
        {
            if (command.payload.Length < 8)
            {
                return false;
            }
            byte[] b = Encoding.Latin1.GetBytes(command.payload);
            double start = ((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]) * 1e-6;
            double end = ((b[4] << 24) | (b[5] << 16) | (b[6] << 8) | b[7]) * 1e-6;
            if (current == null)
            {
                return true; // nothing to attach it to
            }

            double endTemp = ToTemp(tempC);
            pending.Enqueue(new PendingSection(sectionsSeen, start, end, boundaryTempC, endTemp));
            sectionsSeen++;
            boundaryTempC = endTemp; // the end of this speed is the start of the next
            EmitReadySections(false);
            return true;
        }

        /// <summary>
        /// Cuts out every announced speed whose points have arrived. The separator travels on a faster path than the
        /// data and can show up before the last points of its section, so a section waits until a point at or after
        /// its end is stored. force: take what is there regardless (end of the run).
        /// </summary>
        private void EmitReadySections(bool force)
        {
            if (current == null)
            {
                return;
            }
            bool added = false;
            while (pending.Count > 0)
            {
                PendingSection s = pending.Peek();
                List<Sample> samples = current.Run.Samples;
                bool dataArrived = samples.Count > 0 && samples[samples.Count - 1].T >= s.End;
                if (!dataArrived && !force)
                {
                    break; // later sections end later, so none of them can be ready either
                }
                pending.Dequeue();

                (double targetMmS, double hz, int cycles) = s.Index < plan.Count ? plan[s.Index] : (0.0, 0.0, 0);
                var info = new StepInfo
                {
                    Index = s.Index, TargetSpeedMmS = targetMmS, DriveHz = hz, PlannedCycles = cycles,
                    StartS = s.Start, EndS = s.End, TempStartC = s.TempStart, TempEndC = s.TempEnd,
                };
                current.Run.Steps.Add(info);
                current.Run.AssignStep(s.Index, s.Start, s.End);
                current.Steps.Add(new StepEntry(current, info));
                added = true;
            }

            if (added)
            {
                Reanalyze(current);
                runPanel.UpdateEntry(current);
                RefreshGraphs();
            }
        }

        /// <summary> End of a run (or the link dropped): analyse, save the raw data, and close the entry. </summary>
        public void FinishRun()
        {
            if (current == null)
            {
                return;
            }
            RunEntry entry = current;
            EmitReadySections(true);
            current = null;
            entry.InProgress = false;

            if (entry.Run.Samples.Count == 0)
            {
                runPanel.RemoveEntry(entry);
                return;
            }

            Reanalyze(entry);
            if (AppSettings.Current.AutoSaveRuns)
            {
                try
                {
                    if (File.Exists(TestRun.FilePathFor(ProjectFolder, entry.Display))) // taken by a file added meanwhile
                    {
                        entry.Display = entry.Run.Name = UniqueName(entry.Display, entry);
                    }
                    string path = TestRun.FilePathFor(ProjectFolder, entry.Display);
                    entry.Run.Save(path);
                    entry.Path = path;
                    say("Run saved: " + path);
                }
                catch (Exception ex)
                {
                    say("Could not save the run: " + ex.Message);
                }
            }
            runPanel.UpdateEntry(entry);
            RefreshGraphs();
        }

        // ------------------------------------------------------------------ analysis and drawing

        /// <summary> Analyses a run with the current settings and hands each speed its result. </summary>
        private void Reanalyze(RunEntry entry)
        {
            try
            {
                entry.Results = entry.Run.Analyze(AppSettings.Current);
            }
            catch (Exception ex)
            {
                entry.Results = new List<StepResult>();
                say("Analysis failed for " + entry.Display + ": " + ex.Message);
            }
            foreach (StepEntry step in entry.Steps)
            {
                step.Result = entry.Results.FirstOrDefault(r => r.Info.Index == step.Info.Index);
            }
        }

        /// <summary> Reads the samples of a run listed from its header only, and analyses it. </summary>
        private void EnsureLoaded(RunEntry entry)
        {
            if (entry.Loaded)
            {
                return;
            }
            entry.Loaded = true; // even when it fails: no endless retries
            try
            {
                TestRun full = TestRun.Load(entry.Path);
                entry.Run = full;
                Reanalyze(entry);
                runPanel.RefreshTexts();
            }
            catch (Exception ex)
            {
                say("Could not read " + Path.GetFileName(entry.Path) + ": " + ex.Message);
            }
        }

        /// <summary> Redraws the graphs from the ticked runs. </summary>
        public void RefreshGraphs()
        {
            var shown = runPanel.CheckedRuns().ToList();
            foreach (RunEntry r in shown)
            {
                EnsureLoaded(r);
            }
            graphPanel.ShowRuns(shown);
        }

        /// <summary> The settings changed (filter, units, calibration...): analyse every loaded run again. </summary>
        public void SettingsChanged()
        {
            foreach (RunEntry r in runPanel.Entries)
            {
                if (r.Loaded)
                {
                    Reanalyze(r);
                }
            }
            runPanel.RefreshTexts();
            runPanel.AssignColours(); // the shift of the graph colours may have changed
            graphPanel.ApplySettings();
            RefreshGraphs();
        }

        // ------------------------------------------------------------------ run list and files

        private RunEntry? ReadHeader(string path)
        {
            if (!TestRun.IsRunFile(path))
            {
                return null; // summaries, calibration history, other CSV files
            }
            try
            {
                TestRun header = TestRun.LoadHeader(path);
                if (string.IsNullOrWhiteSpace(header.Name))
                {
                    header.Name = Path.GetFileNameWithoutExtension(path);
                }
                var entry = new RunEntry(header);
                foreach (StepInfo info in header.Steps)
                {
                    entry.Steps.Add(new StepEntry(entry, info));
                }
                return entry;
            }
            catch (Exception ex)
            {
                say("Could not read " + Path.GetFileName(path) + ": " + ex.Message);
                return null;
            }
        }

        /// <summary> True if a run of the list is not in a file (it would be lost if the list is emptied). </summary>
        public bool HasUnsavedRuns => runPanel.Entries.Any(r => r.InProgress || (r.Path.Length == 0 && r.Run.Samples.Count > 0));

        /// <summary>
        /// Makes a folder the active project: the list is emptied and every run file of the folder is listed
        /// (header only, none shown). False if the folder can't be read: no project is active then.
        /// </summary>
        public bool LoadProject(string folder)
        {
            runPanel.Clear();
            ProjectFolder = "";
            var found = new List<RunEntry>();
            try
            {
                foreach (string path in Directory.EnumerateFiles(folder, "*.csv"))
                {
                    RunEntry? entry = ReadHeader(path);
                    if (entry != null)
                    {
                        found.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                say("Could not read the project folder: " + ex.Message);
                return false;
            }
            ProjectFolder = folder;
            foreach (RunEntry entry in found.OrderByDescending(e => e.Run.Date)) // newest first
            {
                runPanel.AddEntry(entry, false, false);
            }
            say("Project " + Path.GetFileName(folder.TrimEnd('\\', '/')) + ": " + found.Count + (found.Count == 1 ? " run" : " runs"));
            return true;
        }

        /// <summary> The Import button: each run file is copied into the project folder and listed (shown). </summary>
        private void ImportFiles(string[] paths)
        {
            if (!HasProject)
            {
                say("DENIED: select a project first");
                return;
            }
            string projectDir = Path.GetFullPath(ProjectFolder).TrimEnd('\\', '/');
            foreach (string source in paths)
            {
                string name = Path.GetFileName(source);
                string? sourceDir = Path.GetDirectoryName(Path.GetFullPath(source))?.TrimEnd('\\', '/');
                if (string.Equals(sourceDir, projectDir, StringComparison.OrdinalIgnoreCase))
                {
                    say(name + " is already in the project");
                    continue;
                }
                RunEntry? header = ReadHeader(source);
                if (header == null)
                {
                    say(name + " is not a DynamiX run file");
                    continue;
                }

                try
                {
                    string runName = header.Display;
                    string unique = UniqueName(runName, null);
                    string destination = Path.Combine(ProjectFolder, name);
                    if (unique == runName && !File.Exists(destination))
                    {
                        File.Copy(source, destination);
                    }
                    else
                    {
                        // the name (or the file name) is taken: the copy is a run of its own, with " n" added to both
                        TestRun run = TestRun.Load(source);
                        run.Name = unique;
                        destination = TestRun.FilePathFor(ProjectFolder, unique);
                        run.Save(destination);
                    }
                    RunEntry? added = ReadHeader(destination);
                    if (added != null)
                    {
                        runPanel.AddEntry(added, true);
                        say("Imported " + added.Display);
                    }
                }
                catch (Exception ex)
                {
                    say("Could not import " + name + ": " + ex.Message);
                }
            }
        }

        private void DeleteRun(RunEntry entry)
        {
            try
            {
                File.Delete(entry.Path);
                runPanel.RemoveEntry(entry);
            }
            catch (Exception ex)
            {
                say("Could not delete the file: " + ex.Message);
            }
        }

        /// <summary>
        /// The summary table (peak speeds and forces of the shown speeds) of the given runs, as .xlsx or .csv:
        /// one block per run, in one file.
        /// </summary>
        private void ExportSummary(List<RunEntry> shown)
        {
            foreach (RunEntry r in shown)
            {
                EnsureLoaded(r);
            }
            var tests = shown
                .Select(r => new KeyValuePair<string, List<StepResult>>(
                    r.Display, r.Steps.Where(s => s.Visible && s.Result != null).Select(s => s.Result!).ToList()))
                .Where(t => t.Value.Count > 0)
                .ToList();
            if (tests.Count == 0)
            {
                say("Nothing to export: tick a run that has measured speeds");
                return;
            }

            string folder = ProjectFolder;
            using var dialog = new SaveFileDialog
            {
                Filter = "Excel workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
                FileName = (tests.Count == 1 ? Sanitize(tests[0].Key) : "DynamiX") + "_summary.xlsx",
                InitialDirectory = folder,
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            try
            {
                SummaryExport.Write(dialog.FileName, tests, AppSettings.Current.Units);
                say("Exported " + Path.GetFileName(dialog.FileName));
            }
            catch (Exception ex)
            {
                say("Export failed: " + ex.Message);
            }
        }

        /// <summary> Save Points: a copy of the run's raw data file wherever the user chooses. </summary>
        private void SavePoints(RunEntry entry)
        {
            EnsureLoaded(entry);
            if (entry.Run.Samples.Count == 0)
            {
                say("This run has no points to save");
                return;
            }
            string folder = Directory.Exists(ProjectFolder) ? ProjectFolder : "";
            using var dialog = new SaveFileDialog
            {
                Filter = "DynamiX run (*.csv)|*.csv",
                FileName = entry.Path.Length > 0 ? Path.GetFileName(entry.Path) : Sanitize(entry.Display) + ".csv",
                InitialDirectory = folder,
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            try
            {
                string known = entry.Run.FilePath;
                entry.Run.Save(dialog.FileName);
                entry.Run.FilePath = known;
                if (entry.Path.Length == 0) // a run that was not saved before now has a file
                {
                    entry.Path = dialog.FileName;
                    entry.Run.FilePath = dialog.FileName;
                    runPanel.UpdateEntry(entry);
                }
                say("Points saved: " + dialog.FileName);
            }
            catch (Exception ex)
            {
                say("Could not save the points: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static string Sanitize(string name)
        {
            return string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
        }

        /// <summary> The ESP32 sends 0 for "no valid reading". </summary>
        private static double ToTemp(double celsius)
        {
            return celsius == 0 ? double.NaN : celsius;
        }

        private static string DebugText(List<Sample> points)
        {
            var sb = new StringBuilder();
            int first = Math.Max(0, points.Count - DebugPoints);
            for (int i = first; i < points.Count; i++)
            {
                sb.Append("Point ").Append(i - first)
                  .Append(": t=").Append(points[i].T.ToString("0.000000"))
                  .Append(" , f=").Append(points[i].F.ToString("0.##"))
                  .Append(" , p=").Append(points[i].X.ToString("0.##"))
                  .Append('\n');
            }
            return sb.ToString();
        }
    }
}
