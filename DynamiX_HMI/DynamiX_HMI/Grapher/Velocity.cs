using System;
using System.Collections.Generic;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// Shaft velocity from the position samples, for the Force/Speed graph.
    ///
    /// Centered difference: v[i] = (p[i+k] - p[i-k]) / (t[i+k] - t[i-k]).
    /// The slope is taken symmetrically around sample i, so the velocity belongs to the same
    /// instant as force[i]: no interpolation of the force is needed, and there is no half-sample
    /// lag (a forward or backward difference would shift the velocity by half a sample, which
    /// opens a fake loop in the force/velocity curve). It is also more accurate than either
    /// one-sided difference (error in dt² instead of dt).
    ///
    /// Differentiating amplifies the position noise; a wider span (larger k) averages it out,
    /// at the cost of rounding the sharpest velocity peaks. At 10 Hz with ~1.5 ms between
    /// samples, k = 2 (a 6 ms span) lowers the peak velocity by less than 1 %.
    /// </summary>
    public static class Velocity
    {
        public const int HalfSpan = 2; // k, in samples. TUNE: raise it if the curve is too noisy.

        /// <summary>
        /// One velocity per point, in position units per second (mm/s, or counts/s when the
        /// conversion is off). At both ends the span shrinks to the points that exist.
        /// </summary>
        public static List<float> Compute(List<DataPointUnit> points, int halfSpan = HalfSpan)
        {
            int n = points.Count;
            List<float> velocities = new List<float>(n);
            for (int i = 0; i < n; i++)
            {
                int before = Math.Max(0, i - halfSpan);
                int after = Math.Min(n - 1, i + halfSpan);
                float dt = points[after].time - points[before].time;
                velocities.Add(dt > 0 ? (points[after].position - points[before].position) / dt : 0f);
            }
            return velocities;
        }
    }
}
