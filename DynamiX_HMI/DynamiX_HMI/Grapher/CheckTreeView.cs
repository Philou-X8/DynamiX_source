using System.Drawing;
using System.Windows.Forms;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// TreeView with a fix for a Windows bug: a fast second click on a check box arrives as a double-click
    /// and would not toggle it.
    /// </summary>
    public sealed class CheckTreeView : TreeView
    {
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_LBUTTONDOWN = 0x0201;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDBLCLK)
            {
                var hit = HitTest(new Point(m.LParam.ToInt32() & 0xFFFF, (m.LParam.ToInt32() >> 16) & 0xFFFF));
                if (hit.Location == TreeViewHitTestLocations.StateImage)
                {
                    m.Msg = WM_LBUTTONDOWN;
                }
            }
            base.WndProc(ref m);
        }
    }
}
