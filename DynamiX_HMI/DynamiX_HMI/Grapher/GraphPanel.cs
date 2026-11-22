using DynamiX_HMI.Analysis;
using DynamiX_HMI.Config;
using System.Drawing;
using System.Windows.Forms;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// The graph area, a passive view (the GraphController gives it what to draw):
    ///   Live        force and position of the last 10 s, with noise figures
    ///   Force/Pos   force over position, every measured cycle of the shown speeds
    ///   Force/Speed force over speed (the average cycle of each shown speed)
    ///   Peak        force at peak speed against peak speed, one line per run, and the table behind it
    ///   Time        the whole first shown run against time: force, position and speed
    ///   Debug       the last points as text
    /// Every plot zooms with the wheel or a box, pans with the right button, and shows the nearest point on hover.
    /// </summary>
    public partial class GraphPanel : UserControl
    {
        private const int MaxTimePoints = 6000;

        private LiveBuffer? live;

        public GraphPanel()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;

            ApplySettings();
            liveTimer.Start();
            ShowRuns(Array.Empty<RunEntry>());
        }

        private void liveTimer_Tick(object? sender, EventArgs e)
        {
            RefreshLive();
        }

        /// <summary> The live graphs read this buffer on every refresh. </summary>
        public void SetLiveBuffer(LiveBuffer buffer)
        {
            live = buffer;
        }

        /// <summary> Refresh period and units changed. </summary>
        public void ApplySettings()
        {
            liveTimer.Interval = AppSettings.Current.ScreenRefreshMs;
            UnitSystem u = AppSettings.Current.Units;
            plotLiveF.Title = "Force";
            plotLiveF.XLabel = "Time (s)";
            plotLiveF.YLabel = "Force (" + Units.ForceUnit(u) + ")";
            plotLiveX.Title = "Position";
            plotLiveX.XLabel = "Time (s)";
            plotLiveX.YLabel = "Position (" + Units.LengthUnit(u) + ")";
            RefreshLive(true);
        }

        public void SetDebugText(string text)
        {
            labelDebug.Text = text;
        }

        // ------------------------------------------------------------------ Live tab

        private void RefreshLive(bool force = false)
        {
            if (live == null || (!live.Dirty && !force) || tabs.SelectedTab != pageLive)
            {
                return;
            }
            live.Dirty = false;
            live.Trim();
            UnitSystem u = AppSettings.Current.Units;
            live.Snapshot(out double[] t, out double[] f, out double[] x);
            plotLiveF.Series.Clear();
            plotLiveX.Series.Clear();
            if (t.Length > 1)
            {
                plotLiveF.Series.Add(new PlotSeries { X = t, Y = f.Select(v => Units.Force(v, u)).ToArray(), Color = PlotControl.Palette[1], Width = 1f });
                plotLiveX.Series.Add(new PlotSeries { X = t, Y = x.Select(v => Units.Length(v, u)).ToArray(), Color = PlotControl.Palette[0], Width = 1f });
            }
            plotLiveF.Invalidate();
            plotLiveX.Invalidate();

            if (live.TryGetStats(out double fm, out double fs, out double pm, out double ps, out int n))
            {
                labelStats.Text = string.Format("Last {0:0.#} s ({1} samples):   force {2:0.00} ± {3:0.00} {4} (std)    position {5:0.000} ± {6:0.0000} {7} (std)",
                    LiveBuffer.StatsWindowS, n, Units.Force(fm, u), Units.Force(fs, u), Units.ForceUnit(u),
                    Units.Length(pm, u), Units.Length(ps, u), Units.LengthUnit(u));
            }
            else
            {
                labelStats.Text = "Waiting for data...";
            }
        }

        // ------------------------------------------------------------------ analysed runs

        /// <summary> Draws the runs that are ticked in the run list (their visible speeds). Results must be up to date. </summary>
        public void ShowRuns(IReadOnlyList<RunEntry> runs)
        {
            AppSettings set = AppSettings.Current;
            UnitSystem u = set.Units;
            bool abs = set.AbsoluteSpeed;
            string fu = Units.ForceUnit(u), lu = Units.LengthUnit(u), su = Units.SpeedUnit(u);

            plotFx.Series.Clear();
            plotFv.Series.Clear();
            plotPeak.Series.Clear();
            plotTimeF.Series.Clear();
            plotTimeX.Series.Clear();
            plotTimeV.Series.Clear();
            table.Rows.Clear();

            plotFx.Title = set.RemoveStaticForce ? "Force vs position, every cycle (static force removed)" : "Force vs position, every cycle (as measured)";
            plotFx.XLabel = "Position above bottom (" + lu + ")";
            plotFx.YLabel = "Force (" + fu + "), + compression";
            plotFv.Title = set.AverageCycles ? "Force vs speed, average cycle" : "Force vs speed, every cycle";
            plotFv.XLabel = (abs ? "Speed magnitude (" : "Speed (") + su + ")" + (abs ? "" : ", + compression");
            plotFv.YLabel = "Force (" + fu + "), + compression";
            plotPeak.Title = "Force at peak speed";
            plotPeak.XLabel = "Peak speed (" + su + ")";
            plotPeak.YLabel = "Force at peak speed (" + fu + ")";
            plotTimeF.Title = "Whole run";
            plotTimeF.XLabel = "Time (s)";
            plotTimeF.YLabel = "Force (" + fu + "), as measured";
            plotTimeX.XLabel = "Time (s)";
            plotTimeX.YLabel = "Position above bottom (" + lu + ")";
            plotTimeV.XLabel = "Time (s)";
            plotTimeV.YLabel = "Speed (" + su + "), + compression";

            Func<double, double> speed = v => Units.Length(abs ? Math.Abs(v) : v, u);
            bool single = runs.Count == 1;

            foreach (RunEntry run in runs)
            {
                Color runColor = run.Shown;
                var steps = run.Steps.Where(s => s.Visible && s.Result != null).OrderBy(s => s.Info.DriveHz).ToList();
                foreach (StepEntry step in steps)
                {
                    StepResult r = step.Result!;
                    string stepName = RunPanel.StepName(step.Info, u);
                    string label = single ? stepName : run.Display + "  " + stepName;
                    bool first = true;
                    foreach (ProcessedCycle c in r.Cycles)
                    {
                        plotFx.Series.Add(new PlotSeries
                        {
                            X = c.X.Select(v => Units.Length(v, u)).ToArray(),
                            Y = c.F.Select(v => Units.Force(v, u)).ToArray(),
                            Color = step.Shown, Name = first ? label : null, Width = 1.2f,
                        });
                        first = false;
                    }
                    if (set.AverageCycles)
                    {
                        ProcessedCycle avg = Analyzer.AverageCycles(r.Cycles);
                        plotFv.Series.Add(new PlotSeries
                        {
                            X = avg.V.Select(speed).ToArray(), Y = avg.F.Select(v => Units.Force(v, u)).ToArray(),
                            Color = step.Shown, Name = label,
                        });
                    }
                    else
                    {
                        first = true;
                        foreach (ProcessedCycle c in r.Cycles)
                        {
                            plotFv.Series.Add(new PlotSeries
                            {
                                X = c.V.Select(speed).ToArray(), Y = c.F.Select(v => Units.Force(v, u)).ToArray(),
                                Color = step.Shown, Name = first ? label : null, Width = 1.2f,
                            });
                            first = false;
                        }
                    }

                    table.Rows.Add(run.Display, stepName, step.Info.DriveHz.ToString("0.0"), r.CycleCount,
                        speed(r.CompPeakVel).ToString("0.#"), Units.Force(r.CompForceAtPeakVel, u).ToString("0.#"),
                        Units.Length(Math.Abs(r.RebPeakVel), u).ToString("0.#"), Units.Force(r.RebForceAtPeakVel, u).ToString("0.#"),
                        double.IsNaN(step.Info.TempStartC) ? "" : step.Info.TempStartC.ToString("0.0"));
                }
                AddPeakSeries(run, steps, runColor, u, abs);
            }

            if (runs.Count > 0)
            {
                ShowWholeRun(runs.FirstOrDefault(r => r.Loaded) ?? runs[0], set);
            }

            foreach (PlotControl p in new[] { plotFx, plotFv, plotPeak, plotTimeF, plotTimeX, plotTimeV })
            {
                p.Invalidate();
            }
        }

        private void AddPeakSeries(RunEntry run, List<StepEntry> steps, Color color, UnitSystem u, bool abs)
        {
            if (steps.Count == 0)
            {
                return;
            }
            Func<double, double> speed = v => Units.Length(abs ? Math.Abs(v) : v, u);
            if (abs)
            {
                // two branches from the origin: compression above zero force, rebound below, both at positive speed
                var cx = new List<double> { 0 }; var cy = new List<double> { 0 };
                var rx = new List<double> { 0 }; var ry = new List<double> { 0 };
                foreach (StepEntry s in steps)
                {
                    StepResult r = s.Result!;
                    cx.Add(speed(r.CompPeakVel)); cy.Add(Units.Force(r.CompForceAtPeakVel, u));
                    rx.Add(speed(r.RebPeakVel)); ry.Add(Units.Force(r.RebForceAtPeakVel, u));
                }
                plotPeak.Series.Add(new PlotSeries { X = cx.ToArray(), Y = cy.ToArray(), Color = color, Markers = true, Name = run.Display, Width = 2f });
                plotPeak.Series.Add(new PlotSeries { X = rx.ToArray(), Y = ry.ToArray(), Color = color, Markers = true, Width = 2f });
            }
            else
            {
                var px = new List<double>(); var py = new List<double>();
                foreach (StepEntry s in steps.OrderByDescending(q => q.Info.DriveHz))
                {
                    px.Add(speed(s.Result!.RebPeakVel)); py.Add(Units.Force(s.Result.RebForceAtPeakVel, u));
                }
                px.Add(0); py.Add(0);
                foreach (StepEntry s in steps)
                {
                    px.Add(speed(s.Result!.CompPeakVel)); py.Add(Units.Force(s.Result.CompForceAtPeakVel, u));
                }
                plotPeak.Series.Add(new PlotSeries { X = px.ToArray(), Y = py.ToArray(), Color = color, Markers = true, Name = run.Display, Width = 2f });
            }
        }

        /// <summary> Force, position and speed of the whole run against time, as measured. </summary>
        private void ShowWholeRun(RunEntry entry, AppSettings set)
        {
            TestRun run = entry.Run;
            int n = run.Samples.Count;
            if (n < 10)
            {
                return;
            }
            UnitSystem u = set.Units;
            RunCalibration cal = run.Calibration;
            bool known = cal.Known;
            double dir = known ? cal.Direction : 1.0;
            double bottom = known ? cal.BottomMm : run.Samples.Min(s => s.X);

            var t = new double[n];
            var x = new double[n];
            for (int i = 0; i < n; i++)
            {
                t[i] = run.Samples[i].T;
                x[i] = run.Samples[i].X;
            }
            double fs = (n - 1) / Math.Max(1e-9, t[n - 1] - t[0]);
            double[] xf = Signal.LowPassZeroPhase(x, set.FilterCutoffHz, fs);
            int halfWindow = Math.Max(2, (int)Math.Round(set.VelocityWindowMs / 1000.0 * fs / 2.0));
            double[] v = Signal.Derivative(t, xf, halfWindow);
            double sign = (set.TopIsCompression ? 1.0 : -1.0) * dir;

            int stride = Math.Max(1, n / MaxTimePoints);
            int m = (n + stride - 1) / stride;
            var tt = new double[m]; var ff = new double[m]; var xx = new double[m]; var vv = new double[m];
            for (int i = 0; i < m; i++)
            {
                int k = i * stride;
                tt[i] = t[k];
                ff[i] = Units.Force(run.Samples[k].F, u);
                xx[i] = Units.Length(dir * (x[k] - bottom), u);
                vv[i] = Units.Length(sign * v[k], u);
            }
            plotTimeF.Series.Add(new PlotSeries { X = tt, Y = ff, Color = PlotControl.Palette[1], Width = 1f });
            plotTimeX.Series.Add(new PlotSeries { X = tt, Y = xx, Color = PlotControl.Palette[0], Width = 1f });
            plotTimeV.Series.Add(new PlotSeries { X = tt, Y = vv, Color = PlotControl.Palette[2], Width = 1f });
            plotTimeF.Title = "Whole run: " + entry.Display;
        }
    }
}
