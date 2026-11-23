using DynamiX_HMI.Config;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// One run of the run list: a group box with the run's name, the buttons that act on it, and a small tree
    /// of its speeds. The root node ("Show Speeds") shows or hides the whole run; under it each speed has a
    /// visibility box and a colour square (click it to pick the colour). The control resizes itself to fit its tree.
    /// </summary>
    public partial class RunItem : UserControl
    {
        private const int GroupMinWidth = 253;   // wide enough for the three buttons
        private const int TreeLeft = 6;
        private const int TreeTop = 51;
        private const int Pad = 6;
        private const int SwatchSize = 14;        // the colour square; the image is wider (see RunItem.Designer.cs):
                                                  // the rest is empty space between the check box and the square

        private RunEntry? entry;
        private TreeNode? rootNode;
        private TreeNode? menuNode;               // the node a right click opened the menu on
        private readonly Dictionary<int, int> swatchIndex = new Dictionary<int, int>();
        private bool suppress;

        /// <summary> The ticks changed: the graphs must be redrawn. </summary>
        public Action? SelectionChanged;
        /// <summary> A colour was picked: the swatches and the graphs must follow. </summary>
        public Action? ColoursChanged;
        public Action<RunEntry>? SaveSummaryRequested;
        public Action<RunEntry>? SavePointsRequested;
        public Action<RunEntry>? RemoveRequested;
        /// <summary> Delete the run's file (already confirmed). </summary>
        public Action<RunEntry>? DeleteRequested;

        public RunItem()
        {
            InitializeComponent();
        }

        public RunEntry Entry => entry ?? throw new InvalidOperationException("RunItem is not bound to a run");

        /// <summary> True if the run is ticked: its visible speeds are drawn. </summary>
        public bool IsShown => rootNode != null && rootNode.Checked;

        // ------------------------------------------------------------------ content

        /// <summary> Shows a run. </summary>
        public void Bind(RunEntry run, bool shown)
        {
            entry = run;
            Rebuild(shown);
        }

        /// <summary> The run's speeds or state changed: builds the tree again, keeping the run ticked or not. </summary>
        public void Rebuild()
        {
            Rebuild(IsShown);
        }

        private void Rebuild(bool shown)
        {
            if (entry == null)
            {
                return;
            }
            bool expanded = rootNode?.IsExpanded ?? false; // a new run starts collapsed
            UnitSystem u = AppSettings.Current.Units;

            suppress = true;
            treeSpeeds.BeginUpdate();
            treeSpeeds.Nodes.Clear();
            rootNode = new TreeNode("Show Speeds") { Tag = entry, Checked = shown, ToolTipText = entry.Path.Length > 0 ? entry.Path : "not saved yet" };
            foreach (StepEntry step in entry.Steps)
            {
                rootNode.Nodes.Add(new TreeNode(RunPanel.StepName(step.Info, u))
                {
                    Tag = step, Checked = step.Visible, ToolTipText = StepTip(step),
                });
            }
            treeSpeeds.Nodes.Add(rootNode);
            if (expanded)
            {
                rootNode.Expand();
            }
            treeSpeeds.EndUpdate();
            suppress = false;

            groupBoxRunItem.Text = entry.Display + (entry.InProgress ? " (running)" : "");
            // a run that is still being measured has nothing final to save or remove
            buttonSaveSummary.Enabled = buttonSavePoints.Enabled = buttonRemove.Enabled = !entry.InProgress;
            menuDeleteFile.Enabled = menuShowInFolder.Enabled = entry.Path.Length > 0;
            FitToTree();
        }

        private static string StepTip(StepEntry step)
        {
            string tip = step.Info.DriveHz.ToString("0.0") + " Hz drive, " + step.Info.PlannedCycles + " cycles";
            if (!double.IsNaN(step.Info.TempStartC))
            {
                tip += ", " + step.Info.TempStartC.ToString("0.0") + " °C";
            }
            return tip;
        }

        /// <summary> Units changed: the speed names again. </summary>
        public void RefreshTexts()
        {
            if (rootNode == null)
            {
                return;
            }
            suppress = true;
            UnitSystem u = AppSettings.Current.Units;
            foreach (TreeNode child in rootNode.Nodes)
            {
                if (child.Tag is StepEntry s)
                {
                    child.Text = RunPanel.StepName(s.Info, u);
                }
            }
            suppress = false;
            FitToTree();
        }

        /// <summary> Draws the colour squares: the colours the graphs use if the run is shown, else only the chosen ones. </summary>
        public void ApplyColours(bool shown)
        {
            if (entry == null || rootNode == null)
            {
                return;
            }
            SetSwatch(rootNode, shown ? entry.Shown : entry.Custom);
            foreach (TreeNode child in rootNode.Nodes)
            {
                var s = (StepEntry)child.Tag!;
                SetSwatch(child, shown ? s.Shown : s.Custom ?? entry.Custom);
            }
        }

        // ------------------------------------------------------------------ size

        /// <summary> The tree is as big as its visible nodes, and so are the group box and this control. </summary>
        private void FitToTree()
        {
            int rows = 0, width = 0;
            Font font = treeSpeeds.Font;
            void Visit(TreeNode n, int level)
            {
                rows++;
                int w = (level + 1) * treeSpeeds.Indent + 24 + swatches.ImageSize.Width + 10
                        + TextRenderer.MeasureText(n.Text, font).Width;
                width = Math.Max(width, w);
                if (n.IsExpanded)
                {
                    foreach (TreeNode c in n.Nodes)
                    {
                        Visit(c, level + 1);
                    }
                }
            }
            foreach (TreeNode n in treeSpeeds.Nodes)
            {
                Visit(n, 0);
            }

            treeSpeeds.Size = new Size(width, rows * treeSpeeds.ItemHeight + 8);
            int groupWidth = Math.Max(GroupMinWidth, TreeLeft + width + Pad);
            int groupHeight = TreeTop + treeSpeeds.Height + Pad + 2;
            Size = new Size(groupWidth + 2, groupHeight + 2); // the control's own 1 pixel border
        }

        // ------------------------------------------------------------------ colours

        private void SetSwatch(TreeNode n, Color? c)
        {
            n.ImageIndex = n.SelectedImageIndex = Swatch(c);
        }

        private int Swatch(Color? c)
        {
            int key = c.HasValue ? c.Value.ToArgb() : 0;
            if (swatchIndex.TryGetValue(key, out int idx))
            {
                return idx;
            }
            var bmp = new Bitmap(swatches.ImageSize.Width, swatches.ImageSize.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                int left = bmp.Width - SwatchSize; // the square sits at the right end of the image, next to the text
                if (c.HasValue)
                {
                    using var b = new SolidBrush(c.Value);
                    g.FillRectangle(b, left + 1, 1, 12, 12);
                }
                using var pen = new Pen(c.HasValue ? Color.FromArgb(90, 90, 90) : Color.FromArgb(200, 200, 200));
                g.DrawRectangle(pen, left + 1, 1, 11, 11);
            }
            swatches.Images.Add(bmp);
            idx = swatches.Images.Count - 1;
            swatchIndex[key] = idx;
            return idx;
        }

        /// <summary> Where a node's colour square is drawn: just left of its text. </summary>
        private Rectangle SwatchBounds(TreeNode n)
        {
            int w = SwatchSize + 4;
            return new Rectangle(n.Bounds.Left - w - 1, n.Bounds.Top, w + 1, n.Bounds.Height);
        }

        private void PickColour(TreeNode node)
        {
            Color? current = node.Tag is RunEntry r ? r.Custom ?? r.Shown : node.Tag is StepEntry s ? s.Shown : null;
            using var dialog = new ColorDialog { Color = current ?? PlotControl.Palette[0], FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SetColour(node, dialog.Color);
            }
        }

        private void SetColour(TreeNode node, Color? colour)
        {
            if (node.Tag is RunEntry r)
            {
                r.Custom = colour;
                foreach (StepEntry step in r.Steps) // the speeds follow the new colour again
                {
                    step.Custom = null;
                }
            }
            else if (node.Tag is StepEntry s)
            {
                s.Custom = colour;
            }
            ColoursChanged?.Invoke();
        }

        // ------------------------------------------------------------------ events (wired in RunItem.Designer.cs)

        private void buttonSaveSummary_Click(object? sender, EventArgs e)
        {
            SaveSummaryRequested?.Invoke(Entry);
        }

        private void buttonSavePoints_Click(object? sender, EventArgs e)
        {
            SavePointsRequested?.Invoke(Entry);
        }

        private void buttonRemove_Click(object? sender, EventArgs e)
        {
            RemoveRequested?.Invoke(Entry);
        }

        private void treeSpeeds_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (suppress || e.Node == null)
            {
                return;
            }
            if (e.Node.Tag is StepEntry step)
            {
                step.Visible = e.Node.Checked;
                if (e.Node.Checked && rootNode != null && !rootNode.Checked) // showing a speed shows its run
                {
                    suppress = true;
                    rootNode.Checked = true;
                    suppress = false;
                }
            }
            SelectionChanged?.Invoke();
        }

        private void treeSpeeds_AfterExpandCollapse(object? sender, TreeViewEventArgs e)
        {
            FitToTree();
        }

        private void treeSpeeds_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            treeSpeeds.SelectedNode = e.Node;
            if (e.Button == MouseButtons.Right)
            {
                menuNode = e.Node;
                menuRun.Show(treeSpeeds, e.Location);
            }
        }

        private void treeSpeeds_MouseClick(object? sender, MouseEventArgs e)
        {
            // a click on the colour square picks a colour
            TreeNode? n = treeSpeeds.GetNodeAt(e.Location);
            if (n != null && e.Button == MouseButtons.Left && SwatchBounds(n).Contains(e.Location))
            {
                PickColour(n);
            }
        }

        private void menuRun_Closed(object? sender, ToolStripDropDownClosedEventArgs e)
        {
            // keep menuNode until the click handler of the chosen item has run
            BeginInvoke(() => menuNode = null);
        }

        private void menuShowAll_Click(object? sender, EventArgs e)
        {
            SetAllSpeeds(true);
        }

        private void menuHideAll_Click(object? sender, EventArgs e)
        {
            SetAllSpeeds(false);
        }

        private void menuDefaultColour_Click(object? sender, EventArgs e)
        {
            TreeNode? node = menuNode ?? rootNode;
            if (node != null)
            {
                SetColour(node, null);
            }
        }

        private void menuShowInFolder_Click(object? sender, EventArgs e)
        {
            if (entry == null || entry.Path.Length == 0)
            {
                return;
            }
            try
            {
                Process.Start("explorer.exe", "/select,\"" + entry.Path + "\"");
            }
            catch (Exception)
            {
                // no explorer: nothing to show
            }
        }

        private void menuDeleteFile_Click(object? sender, EventArgs e)
        {
            if (entry == null || entry.Path.Length == 0)
            {
                return;
            }
            string message = "Delete this run file?\n\n" + entry.Path + "\n\nIt cannot be undone.";
            if (MessageBox.Show(this, message, "Delete run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                DeleteRequested?.Invoke(entry);
            }
        }

        private void SetAllSpeeds(bool on)
        {
            if (entry == null || rootNode == null)
            {
                return;
            }
            suppress = true;
            foreach (TreeNode child in rootNode.Nodes)
            {
                child.Checked = on;
                ((StepEntry)child.Tag!).Visible = on;
            }
            if (on)
            {
                rootNode.Checked = true;
            }
            suppress = false;
            SelectionChanged?.Invoke();
        }
    }
}
