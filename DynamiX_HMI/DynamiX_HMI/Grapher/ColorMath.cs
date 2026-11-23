using System.Drawing;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// Colour maths in HSV (hue, saturation, value). The hue is also given on a 0 to 255 scale (255 steps for the
    /// 360 degrees of the colour wheel), the scale the Display setting "Shift graph colours" uses.
    /// </summary>
    public static class ColorMath
    {
        public const double HueSteps = 255.0;

        /// <summary> Hue in degrees (0 to 360), saturation and value (0 to 1). </summary>
        public static void ToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            if (delta <= 0)
            {
                hue = 0;
            }
            else if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * ((b - r) / delta + 2);
            }
            else
            {
                hue = 60 * ((r - g) / delta + 4);
            }
            if (hue < 0)
            {
                hue += 360;
            }
            saturation = max <= 0 ? 0 : delta / max;
            value = max;
        }

        public static Color FromHsv(double hue, double saturation, double value, int alpha = 255)
        {
            hue = ((hue % 360) + 360) % 360;
            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - c;
            double r, g, b;
            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(alpha, (int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }

        /// <summary> The hue of a colour on the 0 to 255 scale. </summary>
        public static double Hue255(Color color)
        {
            ToHsv(color, out double hue, out _, out _);
            return hue * HueSteps / 360.0;
        }

        /// <summary>
        /// The colour with its hue moved by 'steps' on the 0 to 255 scale (negative = down, wrapping around);
        /// saturation and value stay. Greys have no hue and stay as they are.
        /// </summary>
        public static Color ShiftHue255(Color color, double steps)
        {
            if (steps == 0)
            {
                return color;
            }
            ToHsv(color, out double hue, out double saturation, out double value);
            return FromHsv(hue + steps * 360.0 / HueSteps, saturation, value, color.A);
        }
    }
}
