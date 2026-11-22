namespace CSharpProto1.Menus
{
    /// <summary>
    /// Straight line through two calibration points: raw ADC counts (X) to a value with units (Y).
    /// Immutable: a new calibration replaces the old one as a whole.
    /// </summary>
    public sealed class LinearCalibration
    {
        /// <summary> Y = X: the raw counts, unchanged. </summary>
        public static readonly LinearCalibration Identity = new LinearCalibration(1.0, 0.0);

        public double Slope { get; }   // units per count
        public double Offset { get; }  // units at 0 counts

        private LinearCalibration(double slope, double offset)
        {
            Slope = slope;
            Offset = offset;
        }

        /// <summary> False if the two points don't define a usable line (same X, same Y, not a number). </summary>
        public static bool TryFromPoints(double x1, double y1, double x2, double y2, out LinearCalibration calibration)
        {
            calibration = Identity;
            if (x1 == x2)
            {
                return false;
            }
            double slope = (y2 - y1) / (x2 - x1);
            double offset = y1 - slope * x1;
            if (slope == 0 || !double.IsFinite(slope) || !double.IsFinite(offset))
            {
                return false;
            }
            calibration = new LinearCalibration(slope, offset);
            return true;
        }

        public float ToUnits(int raw)
        {
            return (float)(Slope * raw + Offset);
        }

        public double ToRaw(double units)
        {
            return (units - Offset) / Slope;
        }
    }
}
