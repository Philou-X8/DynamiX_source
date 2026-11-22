using CSharpProto1.Serial;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSharpProto1.Grapher
{
    /// <summary>
    /// Passive view: it only draws what the GraphController gives it
    /// (SetRecentPoints for the live views, SetRuns for the finished runs).
    /// </summary>
    public partial class GraphPanel : UserControl
    {
        private const int RawWindowPoints = 128; // raw view: this many of the latest points (twice the old 64)
        private const int DebugPoints = 16;      // debug tab: how many of the latest points are listed
        private const int PlotMargin = 6;            // pixels kept free around the curves

        private List<DataPointUnit> recentPoints = new List<DataPointUnit>();
        private List<RunCurve> runCurves = new List<RunCurve>();

        // ranges of the runs drawn in the Force/Pos and Force/Spd views, set by SetRuns()
        private bool hasRange;
        private float minPos, maxPos, minForce, maxForce, minSpd, maxSpd;

        public GraphPanel()
        {
            InitializeComponent();

            // Draw in Paint handlers on double-buffered panels, so the graphs don't flicker
            // and survive the window being covered or resized.
            EnableDoubleBuffer(panelGraphRaw);
            EnableDoubleBuffer(panelGraphForcePos);
            EnableDoubleBuffer(panelGraphForceSpd);
            panelGraphRaw.Paint += PaintRaw;
            panelGraphForcePos.Paint += PaintForcePos;
            panelGraphForceSpd.Paint += PaintForceSpd;
        }

        private static void EnableDoubleBuffer(Control control)
        {
            // Control.DoubleBuffered is protected, so go through reflection
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(control, true);
        }

        /// <summary>
        /// Latest points received (oldest first). Refreshes the raw view and the debug tab.
        /// The list is read, not copied: it must not change until the next call.
        /// </summary>
        public void SetRecentPoints(List<DataPointUnit> points)
        {
            recentPoints = points;

            // same text as label2 of the master form: the last few points in plain text
            StringBuilder sb = new StringBuilder();
            int first = Math.Max(0, points.Count - DebugPoints);
            for (int i = first; i < points.Count; i++)
            {
                sb.Append("Point ").Append(i - first)
                  .Append(": t=").Append(points[i].time.ToString("0.000000"))
                  .Append(" , f=").Append(Show(points[i].force))
                  .Append(" , p=").Append(Show(points[i].position))
                  .Append('\n');
            }
            labelDataDebug.Text = sb.ToString();

            // raw view labels: min and max of the points currently drawn (the latest window)
            int n = Math.Min(points.Count, RawWindowPoints);
            if (n == 0)
            {
                label1.Text = "Force min and max: ? , ?";
                label2.Text = "Position min and max: ? , ?";
            }
            else
            {
                int start = points.Count - n;
                float fMin = points[start].force, fMax = fMin;
                float pMin = points[start].position, pMax = pMin;
                for (int i = start + 1; i < points.Count; i++)
                {
                    fMin = Math.Min(fMin, points[i].force);
                    fMax = Math.Max(fMax, points[i].force);
                    pMin = Math.Min(pMin, points[i].position);
                    pMax = Math.Max(pMax, points[i].position);
                }
                label1.Text = "Force min and max: " + Show(fMin) + " , " + Show(fMax);
                label2.Text = "Position min and max: " + Show(pMin) + " , " + Show(pMax);
            }

            panelGraphRaw.Invalidate();
        }

        /// <summary> Runs to draw in the Force/Pos and Force/Spd views, scaled to fit all of them. </summary>
        public void SetRuns(List<RunCurve> curves)
        {
            runCurves = curves;

            hasRange = false;
            foreach (RunCurve curve in curves)
            {
                for (int i = 0; i < curve.Points.Count; i++)
                {
                    DataPointUnit p = curve.Points[i];
                    float v = curve.Velocities[i];
                    if (!hasRange)
                    {
                        minPos = maxPos = p.position;
                        minForce = maxForce = p.force;
                        minSpd = maxSpd = v;
                        hasRange = true;
                        continue;
                    }
                    minPos = Math.Min(minPos, p.position);
                    maxPos = Math.Max(maxPos, p.position);
                    minForce = Math.Min(minForce, p.force);
                    maxForce = Math.Max(maxForce, p.force);
                    minSpd = Math.Min(minSpd, v);
                    maxSpd = Math.Max(maxSpd, v);
                }
            }

            labelRangeX.Text = hasRange
                ? "Position min and max: " + Show(minPos) + " , " + Show(maxPos)
                : "Position min and max: ? , ?";
            labelRangeY.Text = hasRange
                ? "Force min and max: " + Show(minForce) + " , " + Show(maxForce)
                : "Force min and max: ? , ?";
            labelSpdRangeX.Text = hasRange
                ? "Speed min and max: " + Show(minSpd) + " , " + Show(maxSpd)
                : "Speed min and max: ? , ?";
            labelSpdRangeY.Text = labelRangeY.Text;

            panelGraphForcePos.Invalidate();
            panelGraphForceSpd.Invalidate();
        }

        //
        // Raw view: force and position over time (index of the point = time at a steady sample rate)
        //
        private void PaintRaw(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);

            int n = Math.Min(recentPoints.Count, RawWindowPoints);
            if (n < 2)
            {
                return;
            }
            int first = recentPoints.Count - n;
            int width = panelGraphRaw.ClientSize.Width;
            int height = panelGraphRaw.ClientSize.Height;

            // The window always spans the full width; the newest point is at the right edge,
            // so while the buffer fills up the curve slides in from the right.
            float stepX = (width - 1) / (float)(RawWindowPoints - 1);
            float offsetX = (RawWindowPoints - n) * stepX;

            // each curve is scaled to its own min and max
            using Pen penForce = new Pen(Color.Red, 2);
            using Pen penPosition = new Pen(Color.Blue, 2);
            DrawRawCurve(g, penForce, first, n, p => p.force, stepX, offsetX, height);
            DrawRawCurve(g, penPosition, first, n, p => p.position, stepX, offsetX, height);
        }

        private void DrawRawCurve(Graphics g, Pen pen, int first, int n, Func<DataPointUnit, float> value,
                                  float stepX, float offsetX, int height)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float v = value(recentPoints[first + i]);
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }

            PointF[] pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                pts[i] = new PointF(offsetX + i * stepX,
                                    ToPixel(value(recentPoints[first + i]), min, max, height, flip: true));
            }
            g.DrawLines(pen, pts);
        }

        //
        // Force/Pos view: force (Y) over position (X), both scaled to the observed min and max
        //
        private void PaintForcePos(object? sender, PaintEventArgs e)
        {
            PaintForceOver(e.Graphics, panelGraphForcePos, (curve, i) => curve.Points[i].position, minPos, maxPos,
                           zeroLine: false);
        }

        //
        // Force/Spd view: force (Y) over velocity (X), same scaling. A vertical line marks
        // zero speed (one side is compression, the other rebound).
        //
        private void PaintForceSpd(object? sender, PaintEventArgs e)
        {
            PaintForceOver(e.Graphics, panelGraphForceSpd, (curve, i) => curve.Velocities[i], minSpd, maxSpd,
                           zeroLine: true);
        }

        /// <summary>
        /// Every visible run: force (Y) over xValue (X), scaled to [minX, maxX] and the force range.
        /// zeroLine: a light vertical line at X = 0 (under the curves), when 0 is in the range.
        /// </summary>
        private void PaintForceOver(Graphics g, Panel panel, Func<RunCurve, int, float> xValue, float minX, float maxX,
                                    bool zeroLine)
        {
            g.Clear(Color.White);

            if (!hasRange)
            {
                return;
            }
            int width = panel.ClientSize.Width;
            int height = panel.ClientSize.Height;

            if (zeroLine && minX < 0 && maxX > 0)
            {
                float x = ToPixel(0f, minX, maxX, width, flip: false);
                g.DrawLine(Pens.LightGray, x, 0, x, height);
            }

            foreach (RunCurve curve in runCurves)
            {
                if (curve.Points.Count < 2)
                {
                    continue;
                }
                PointF[] pts = new PointF[curve.Points.Count];
                for (int i = 0; i < pts.Length; i++)
                {
                    pts[i] = new PointF(ToPixel(xValue(curve, i), minX, maxX, width, flip: false),
                                        ToPixel(curve.Points[i].force, minForce, maxForce, height, flip: true));
                }
                using Pen pen = new Pen(curve.Color, 2);
                g.DrawLines(pen, pts);
            }
        }

        /// <summary> A value for the labels: mm and N to 2 decimals, raw counts (conversion off) unchanged. </summary>
        private static string Show(float value)
        {
            return value.ToString("0.##");
        }

        /// <summary>
        /// Maps value from [min, max] to [PlotMargin, size - PlotMargin] pixels.
        /// flip is for Y, where pixel 0 is the top and larger values must go up.
        /// A flat curve (min == max) is drawn in the middle.
        /// </summary>
        private static float ToPixel(float value, float min, float max, int size, bool flip)
        {
            float usable = size - 1 - 2 * PlotMargin;
            float ratio = max == min ? 0.5f : (value - min) / (max - min);
            if (flip)
            {
                ratio = 1f - ratio;
            }
            return PlotMargin + ratio * usable;
        }
    }
}
