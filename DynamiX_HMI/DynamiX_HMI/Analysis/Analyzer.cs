using DynamiX_HMI.Config;

namespace DynamiX_HMI.Analysis
{
    /// <summary>One calibrated sample: run time in s, position in mm, force in N (static force still in).</summary>
    public struct Sample
    {
        public double T;
        public double X;
        public double F;

        public Sample(double t, double x, double f)
        {
            T = t;
            X = x;
            F = f;
        }
    }

    /// <summary>
    /// What the ESP32 measured before a run (its calibration cycle, and the last TAR), in mm and N.
    /// The bottom of the stroke is the reference for the position, and the line between the bottom force
    /// and the top force is the static force (preload plus gas spring) that is removed from the graphs.
    /// </summary>
    public class RunCalibration
    {
        public double BottomMm;      // position at the bottom end of the stroke
        public double TopMm;         // position at the top end
        public double BottomForceN;  // force at rest at the bottom, shock mounted
        public double TopForceN;     // force through the top, slow speed
        public double EmptyForceN;   // TAR: force with nothing mounted (0 if there was no TAR)

        public RunCalibration Clone()
        {
            return (RunCalibration)MemberwiseClone();
        }

        public double StrokeMm => Math.Abs(TopMm - BottomMm);
        public bool Known => StrokeMm > 0.01;

        /// <summary>+1 if the position increases from the bottom to the top, -1 if it decreases (negative scale).</summary>
        public double Direction => TopMm >= BottomMm ? 1.0 : -1.0;

        public double StaticSlopeNPerMm => Known ? (TopForceN - BottomForceN) / StrokeMm : 0.0;

        /// <summary>Static (speed independent) force at xr mm above the bottom.</summary>
        public double StaticForce(double xr)
        {
            return BottomForceN + StaticSlopeNPerMm * xr;
        }
    }

    /// <summary>One speed of a run, as the ESP32 announced it.</summary>
    public class StepInfo
    {
        public int Index;
        public double TargetSpeedMmS;   // target peak speed the operator asked for
        public double DriveHz;
        public int PlannedCycles;
        public double StartS;
        public double EndS;
        public double TempStartC = double.NaN;  // NaN = no reading
        public double TempEndC = double.NaN;
    }

    /// <summary>One processed cycle, bottom dead centre to bottom dead centre.</summary>
    public class ProcessedCycle
    {
        public double[] T = Array.Empty<double>();  // s from the start of the cycle
        public double[] X = Array.Empty<double>();  // mm above the bottom
        public double[] V = Array.Empty<double>();  // mm/s, + = compression
        public double[] F = Array.Empty<double>();  // N, + = compression, static force removed if asked
        public double Period => T.Length > 0 ? T[T.Length - 1] : 0;
    }

    public class StepResult
    {
        public StepInfo Info = new StepInfo();
        public int CycleCount;
        public double CompPeakVel, CompForceAtPeakVel, CompMaxForce;
        public double RebPeakVel, RebForceAtPeakVel, RebMaxForce;     // negative values
        public List<ProcessedCycle> Cycles = new List<ProcessedCycle>();
    }

    /// <summary>Finds bottom dead centres (position minima) in a position stream.</summary>
    public class CycleDetector
    {
        private readonly double hysteresis;
        private int mode;          // 0 = waiting for a first maximum, 1 = looking for a minimum, 2 = looking for a maximum
        private double min, max;
        private int minIndex;

        public CycleDetector(double hysteresisMm)
        {
            hysteresis = hysteresisMm;
            max = double.MinValue;
        }

        /// <summary>Feed one sample. Returns the index of a bottom dead centre when one is confirmed, otherwise -1.</summary>
        public int Feed(int index, double x)
        {
            if (mode == 1)
            {
                if (x < min)
                {
                    min = x;
                    minIndex = index;
                }
                else if (x > min + hysteresis)
                {
                    mode = 2;
                    max = x;
                    return minIndex;
                }
            }
            else
            {
                if (x > max)
                {
                    max = x;
                }
                else if (x < max - hysteresis)
                {
                    mode = 1;
                    min = x;
                    minIndex = index;
                }
            }
            return -1;
        }
    }

    public static class Analyzer
    {
        private const int Margin = 150; // extra samples processed on each side so the filters are settled inside the cycle

        /// <summary>
        /// Cuts the samples [a, b] of one speed into whole cycles, bottom dead centre to bottom dead centre.
        /// The ESP32 counted the cycles already, but its section starts and ends mid-cycle: only the whole
        /// cycles inside are used. Cycles much longer or shorter than the median (a glitch) are dropped.
        /// If no whole cycle is found, the whole range is one cycle.
        /// </summary>
        public static List<(int i0, int i1)> FindCycles(IList<Sample> s, int a, int b, AppSettings set, RunCalibration? cal)
        {
            var cycles = new List<(int, int)>();
            if (b - a < 10)
            {
                return cycles;
            }
            bool known = cal != null && cal.Known;
            double dir = known ? cal!.Direction : 1.0;
            double reference = known ? cal!.BottomMm : s[a].X;

            var detector = new CycleDetector(set.CycleHysteresisMm);
            var minima = new List<int>();
            for (int i = a; i <= b; i++)
            {
                int m = detector.Feed(i, dir * (s[i].X - reference));
                if (m >= 0)
                {
                    minima.Add(m);
                }
            }

            for (int k = 0; k + 1 < minima.Count; k++)
            {
                cycles.Add((minima[k], minima[k + 1]));
            }

            if (cycles.Count >= 3)
            {
                var lengths = cycles.Select(c => s[c.Item2].T - s[c.Item1].T).OrderBy(x => x).ToList();
                double median = lengths[lengths.Count / 2];
                cycles = cycles.Where(c =>
                {
                    double length = s[c.Item2].T - s[c.Item1].T;
                    return length > 0.6 * median && length < 1.6 * median;
                }).ToList();
            }

            if (cycles.Count == 0)
            {
                cycles.Add((a, b));
            }
            return cycles;
        }

        /// <summary>
        /// Filters, differentiates and (when the calibration is known) removes the static force for the samples [i0, i1].
        /// Position is mm above the bottom, speed is + for compression, force is + for compression.
        /// </summary>
        public static ProcessedCycle Process(IList<Sample> s, int i0, int i1, AppSettings set, RunCalibration? cal)
        {
            int a = Math.Max(0, i0 - Margin);
            int b = Math.Min(s.Count - 1, i1 + Margin);
            int n = b - a + 1;
            var t = new double[n];
            var x = new double[n];
            var f = new double[n];
            for (int i = 0; i < n; i++)
            {
                Sample p = s[a + i];
                t[i] = p.T;
                x[i] = p.X;
                f[i] = p.F;
            }

            double fs = (n - 1) / Math.Max(1e-9, t[n - 1] - t[0]);
            x = Signal.LowPassZeroPhase(x, set.FilterCutoffHz, fs);
            f = Signal.LowPassZeroPhase(f, set.FilterCutoffHz, fs);

            if (set.PositionDelayMs != 0)
            {
                var shifted = new double[n];
                int hint = 0;
                for (int i = 0; i < n; i++)
                {
                    shifted[i] = Signal.Interp(t, x, t[i] + set.PositionDelayMs / 1000.0, ref hint);
                }
                x = shifted;
            }

            int halfWindow = Math.Max(2, (int)Math.Round(set.VelocityWindowMs / 1000.0 * fs / 2.0));
            double[] v = Signal.Derivative(t, x, halfWindow);

            int m = i1 - i0 + 1;
            int o = i0 - a;
            bool known = cal != null && cal.Known;
            double dir = known ? cal!.Direction : 1.0;
            double bottom;
            if (known)
            {
                bottom = cal!.BottomMm;
            }
            else
            {
                bottom = double.MaxValue;
                for (int i = 0; i < m; i++)
                {
                    bottom = Math.Min(bottom, x[o + i]);
                }
            }

            double speedSign = set.TopIsCompression ? 1.0 : -1.0;
            double tare = !set.RemoveStaticForce && cal != null ? cal.EmptyForceN : 0.0;
            var c = new ProcessedCycle { T = new double[m], X = new double[m], V = new double[m], F = new double[m] };
            for (int i = 0; i < m; i++)
            {
                c.T[i] = t[o + i] - t[o];
                c.X[i] = dir * (x[o + i] - bottom);
                c.V[i] = speedSign * dir * v[o + i];
                double staticForce = set.RemoveStaticForce && known ? cal!.StaticForce(c.X[i]) : 0.0;
                c.F[i] = f[o + i] - staticForce - tare;
            }
            return c;
        }

        /// <summary>
        /// Average of several cycles of the same speed: each is resampled at the same fractions of its period
        /// (0 = bottom dead centre at the start, 1 = at the end), then position, speed and force are averaged point by point.
        /// </summary>
        public static ProcessedCycle AverageCycles(IList<ProcessedCycle> cycles, int points = 500)
        {
            if (cycles.Count == 1)
            {
                return cycles[0];
            }
            var a = new ProcessedCycle { T = new double[points], X = new double[points], V = new double[points], F = new double[points] };
            double period = 0;
            foreach (ProcessedCycle c in cycles)
            {
                double p = c.Period;
                period += p / cycles.Count;
                int j = 0;
                for (int i = 0; i < points; i++)
                {
                    double t = p * i / (points - 1);
                    while (j < c.T.Length - 2 && c.T[j + 1] < t)
                    {
                        j++;
                    }
                    double span = c.T[j + 1] - c.T[j];
                    double u = span > 0 ? Math.Max(0, Math.Min(1, (t - c.T[j]) / span)) : 0;
                    a.X[i] += (c.X[j] + u * (c.X[j + 1] - c.X[j])) / cycles.Count;
                    a.V[i] += (c.V[j] + u * (c.V[j + 1] - c.V[j])) / cycles.Count;
                    a.F[i] += (c.F[j] + u * (c.F[j + 1] - c.F[j])) / cycles.Count;
                }
            }
            for (int i = 0; i < points; i++)
            {
                a.T[i] = period * i / (points - 1);
            }
            return a;
        }

        /// <summary>Peak values of a speed, averaged over its measured cycles.</summary>
        public static StepResult Summarize(StepInfo info, List<ProcessedCycle> cycles)
        {
            var r = new StepResult { Info = info, CycleCount = cycles.Count, Cycles = cycles };
            foreach (ProcessedCycle c in cycles)
            {
                double vmax = double.MinValue, vmin = double.MaxValue, fmax = double.MinValue, fmin = double.MaxValue;
                for (int i = 0; i < c.V.Length; i++)
                {
                    vmax = Math.Max(vmax, c.V[i]);
                    vmin = Math.Min(vmin, c.V[i]);
                    fmax = Math.Max(fmax, c.F[i]);
                    fmin = Math.Min(fmin, c.F[i]);
                }
                double sumComp = 0, sumReb = 0;
                int countComp = 0, countReb = 0;
                for (int i = 0; i < c.V.Length; i++) // force averaged where the speed is within 2 % of its peak
                {
                    if (c.V[i] >= 0.98 * vmax)
                    {
                        sumComp += c.F[i];
                        countComp++;
                    }
                    if (c.V[i] <= 0.98 * vmin)
                    {
                        sumReb += c.F[i];
                        countReb++;
                    }
                }
                r.CompPeakVel += vmax;
                r.RebPeakVel += vmin;
                r.CompMaxForce += fmax;
                r.RebMaxForce += fmin;
                r.CompForceAtPeakVel += sumComp / Math.Max(1, countComp);
                r.RebForceAtPeakVel += sumReb / Math.Max(1, countReb);
            }
            int k = Math.Max(1, cycles.Count);
            r.CompPeakVel /= k;
            r.RebPeakVel /= k;
            r.CompMaxForce /= k;
            r.RebMaxForce /= k;
            r.CompForceAtPeakVel /= k;
            r.RebForceAtPeakVel /= k;
            return r;
        }
    }
}
