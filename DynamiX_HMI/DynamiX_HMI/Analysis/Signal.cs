using System;

namespace DynamiX_HMI.Analysis
{
    public static class Signal
    {
        /// <summary>Zero-phase low-pass: 2nd-order Butterworth run forward then backward (4th order overall, no time shift).</summary>
        public static double[] LowPassZeroPhase(double[] x, double cutoffHz, double sampleHz)
        {
            int n = x.Length;
            if (n < 4 || cutoffHz <= 0 || cutoffHz >= 0.45 * sampleHz) return (double[])x.Clone();
            double k = Math.Tan(Math.PI * cutoffHz / sampleHz);
            double norm = 1 / (1 + Math.Sqrt(2) * k + k * k);
            double b0 = k * k * norm, b1 = 2 * b0, b2 = b0;
            double a1 = 2 * (k * k - 1) * norm, a2 = (1 - Math.Sqrt(2) * k + k * k) * norm;

            // Pad both ends with an odd reflection so the filter starts and ends settled.
            int pad = Math.Min(n - 1, (int)Math.Ceiling(3 * sampleHz / cutoffHz));
            var y = new double[n + 2 * pad];
            for (int i = 0; i < pad; i++)
            {
                y[i] = 2 * x[0] - x[pad - i];
                y[pad + n + i] = 2 * x[n - 1] - x[n - 2 - i];
            }
            Array.Copy(x, 0, y, pad, n);
            for (int pass = 0; pass < 2; pass++)
            {
                double c = y[0], z1 = 0, z2 = 0;
                for (int i = 0; i < y.Length; i++)
                {
                    double v = y[i] - c;                 // remove the starting level: no start-up transient
                    double o = b0 * v + z1;
                    z1 = b1 * v - a1 * o + z2;
                    z2 = b2 * v - a2 * o;
                    y[i] = o + c;
                }
                Array.Reverse(y);
            }
            var r = new double[n];
            Array.Copy(y, pad, r, 0, n);
            return r;
        }

        /// <summary>Derivative dy/dt by a least-squares line fit over +/- halfWindow samples (uses the real timestamps).</summary>
        public static double[] Derivative(double[] t, double[] y, int halfWindow)
        {
            int n = y.Length;
            var d = new double[n];
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - halfWindow), b = Math.Min(n - 1, i + halfWindow);
                double st = 0, sy = 0;
                int m = b - a + 1;
                for (int j = a; j <= b; j++) { st += t[j]; sy += y[j]; }
                double mt = st / m, my = sy / m, num = 0, den = 0;
                for (int j = a; j <= b; j++) { double dt = t[j] - mt; num += dt * (y[j] - my); den += dt * dt; }
                d[i] = den > 0 ? num / den : 0;
            }
            return d;
        }

        /// <summary>Linear interpolation of y(t) at time tq; clamps outside the data.</summary>
        public static double Interp(double[] t, double[] y, double tq, ref int hint)
        {
            int n = t.Length;
            if (tq <= t[0]) return y[0];
            if (tq >= t[n - 1]) return y[n - 1];
            int i = Math.Max(0, Math.Min(hint, n - 2));
            while (i > 0 && t[i] > tq) i--;
            while (i < n - 2 && t[i + 1] < tq) i++;
            hint = i;
            double span = t[i + 1] - t[i];
            return span > 0 ? y[i] + (y[i + 1] - y[i]) * (tq - t[i]) / span : y[i];
        }
    }
}
