#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace DynamiX_HMI.Grapher
{
    public class PlotSeries
    {
        public string Name;          // null = not in the legend
        public Color Color = Color.SteelBlue;
        public double[] X, Y;
        public float Width = 1.6f;
        public bool Line = true, Markers;
    }

    /// <summary>
    /// A small XY plot (lines and markers, auto-scaled axes, legend). No external dependency.
    /// Mouse: wheel zooms both axes around the cursor (Shift = X only, Shift + Ctrl = Y only), left-drag zooms to a box,
    /// right-drag pans, double-click goes back to the full view; hovering shows the nearest point's values.
    /// </summary>
    public class PlotControl : Control
    {
        public readonly List<PlotSeries> Series = new List<PlotSeries>();
        public string Title = "", XLabel = "", YLabel = "";

        // view: null = automatic (all data); otherwise the zoomed range
        double[] _view;
        // last drawn mapping, for the mouse
        RectangleF _area;
        double _x0, _x1, _y0, _y1;
        // mouse state
        Point _down; bool _boxing, _panning; Point _cur;
        double[] _panStart;
        int _hoverSeries = -1, _hoverIndex = -1;

        public bool Zoomed { get { return _view != null; } }
        public void ResetZoom() { _view = null; Invalidate(); }

        public static readonly Color[] Palette = {
            Color.FromArgb(31, 119, 180), Color.FromArgb(214, 39, 40), Color.FromArgb(44, 160, 44), Color.FromArgb(255, 127, 14),
            Color.FromArgb(148, 103, 189), Color.FromArgb(23, 162, 184), Color.FromArgb(140, 86, 75), Color.FromArgb(227, 119, 194) };

        public PlotControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.White;
        }

        // ------------------------------------------------------------------ mouse
        double ToX(float px) { return _x0 + (px - _area.Left) / _area.Width * (_x1 - _x0); }
        double ToY(float py) { return _y0 + (_area.Bottom - py) / _area.Height * (_y1 - _y0); }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); Invalidate(); }   // focus: so the wheel reaches this plot

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_area.Contains(e.Location)) return;
            double f = e.Delta > 0 ? 0.8 : 1.25;
            double cx = ToX(e.X), cy = ToY(e.Y);
            // Plain wheel zooms both axes. Ctrl must too: a touchpad pinch arrives as Ctrl + wheel.
            // Shift: horizontal axis only; Shift + Ctrl: vertical axis only.
            bool shift = (ModifierKeys & Keys.Shift) != 0, ctrl = (ModifierKeys & Keys.Control) != 0;
            bool onlyX = shift && !ctrl, onlyY = shift && ctrl;
            double nx0 = _x0, nx1 = _x1, ny0 = _y0, ny1 = _y1;
            if (!onlyY) { nx0 = cx - (cx - _x0) * f; nx1 = cx + (_x1 - cx) * f; }
            if (!onlyX) { ny0 = cy - (cy - _y0) * f; ny1 = cy + (_y1 - cy) * f; }
            _view = new[] { nx0, nx1, ny0, ny1 };
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _down = _cur = e.Location;
            if (e.Button == MouseButtons.Left && _area.Contains(e.Location)) _boxing = true;
            if (e.Button == MouseButtons.Right) { _panning = true; _panStart = new[] { _x0, _x1, _y0, _y1 }; }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_boxing && Math.Abs(e.X - _down.X) > 6 && Math.Abs(e.Y - _down.Y) > 6)
            {
                double ax = ToX(_down.X), bx = ToX(e.X), ay = ToY(_down.Y), by = ToY(e.Y);
                _view = new[] { Math.Min(ax, bx), Math.Max(ax, bx), Math.Min(ay, by), Math.Max(ay, by) };
            }
            _boxing = _panning = false;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); ResetZoom(); }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverSeries = _hoverIndex = -1; Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _cur = e.Location;
            if (_panning && _panStart != null)
            {
                double dx = (e.X - _down.X) / _area.Width * (_panStart[1] - _panStart[0]);
                double dy = (e.Y - _down.Y) / _area.Height * (_panStart[3] - _panStart[2]);
                _view = new[] { _panStart[0] - dx, _panStart[1] - dx, _panStart[2] + dy, _panStart[3] + dy };
                Invalidate();
                return;
            }
            if (_boxing) { Invalidate(); return; }
            int bs = -1, bi = -1;
            if (_area.Contains(e.Location)) FindNearest(e.X, e.Y, out bs, out bi);
            if (bs != _hoverSeries || bi != _hoverIndex) { _hoverSeries = bs; _hoverIndex = bi; Invalidate(); }
        }

        void FindNearest(float px, float py, out int bs, out int bi)
        {
            bs = bi = -1;
            double best = 15 * 15;                                   // within 15 pixels
            double kx = _area.Width / (_x1 - _x0), ky = _area.Height / (_y1 - _y0);
            for (int si = 0; si < Series.Count; si++)
            {
                PlotSeries s = Series[si];
                if (s.X == null) continue;
                for (int i = 0; i < s.X.Length; i++)
                {
                    double dx = (s.X[i] - _x0) * kx + _area.Left - px, dy = _area.Bottom - (s.Y[i] - _y0) * ky - py;
                    double d = dx * dx + dy * dy;
                    if (d < best) { best = d; bs = si; bi = i; }
                }
            }
        }

        static string Num(double v)
        {
            double a = Math.Abs(v);
            string f = a >= 1000 ? "0" : a >= 100 ? "0.0" : a >= 10 ? "0.00" : a >= 1 ? "0.000" : "0.0000";
            return v.ToString(f, CultureInfo.CurrentCulture);
        }

        static string Short(string label)
        {
            int c = label.IndexOf(',');                              // "Force (N),  + compression" -> "Force (N)"
            return c > 0 ? label.Substring(0, c) : label;
        }


        public void Clear() { Series.Clear(); Invalidate(); }

        static double NiceStep(double range, int target)
        {
            if (range <= 0 || double.IsNaN(range) || double.IsInfinity(range)) return 1;
            double raw = range / target, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), n = raw / mag;
            return (n < 1.5 ? 1 : n < 3 ? 2 : n < 7 ? 5 : 10) * mag;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var area = new RectangleF(64, 30, Width - 64 - 16, Height - 30 - 44);
            if (area.Width < 40 || area.Height < 40) return;

            double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
            foreach (PlotSeries s in Series)
            {
                if (s.X == null) continue;
                for (int i = 0; i < s.X.Length; i++)
                {
                    if (double.IsNaN(s.X[i]) || double.IsNaN(s.Y[i])) continue;
                    x0 = Math.Min(x0, s.X[i]); x1 = Math.Max(x1, s.X[i]);
                    y0 = Math.Min(y0, s.Y[i]); y1 = Math.Max(y1, s.Y[i]);
                }
            }
            if (x0 > x1) { x0 = 0; x1 = 1; y0 = 0; y1 = 1; }
            if (x1 - x0 < 1e-9) { x0 -= 0.5; x1 += 0.5; }
            if (y1 - y0 < 1e-9) { y0 -= 0.5; y1 += 0.5; }
            double px = 0.04 * (x1 - x0), py = 0.06 * (y1 - y0);
            x0 -= px; x1 += px; y0 -= py; y1 += py;
            if (_view != null && _view[1] - _view[0] > 1e-12 && _view[3] - _view[2] > 1e-12) { x0 = _view[0]; x1 = _view[1]; y0 = _view[2]; y1 = _view[3]; }
            _area = area; _x0 = x0; _x1 = x1; _y0 = y0; _y1 = y1;
            if (_hoverSeries >= Series.Count || (_hoverSeries >= 0 && (Series[_hoverSeries].X == null || _hoverIndex >= Series[_hoverSeries].X.Length)))
                _hoverSeries = _hoverIndex = -1;                     // the data changed under the mouse
            double sx = NiceStep(x1 - x0, Math.Max(3, (int)(area.Width / 90))), sy = NiceStep(y1 - y0, Math.Max(3, (int)(area.Height / 50)));

            Func<double, float> mx = v => area.Left + (float)((v - x0) / (x1 - x0)) * area.Width;
            Func<double, float> my = v => area.Bottom - (float)((v - y0) / (y1 - y0)) * area.Height;

            using (var grid = new Pen(Color.FromArgb(232, 232, 232)))
            using (var axis = new Pen(Color.FromArgb(120, 120, 120)))
            using (var zero = new Pen(Color.FromArgb(150, 150, 150), 1.2f))
            using (var small = new Font(Font.FontFamily, 8.5f))
            using (var bold = new Font(Font.FontFamily, 10f, FontStyle.Bold))
            using (var text = new SolidBrush(Color.FromArgb(60, 60, 60)))
            {
                var center = new StringFormat { Alignment = StringAlignment.Center };
                var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                for (double v = Math.Ceiling(x0 / sx) * sx; v <= x1; v += sx)
                {
                    float p = mx(v);
                    g.DrawLine(Math.Abs(v) < sx / 1000 ? zero : grid, p, area.Top, p, area.Bottom);
                    g.DrawString(Fmt(v, sx), small, text, p, area.Bottom + 4, center);
                }
                for (double v = Math.Ceiling(y0 / sy) * sy; v <= y1; v += sy)
                {
                    float p = my(v);
                    g.DrawLine(Math.Abs(v) < sy / 1000 ? zero : grid, area.Left, p, area.Right, p);
                    g.DrawString(Fmt(v, sy), small, text, area.Left - 5, p, right);
                }
                g.DrawRectangle(axis, area.Left, area.Top, area.Width, area.Height);
                g.DrawString(Title, bold, text, area.Left + area.Width / 2, 6, center);
                g.DrawString(XLabel, small, text, area.Left + area.Width / 2, Height - 20, center);
                GraphicsState st = g.Save();
                g.TranslateTransform(12, area.Top + area.Height / 2);
                g.RotateTransform(-90);
                g.DrawString(YLabel, small, text, 0, -7, center);
                g.Restore(st);

                g.SetClip(area);
                foreach (PlotSeries s in Series)
                {
                    if (s.X == null || s.X.Length == 0) continue;
                    var pts = new List<PointF>(s.X.Length);
                    for (int i = 0; i < s.X.Length; i++)
                        if (!double.IsNaN(s.X[i]) && !double.IsNaN(s.Y[i])) pts.Add(new PointF(mx(s.X[i]), my(s.Y[i])));
                    using (var pen = new Pen(s.Color, s.Width) { LineJoin = LineJoin.Round })
                    using (var br = new SolidBrush(s.Color))
                    {
                        if (s.Line && pts.Count > 1) g.DrawLines(pen, pts.ToArray());
                        if (s.Markers) foreach (PointF p in pts) g.FillEllipse(br, p.X - 4, p.Y - 4, 8, 8);
                    }
                }
                g.ResetClip();

                float ly = area.Top + 6; int shown = 0;
                foreach (PlotSeries s in Series)
                {
                    if (string.IsNullOrEmpty(s.Name) || shown++ >= 14) continue;
                    SizeF sz = g.MeasureString(s.Name, small);
                    float lx = area.Right - sz.Width - 34;
                    using (var bg = new SolidBrush(Color.FromArgb(215, 255, 255, 255))) g.FillRectangle(bg, lx - 2, ly - 1, sz.Width + 32, sz.Height + 1);
                    using (var pen = new Pen(s.Color, 2.5f)) g.DrawLine(pen, lx, ly + sz.Height / 2, lx + 20, ly + sz.Height / 2);
                    g.DrawString(s.Name, small, text, lx + 24, ly);
                    ly += sz.Height + 1;
                }

                // zoom box being dragged
                if (_boxing)
                {
                    var r = new RectangleF(Math.Min(_down.X, _cur.X), Math.Min(_down.Y, _cur.Y), Math.Abs(_cur.X - _down.X), Math.Abs(_cur.Y - _down.Y));
                    using (var fill = new SolidBrush(Color.FromArgb(40, 31, 119, 180))) g.FillRectangle(fill, r);
                    using (var pen = new Pen(Color.FromArgb(31, 119, 180)) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                }

                // hovered point
                if (_hoverSeries >= 0)
                {
                    PlotSeries s = Series[_hoverSeries];
                    double hx = s.X[_hoverIndex], hy = s.Y[_hoverIndex];
                    float qx = mx(hx), qy = my(hy);
                    if (area.Contains(qx, qy))
                    {
                        using (var pen = new Pen(s.Color, 2f)) g.DrawEllipse(pen, qx - 5, qy - 5, 10, 10);
                        string name = s.Name;
                        for (int k = _hoverSeries; name == null && k >= 0; k--) name = Series[k].Name;   // unnamed overlays share the previous name
                        string tip = (name != null ? name + "\n" : "") + Short(XLabel) + ": " + Num(hx) + "\n" + Short(YLabel) + ": " + Num(hy);
                        SizeF tsz = g.MeasureString(tip, small);
                        float tx = qx + 12, ty = qy - tsz.Height - 8;
                        if (tx + tsz.Width + 8 > area.Right) tx = qx - tsz.Width - 20;
                        if (ty < area.Top) ty = qy + 12;
                        using (var bg = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillRectangle(bg, tx, ty, tsz.Width + 8, tsz.Height + 4);
                        using (var pen = new Pen(s.Color)) g.DrawRectangle(pen, tx, ty, tsz.Width + 8, tsz.Height + 4);
                        g.DrawString(tip, small, text, tx + 4, ty + 2);
                    }
                }

                // hint, inside the plot area while the mouse is over it (or when zoomed)
                bool mouseIn = ClientRectangle.Contains(PointToClient(MousePosition));
                if (mouseIn || _view != null)
                    using (var faint = new SolidBrush(Color.FromArgb(140, 140, 140)))
                        g.DrawString(_view != null ? "zoomed: double-click for full view" : "wheel or drag: zoom   right-drag: pan   double-click: full view",
                            small, faint, area.Left + 4, area.Top + 3);
            }
        }

        static string Fmt(double v, double step)
        {
            int dec = step >= 1 ? 0 : Math.Min(6, (int)Math.Ceiling(-Math.Log10(step)));
            if (Math.Abs(v) < step / 1000) v = 0;
            return v.ToString("F" + dec, CultureInfo.CurrentCulture);
        }
    }
}
