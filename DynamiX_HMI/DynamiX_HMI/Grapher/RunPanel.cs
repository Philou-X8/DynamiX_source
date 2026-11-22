using DynamiX_HMI.Analysis;
using DynamiX_HMI.Config;
using System.Drawing;
using System.Windows.Forms;

namespace DynamiX_HMI.Grapher
{
    /// <summary>
    /// The run list: a group box with one RunItem per run, newest first. Each item has its own speeds and buttons.
    /// A run is shown in the graphs when its Show Speeds box is ticked, and then each of its speeds can be hidden.
    /// </summary>
    public partial class RunPanel : UserControl
    {
        private readonly Dictionary<RunEntry, RunItem> items = new Dictionary<RunEntry, RunItem>();
        private readonly List<RunEntry> entries = new List<RunEntry>();
        private bool changeQueued;

        /// <summary> Which runs or speeds are shown changed. </summary>
        public Action? SelectionChanged;
        /// <summary> The Export all button: one summary of every shown run. </summary>
        public Action? ExportRequested;
        /// <summary> The Save Summary button of a run. </summary>
        public Action<RunEntry>? ExportRunRequested;
        /// <summary> The Save Points button of a run. </summary>
        public Action<RunEntry>? SavePointsRequested;
        /// <summary> The Open button, with the chosen files. </summary>
        public Action<string[]>? OpenRequested;
        /// <summary> The Refresh button: read the runs folder again. </summary>
        public Action? RefreshRequested;
        /// <summary> Delete a saved run's file (already confirmed). </summary>
        public Action<RunEntry>? DeleteRequested;

        public RunPanel()
        {
            InitializeComponent();
        }

        public IReadOnlyList<RunEntry> Entries => entries;

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

        // ------------------------------------------------------------------ content

        /// <summary> Replaces the list (runs read from the folder). Keeps the ticks of runs still present. </summary>
        public void SetEntries(IEnumerable<RunEntry> newEntries)
        {
            var wasShown = new HashSet<string>(CheckedRuns().Where(r => r.Path.Length > 0).Select(r => r.Path));
            var inProgress = entries.Where(r => r.InProgress).ToList();

            flowRuns.SuspendLayout();
            foreach (RunItem item in items.Values)
            {
                flowRuns.Controls.Remove(item);
                item.Dispose();
            }
            items.Clear();
            entries.Clear();
            foreach (RunEntry r in inProgress.Concat(newEntries))
            {
                if (inProgress.Contains(r) || !entries.Any(e => e.Path == r.Path))
                {
                    AddItem(r, wasShown.Contains(r.Path) || r.InProgress, false);
                }
            }
            flowRuns.ResumeLayout(true);
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> A new run at the top of the list, shown right away. </summary>
        public void AddEntry(RunEntry entry)
        {
            flowRuns.SuspendLayout();
            AddItem(entry, true, true);
            flowRuns.ResumeLayout(true);
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> The run's speeds or state changed: builds its item again. </summary>
        public void UpdateEntry(RunEntry entry)
        {
            if (items.TryGetValue(entry, out RunItem? item))
            {
                item.Rebuild();
                AssignColours();
            }
        }

        public void RemoveEntry(RunEntry entry)
        {
            if (items.TryGetValue(entry, out RunItem? item))
            {
                flowRuns.Controls.Remove(item);
                item.Dispose();
                items.Remove(entry);
            }
            entries.Remove(entry);
            AssignColours();
            QueueSelectionChanged();
        }

        /// <summary> The runs whose Show Speeds box is ticked, newest first. </summary>
        public IEnumerable<RunEntry> CheckedRuns()
        {
            foreach (RunEntry r in entries)
            {
                if (items.TryGetValue(r, out RunItem? item) && item.IsShown)
                {
                    yield return r;
                }
            }
        }

        private void AddItem(RunEntry entry, bool shown, bool atTop)
        {
            var item = new RunItem { Margin = new Padding(3, 3, 3, 3) };
            item.Bind(entry, shown);
            item.SelectionChanged = () =>
            {
                AssignColours();
                QueueSelectionChanged();
            };
            item.ColoursChanged = () =>
            {
                AssignColours();
                QueueSelectionChanged();
            };
            item.SaveSummaryRequested = e => ExportRunRequested?.Invoke(e);
            item.SavePointsRequested = e => SavePointsRequested?.Invoke(e);
            item.RemoveRequested = ConfirmRemove;
            item.DeleteRequested = e => DeleteRequested?.Invoke(e);

            flowRuns.Controls.Add(item);
            if (atTop)
            {
                flowRuns.Controls.SetChildIndex(item, 0);
                entries.Insert(0, entry);
            }
            else
            {
                entries.Add(entry);
            }
            items[entry] = item;
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
            foreach (RunItem item in items.Values)
            {
                item.RefreshTexts();
            }
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
            foreach (var pair in items)
            {
                pair.Value.ApplyColours(shownSet.Contains(pair.Key));
            }
        }

        // ------------------------------------------------------------------ remove and open

        /// <summary> The X button: takes the run off the list. A run that was never saved would be lost: ask first. </summary>
        private void ConfirmRemove(RunEntry entry)
        {
            if (entry.InProgress)
            {
                return;
            }
            if (entry.Path.Length == 0 && entry.Run.Samples.Count > 0)
            {
                string message = "This run was not saved (saving runs automatically is off).\n\n"
                                 + "Removing it from the list loses its data. Use Save Points first to keep it.\n\nRemove it anyway?";
                if (MessageBox.Show(this, message, "Remove run", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }
            }
            RemoveEntry(entry);
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
