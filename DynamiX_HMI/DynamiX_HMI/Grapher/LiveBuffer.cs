namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// The last few seconds of force and position for the Live tab. Times are PC clock seconds: the points of a
    /// run are anchored so that the newest one lands on the moment its packet arrived. Everything runs on the UI thread.
    /// </summary>
    public class LiveBuffer
    {
        public const double WindowS = 10.0;     // what the Live tab shows
        public const double StatsWindowS = 1.0; // what the noise figures are computed over

        private readonly List<double> t = new List<double>();
        private readonly List<double> f = new List<double>();
        private readonly List<double> x = new List<double>();

        public int Count => t.Count;
        public bool Dirty { get; set; }

        public void Add(double time, double forceN, double positionMm)
        {
            t.Add(time);
            f.Add(forceN);
            x.Add(positionMm);
            Dirty = true;
        }

        /// <summary> Forgets what is older than the window, counted back from the newest sample. </summary>
        public void Trim()
        {
            if (t.Count == 0)
            {
                return;
            }
            double oldest = t[t.Count - 1] - WindowS;
            int cut = 0;
            while (cut < t.Count && t[cut] < oldest)
            {
                cut++;
            }
            if (cut > 0)
            {
                t.RemoveRange(0, cut);
                f.RemoveRange(0, cut);
                x.RemoveRange(0, cut);
            }
        }

        public void Clear()
        {
            t.Clear();
            f.Clear();
            x.Clear();
            Dirty = true;
        }

        /// <summary> Copies of the data, time counted back from the newest sample (so 0 is now, -10 is ten seconds ago). </summary>
        public void Snapshot(out double[] time, out double[] force, out double[] position)
        {
            double now = t.Count > 0 ? t[t.Count - 1] : 0;
            time = new double[t.Count];
            for (int i = 0; i < t.Count; i++)
            {
                time[i] = t[i] - now;
            }
            force = f.ToArray();
            position = x.ToArray();
        }

        /// <summary> Mean and standard deviation of force and position over the last StatsWindowS seconds. </summary>
        public bool TryGetStats(out double forceMean, out double forceSigma, out double positionMean, out double positionSigma, out int count)
        {
            forceMean = forceSigma = positionMean = positionSigma = 0;
            count = 0;
            if (t.Count < 5)
            {
                return false;
            }
            double from = t[t.Count - 1] - StatsWindowS;
            int first = t.Count - 1;
            while (first > 0 && t[first - 1] >= from)
            {
                first--;
            }
            count = t.Count - first;
            if (count < 5)
            {
                return false;
            }
            Stats(f, first, out forceMean, out forceSigma);
            Stats(x, first, out positionMean, out positionSigma);
            return true;
        }

        private static void Stats(List<double> values, int first, out double mean, out double sigma)
        {
            int n = values.Count - first;
            double sum = 0;
            for (int i = first; i < values.Count; i++)
            {
                sum += values[i];
            }
            mean = sum / n;
            double squares = 0;
            for (int i = first; i < values.Count; i++)
            {
                squares += (values[i] - mean) * (values[i] - mean);
            }
            sigma = Math.Sqrt(squares / n);
        }
    }
}
