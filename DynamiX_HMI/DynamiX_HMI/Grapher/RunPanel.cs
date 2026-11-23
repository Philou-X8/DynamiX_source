using DynamiX_HMI.Analysis;
using DynamiX_HMI.Config;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// The run list: one tree for the whole project, newest run first. Each run is a root named after the run, its
    /// speeds are the leaves. A leaf's box shows or hides its speed in the graphs; the box of a run ticks or unticks
    /// all its speeds, and follows them (ticked as long as at least one speed is). Right click a run for what used
    /// to be its buttons: save the summary or the points, remove it, delete its file.
    /// </summary>
    public partial class RunPanel : UserControl
    {
        private const int SwatchSize = 14;        // the colour square; the image is wider (see RunPanel.Designer.cs):
                                                  // the rest is empty space between the check box and the square

        private readonly Dictionary<RunEntry, TreeNode> nodes = new Dictionary<RunEntry, TreeNode>();
        private readonly List<RunEntry> entries = new List<RunEntry>();
        private readonly Dictionary<int, int> swatchIndex = new Dictionary<int, int>();
        private TreeNode? menuNode;               // the node a right click opened the menu on
        private bool suppress;
        private bool changeQueued;

        /// <summary> Which runs or speeds are shown changed. </summary>
        public Action? SelectionChanged;
        /// <summary> The Export button: one summary of every run that has a speed ticked. </summary>
        public Action? ExportRequested;
        /// <summary> Save summary, from a run's menu. </summary>
        public Action<RunEntry>? ExportRunRequested;
        /// <summary> Save points, from a run's menu. </summary>
        public Action<RunEntry>? SavePointsRequested;
        /// <summary> The Import button, with the chosen files. </summary>
        public Action<string[]>? ImportRequested;
        /// <summary> Delete a saved run's file (already confirmed). </summary>
        public Action<RunEntry>? DeleteRequested;

        public RunPanel()
        {
            InitializeComponent();
        }

        public IReadOnlyList<RunEntry> Entries => entries;

        // ------------------------------------------------------------------ events (wired in RunPanel.Designer.cs)

        private void buttonImport_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Import runs into the project",
                Filter = "DynamiX run (*.csv)|*.csv",
                Multiselect = true,
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                ImportRequested?.Invoke(dialog.FileNames);
            }
        }

        private void buttonExport_Click(object? sender, EventArgs e)
        {
            ExportRequested?.Invoke();
        }

        // ------------------------------------------------------------------ content

        /// <summary>
        /// Adds a run to the list: at the top (a new run) or at the bottom (a project being loaded, oldest last).
        /// 'shown' ticks all its speeds or none.
        /// </summary>
        public void AddEntry(RunEntry entry, bool shown, bool atTop = true)
        {
            foreach (StepEntry step in entry.Steps)
            {
                step.Visible = shown;
            }
            var node = new TreeNode { Tag = entry, Checked = shown };
            suppress = true;
            if (atTop)
            {
                treeRuns.Nodes.Insert(0, node);
                entries.Insert(0, entry);
            }
            else
            {
                treeRuns.Nodes.Add(node);
                entries.Add(entry);
            }
            suppress = false;
            nodes[entry] = node;
            FillNode(node, entry, shown);
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> The run's speeds or state changed: builds its node again. </summary>
        public void UpdateEntry(RunEntry entry)
        {
            if (nodes.TryGetValue(entry, out TreeNode? node))
            {
                // speeds that arrive while the run is being measured follow the run's box
                bool wasShown = node.Checked;
                for (int i = node.Nodes.Count; i < entry.Steps.Count; i++)
                {
                    entry.Steps[i].Visible = wasShown;
                }
                FillNode(node, entry, wasShown);
                AssignColours();
                QueueSelectionChanged();
            }
        }

        public void RemoveEntry(RunEntry entry)
        {
            if (nodes.TryGetValue(entry, out TreeNode? node))
            {
                node.Remove();
                nodes.Remove(entry);
            }
            entries.Remove(entry);
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> Empties the list (another project is loaded). </summary>
        public void Clear()
        {
            treeRuns.Nodes.Clear();
            nodes.Clear();
            entries.Clear();
            QueueSelectionChanged();
        }

        /// <summary> The runs with at least one speed ticked (a run still without speeds: its own box), newest first. </summary>
        public IEnumerable<RunEntry> CheckedRuns()
        {
            foreach (RunEntry r in entries)
            {
                if (IsShown(r))
                {
                    yield return r;
                }
            }
        }

        private bool IsShown(RunEntry entry)
        {
            if (entry.Steps.Count > 0)
            {
                return entry.Steps.Any(s => s.Visible);
            }
            return nodes.TryGetValue(entry, out TreeNode? node) && node.Checked;
        }

        /// <summary> (Re)builds the text, tooltip and speeds of a run's node, keeping it expanded or collapsed. </summary>
        private void FillNode(TreeNode node, RunEntry entry, bool shownWithoutSpeeds)
        {
            bool expanded = node.IsExpanded; // a new run starts collapsed
            UnitSystem u = AppSettings.Current.Units;
            suppress = true;
            treeRuns.BeginUpdate();
            node.Nodes.Clear();
            node.Text = entry.Display + (entry.InProgress ? " (running)" : "");
            node.ToolTipText = entry.Path.Length > 0 ? entry.Path : "not saved yet";
            foreach (StepEntry step in entry.Steps)
            {
                node.Nodes.Add(new TreeNode(StepName(step.Info, u))
                {
                    Tag = step, Checked = step.Visible, ToolTipText = StepTip(step),
                });
            }
            node.Checked = entry.Steps.Count > 0 ? entry.Steps.Any(s => s.Visible) : shownWithoutSpeeds;
            if (expanded)
            {
                node.Expand();
            }
            treeRuns.EndUpdate();
            suppress = false;
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

        /// <summary> A speed by its target peak speed, e.g. "250 mm/s" (the drive frequency if it has none). </summary>
        public static string StepName(StepInfo info, UnitSystem u)
        {
            if (info.TargetSpeedMmS > 0)
            {
                return Units.Length(info.TargetSpeedMmS, u).ToString(u == UnitSystem.SI ? "0" : "0.00") + " " + Units.SpeedUnit(u);
            }
            return info.DriveHz.ToString("0.0") + " Hz";
        }

        /// <summary> Updates the speed names after a change of units. </summary>
        public void RefreshTexts()
        {
            suppress = true;
            UnitSystem u = AppSettings.Current.Units;
            foreach (TreeNode root in treeRuns.Nodes)
            {
                foreach (TreeNode child in root.Nodes)
                {
                    if (child.Tag is StepEntry s)
                    {
                        child.Text = StepName(s.Info, u);
                    }
                }
            }
            suppress = false;
        }

        // ------------------------------------------------------------------ selection and colours

        private void QueueSelectionChanged()
        {
            if (!IsHandleCreated)
            {
                SelectionChanged?.Invoke();
                return;
            }
            if (changeQueued)
            {
                return;
            }
            changeQueued = true;
            BeginInvoke(() =>
            {
                changeQueued = false;
                SelectionChanged?.Invoke();
            });
        }

        private void treeRuns_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (suppress || e.Node == null)
            {
                return;
            }
            suppress = true;
            if (e.Node.Tag is RunEntry run)
            {
                // the run's box: every speed follows it
                foreach (TreeNode child in e.Node.Nodes)
                {
                    child.Checked = e.Node.Checked;
                    ((StepEntry)child.Tag!).Visible = e.Node.Checked;
                }
            }
            else if (e.Node.Tag is StepEntry step && e.Node.Parent != null)
            {
                step.Visible = e.Node.Checked;
                // the run's box stays ticked as long as one speed is
                e.Node.Parent.Checked = e.Node.Parent.Nodes.Cast<TreeNode>().Any(n => n.Checked);
            }
            suppress = false;
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary>
        /// Every run keeps its colour: the one chosen, else the palette colour it was given when it joined the list
        /// (a free one, in list order). Hiding or showing a run changes nothing for the others.
        /// </summary>
        public void AssignColours()
        {
            var used = new Dictionary<int, int>(); // colour -> runs using it
            void Use(Color c) => used[c.ToArgb()] = used.TryGetValue(c.ToArgb(), out int n) ? n + 1 : 1;
            foreach (RunEntry r in entries)
            {
                if (r.Custom.HasValue) Use(r.Custom.Value);
                else if (r.Auto.HasValue) Use(r.Auto.Value);
            }
            foreach (RunEntry r in entries)
            {
                if (!r.Custom.HasValue && !r.Auto.HasValue)
                {
                    // the least used palette colour, the first one of those
                    Color pick = PlotControl.Palette.OrderBy(c => used.TryGetValue(c.ToArgb(), out int n) ? n : 0).First();
                    r.Auto = pick;
                    Use(pick);
                }
                r.Shown = r.Custom ?? r.Auto!.Value;
            }
            foreach (RunEntry r in entries)
            {
                ApplyColours(r, IsShown(r));
            }
        }

        /// <summary> Draws the colour squares: the colours the graphs use if the run is shown, else only the chosen ones. </summary>
        private void ApplyColours(RunEntry entry, bool shown)
        {
            if (!nodes.TryGetValue(entry, out TreeNode? root))
            {
                return;
            }
            SetSwatch(root, shown ? entry.Shown : entry.Custom);
            foreach (TreeNode child in root.Nodes)
            {
                var s = (StepEntry)child.Tag!;
                SetSwatch(child, shown ? s.Shown : s.Custom ?? entry.Custom);
            }
        }

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
            AssignColours();
            QueueSelectionChanged();
        }

        // ------------------------------------------------------------------ mouse and menu

        private static RunEntry? RunOf(TreeNode? node)
        {
            return node?.Tag is RunEntry r ? r : node?.Tag is StepEntry s ? s.Run : null;
        }

        private void treeRuns_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            treeRuns.SelectedNode = e.Node;
            if (e.Button == MouseButtons.Right)
            {
                menuNode = e.Node;
                RunEntry? run = RunOf(e.Node);
                bool final = run != null && !run.InProgress; // a run still being measured has nothing final to save
                bool saved = run != null && run.Path.Length > 0;
                menuSaveSummary.Enabled = menuSavePoints.Enabled = menuRemove.Enabled = final;
                menuShowInFolder.Enabled = saved;
                menuDeleteFile.Enabled = saved && final;
                menuRun.Show(treeRuns, e.Location);
            }
        }

        private void treeRuns_MouseClick(object? sender, MouseEventArgs e)
        {
            // a click on the colour square picks a colour
            TreeNode? n = treeRuns.GetNodeAt(e.Location);
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
            if (menuNode != null)
            {
                SetColour(menuNode, null);
            }
        }

        private void menuSaveSummary_Click(object? sender, EventArgs e)
        {
            RunEntry? run = RunOf(menuNode);
            if (run != null)
            {
                ExportRunRequested?.Invoke(run);
            }
        }

        private void menuSavePoints_Click(object? sender, EventArgs e)
        {
            RunEntry? run = RunOf(menuNode);
            if (run != null)
            {
                SavePointsRequested?.Invoke(run);
            }
        }

        private void menuShowInFolder_Click(object? sender, EventArgs e)
        {
            RunEntry? run = RunOf(menuNode);
            if (run == null || run.Path.Length == 0)
            {
                return;
            }
            try
            {
                Process.Start("explorer.exe", "/select,\"" + run.Path + "\"");
            }
            catch (Exception)
            {
                // no explorer: nothing to show
            }
        }

        private void menuDeleteFile_Click(object? sender, EventArgs e)
        {
            RunEntry? run = RunOf(menuNode);
            if (run == null || run.Path.Length == 0)
            {
                return;
            }
            string message = "Delete this run file?\n\n" + run.Path + "\n\nIt cannot be undone.";
            if (MessageBox.Show(this, message, "Delete run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                DeleteRequested?.Invoke(run);
            }
        }

        private void menuRemove_Click(object? sender, EventArgs e)
        {
            RunEntry? run = RunOf(menuNode);
            if (run != null)
            {
                ConfirmRemove(run);
            }
        }

        private void SetAllSpeeds(bool on)
        {
            RunEntry? run = RunOf(menuNode);
            if (run == null || !nodes.TryGetValue(run, out TreeNode? root))
            {
                return;
            }
            suppress = true;
            foreach (TreeNode child in root.Nodes)
            {
                child.Checked = on;
                ((StepEntry)child.Tag!).Visible = on;
            }
            root.Checked = on;
            suppress = false;
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> Takes the run off the list. A run that was never saved would be lost: ask first. </summary>
        private void ConfirmRemove(RunEntry entry)
        {
            if (entry.InProgress)
            {
                return;
            }
            if (entry.Path.Length == 0 && entry.Run.Samples.Count > 0)
            {
                string message = "This run was not saved (saving runs automatically is off).\n\n"
                                 + "Removing it from the list loses its data. Use Save points as first to keep it.\n\nRemove it anyway?";
                if (MessageBox.Show(this, message, "Remove run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }
            }
            RemoveEntry(entry);
        }
    }
}
