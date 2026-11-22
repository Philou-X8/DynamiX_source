using DynamiX_HMI.Analysis;
using DynamiX_HMI.Config;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DynamiX_HMI.Grapher
{
    

    /// <summary>
    /// The run list: each run is a node with its speeds under it. Ticking a run shows it in the graphs, ticking
    /// a speed shows or hides that speed. The coloured square is the colour of the run in the graphs.
    /// </summary>
    public partial class RunPanel : UserControl
    {
        private readonly Dictionary<int, int> swatchIndex = new Dictionary<int, int>();
        private readonly Dictionary<RunEntry, TreeNode> nodes = new Dictionary<RunEntry, TreeNode>();
        private readonly List<RunEntry> entries = new List<RunEntry>();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private bool suppress;
        private bool changeQueued;

        /// <summary> Which runs or speeds are shown changed. </summary>
        public Action? SelectionChanged;
        /// <summary> The Export button: write the summary of the shown runs. </summary>
        public Action? ExportRequested;
        /// <summary> The Open button, with the chosen files. </summary>
        public Action<string[]>? OpenRequested;
        /// <summary> The Refresh button: read the runs folder again. </summary>
        public Action? RefreshRequested;
        /// <summary> Delete a saved run (already confirmed). </summary>
        public Action<RunEntry>? DeleteRequested;

        public RunPanel()
        {
            InitializeComponent();
        }

        // ------------------------------------------------------------------ events (wired in RunPanel.Designer.cs)

        private void buttonOpen_Click(object? sender, EventArgs e)
        {
            OpenFiles();
        }

        private void buttonExport_Click(object? sender, EventArgs e)
        {
            ExportRequested?.Invoke();
        }

        private void buttonRefresh_Click(object? sender, EventArgs e)
        {
            RefreshRequested?.Invoke();
        }

        private void tree_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            OnChecked(e.Node!);
        }

        private void tree_AfterExpand(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is RunEntry r)
            {
                expanded.Add(r.Path);
            }
        }

        private void tree_AfterCollapse(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is RunEntry r)
            {
                expanded.Remove(r.Path);
            }
        }

        private void tree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            tree.SelectedNode = e.Node;
            if (e.Button == MouseButtons.Right)
            {
                BuildMenu(e.Node!).Show(tree, e.Location);
            }
        }

        private void tree_MouseClick(object? sender, MouseEventArgs e)
        {
            // a click on the colour square picks a colour
            TreeNode? n = tree.GetNodeAt(e.Location);
            if (n != null && e.Button == MouseButtons.Left && SwatchBounds(n).Contains(e.Location))
            {
                PickColour(n);
            }
        }

        public IReadOnlyList<RunEntry> Entries => entries;

        // ------------------------------------------------------------------ content

        /// <summary> Replaces the list (runs read from the folder). Keeps the ticks of runs still present. </summary>
        public void SetEntries(IEnumerable<RunEntry> newEntries)
        {
            var wasChecked = new HashSet<string>(CheckedRuns().Where(r => r.Path.Length > 0).Select(r => r.Path));
            var inProgress = entries.Where(r => r.InProgress).ToList();
            suppress = true;
            tree.BeginUpdate();
            tree.Nodes.Clear();
            nodes.Clear();
            entries.Clear();
            foreach (RunEntry r in inProgress.Concat(newEntries))
            {
                if (inProgress.Contains(r) || !entries.Any(e => e.Path == r.Path))
                {
                    AddNode(r, wasChecked.Contains(r.Path) || r.InProgress, false);
                }
            }
            tree.EndUpdate();
            suppress = false;
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> A new run at the top of the list, shown right away. </summary>
        public void AddEntry(RunEntry entry)
        {
            suppress = true;
            AddNode(entry, true, true);
            suppress = false;
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> The run's speeds or name changed: rebuilds its node. </summary>
        public void UpdateEntry(RunEntry entry)
        {
            if (!nodes.TryGetValue(entry, out TreeNode? node))
            {
                return;
            }
            suppress = true;
            bool wasExpanded = node.IsExpanded;
            FillNode(node, entry);
            if (wasExpanded || entry.InProgress)
            {
                node.Expand();
            }
            suppress = false;
            AssignColours();
        }

        public void RemoveEntry(RunEntry entry)
        {
            if (nodes.TryGetValue(entry, out TreeNode? node))
            {
                suppress = true;
                node.Remove();
                suppress = false;
                nodes.Remove(entry);
            }
            entries.Remove(entry);
            AssignColours();
            QueueSelectionChanged();
        }

        public IEnumerable<RunEntry> CheckedRuns()
        {
            foreach (RunEntry r in entries)
            {
                if (nodes.TryGetValue(r, out TreeNode? n) && n.Checked)
                {
                    yield return r;
                }
            }
        }

        private void AddNode(RunEntry entry, bool isChecked, bool atTop)
        {
            var node = new TreeNode(entry.Display) { Tag = entry, Checked = isChecked };
            FillNode(node, entry);
            if (atTop)
            {
                tree.Nodes.Insert(0, node);
                entries.Insert(0, entry);
            }
            else
            {
                tree.Nodes.Add(node);
                entries.Add(entry);
            }
            nodes[entry] = node;
            if (expanded.Contains(entry.Path) || entry.InProgress)
            {
                node.Expand();
            }
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

        private static string SpeedText(StepEntry s, UnitSystem u)
        {
            string text = StepName(s.Info, u) + "   " + s.Info.DriveHz.ToString("0.0") + " Hz";
            if (!double.IsNaN(s.Info.TempStartC))
            {
                text += "   " + s.Info.TempStartC.ToString("0.0") + " °C";
            }
            return text;
        }

        private void FillNode(TreeNode node, RunEntry entry)
        {
            node.Text = entry.Display + (entry.InProgress ? "  (running)" : "");
            node.ToolTipText = entry.Path.Length > 0 ? entry.Path : "not saved yet";
            node.Nodes.Clear();
            UnitSystem u = AppSettings.Current.Units;
            foreach (StepEntry s in entry.Steps)
            {
                node.Nodes.Add(new TreeNode(SpeedText(s, u)) { Tag = s, Checked = s.Visible });
            }
        }

        /// <summary> Updates the speed lines after an analysis (peak speeds) or a change of units. </summary>
        public void RefreshTexts()
        {
            suppress = true;
            UnitSystem u = AppSettings.Current.Units;
            foreach (var pair in nodes)
            {
                foreach (TreeNode child in pair.Value.Nodes)
                {
                    if (child.Tag is StepEntry s)
                    {
                        child.Text = SpeedText(s, u);
                    }
                }
            }
            suppress = false;
        }

        // ------------------------------------------------------------------ ticks and colours

        private void OnChecked(TreeNode node)
        {
            if (suppress)
            {
                return;
            }
            if (node.Tag is StepEntry step)
            {
                step.Visible = node.Checked;
                if (node.Checked && node.Parent != null && !node.Parent.Checked) // showing a speed shows its run
                {
                    suppress = true;
                    node.Parent.Checked = true;
                    suppress = false;
                }
            }
            AssignColours();
            QueueSelectionChanged();
        }

        private void QueueSelectionChanged()
        {
            if (changeQueued || !IsHandleCreated)
            {
                if (!IsHandleCreated)
                {
                    SelectionChanged?.Invoke();
                }
                return;
            }
            changeQueued = true;
            BeginInvoke(() =>
            {
                changeQueued = false;
                SelectionChanged?.Invoke();
            });
        }

        /// <summary> Chosen colours first; the other shown runs take palette colours not already used, in list order. </summary>
        public void AssignColours()
        {
            var shown = CheckedRuns().ToList();
            var used = new HashSet<int>(shown.Where(r => r.Custom.HasValue).Select(r => r.Custom!.Value.ToArgb()));
            int k = 0;
            foreach (RunEntry r in shown)
            {
                if (r.Custom.HasValue)
                {
                    r.Shown = r.Custom.Value;
                    continue;
                }
                Color c = PlotControl.Palette[k % PlotControl.Palette.Length];
                for (int tries = 0; tries < PlotControl.Palette.Length && used.Contains(c.ToArgb()); tries++)
                {
                    c = PlotControl.Palette[++k % PlotControl.Palette.Length];
                }
                r.Shown = c;
                used.Add(c.ToArgb());
                k++;
            }
            var shownSet = new HashSet<RunEntry>(shown);
            foreach (var pair in nodes)
            {
                RunEntry r = pair.Key;
                bool on = shownSet.Contains(r);
                SetSwatch(pair.Value, on ? r.Shown : r.Custom);
                foreach (TreeNode child in pair.Value.Nodes)
                {
                    var s = (StepEntry)child.Tag!;
                    SetSwatch(child, on ? s.Shown : s.Custom ?? r.Custom);
                }
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
            var bmp = new Bitmap(14, 14);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (c.HasValue)
                {
                    using var b = new SolidBrush(c.Value);
                    g.FillRectangle(b, 1, 1, 12, 12);
                }
                using var pen = new Pen(c.HasValue ? Color.FromArgb(90, 90, 90) : Color.FromArgb(200, 200, 200));
                g.DrawRectangle(pen, 1, 1, 11, 11);
            }
            swatches.Images.Add(bmp);
            idx = swatches.Images.Count - 1;
            swatchIndex[key] = idx;
            return idx;
        }

        /// <summary> Where a node's colour square is drawn: just left of its text. </summary>
        private Rectangle SwatchBounds(TreeNode n)
        {
            int w = swatches.ImageSize.Width + 4;
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
            }
            else if (node.Tag is StepEntry s)
            {
                s.Custom = colour;
            }
            AssignColours();
            QueueSelectionChanged();
        }

        // ------------------------------------------------------------------ menu and files

        private ContextMenuStrip BuildMenu(TreeNode node)
        {
            var menu = new ContextMenuStrip();
            TreeNode runNode = node.Parent ?? node;
            menu.Items.Add("Show all speeds", null, (s, e) => SetAllSteps(runNode, true));
            menu.Items.Add("Hide all speeds", null, (s, e) => SetAllSteps(runNode, false));
            menu.Items.Add("Colour...", null, (s, e) => PickColour(node));
            menu.Items.Add("Default colour", null, (s, e) => SetColour(node, null));
            if (runNode.Tag is RunEntry entry && entry.Path.Length > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Show in folder", null, (s, e) => ShowInFolder(entry.Path));
                menu.Items.Add("Delete file...", null, (s, e) => ConfirmDelete(entry));
            }
            return menu;
        }

        private void SetAllSteps(TreeNode runNode, bool on)
        {
            suppress = true;
            foreach (TreeNode child in runNode.Nodes)
            {
                child.Checked = on;
                ((StepEntry)child.Tag!).Visible = on;
            }
            if (on)
            {
                runNode.Checked = true;
            }
            suppress = false;
            AssignColours();
            QueueSelectionChanged();
        }

        private static void ShowInFolder(string path)
        {
            try
            {
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception)
            {
                // no explorer: nothing to show
            }
        }

        private void ConfirmDelete(RunEntry entry)
        {
            string message = "Delete this run file?\n\n" + entry.Path + "\n\nIt cannot be undone.";
            if (MessageBox.Show(this, message, "Delete run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                DeleteRequested?.Invoke(entry);
            }
        }

        private void OpenFiles()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "DynamiX run (*.csv)|*.csv",
                Multiselect = true,
                InitialDirectory = Directory.Exists(AppSettings.Current.RunsFolder) ? AppSettings.Current.RunsFolder : "",
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                OpenRequested?.Invoke(dialog.FileNames);
            }
        }
    }
}
