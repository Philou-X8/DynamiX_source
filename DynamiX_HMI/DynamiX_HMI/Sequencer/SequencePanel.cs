using DynamiX_HMI.Config;
using DynamiX_HMI.Serial;
using System.Globalization;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace DynamiX_HMI.Sequencer
{
    /// <summary>
    /// The sequence builder: a table with one row per step,
    ///   | down | up | speed | speed unit | duration | duration unit | remove |
    /// Speed units: mm/s (default), m/s, drive Hz, yoke RPM. Duration units: cycles (default), seconds.
    /// Type in the empty row at the bottom (or press Add) to add a step: it takes the units and the duration of the
    /// row above, 5 cycles at first. A row that can't be used is shaded and its tooltip says why.
    /// A speed in mm/s or m/s needs the stroke, which comes from the TAR (UpdateStroke).
    /// </summary>
    public partial class SequencePanel : UserControl
    {
        private const string DefaultSpeedUnit = "mm/s";
        private const string DefaultDuration = "5";
        private const string DefaultDurationUnit = "cycles";
        private const string FileHeader = "# DynamiX test sequence: speed, speed unit, duration, duration unit";

        private double strokeMm;
        private bool strokeKnown;

        /// <summary> Why the last MakeSequence or GetPlan failed (empty when it worked). </summary>
        public string ErrorText { get; private set; } = "";

        public SequencePanel()
        {
            InitializeComponent();
        }

        /// <summary> The stroke from the TAR (mm). Rows in mm/s or m/s can't be used without it. </summary>
        public void UpdateStroke(float newStroke, bool isValid)
        {
            bool known = isValid && newStroke > 0;
            if (known == strokeKnown && (!known || Math.Abs(newStroke - strokeMm) < 1e-6))
            {
                return; // called with every live data message: only act on a change
            }
            strokeMm = known ? newStroke : 0;
            strokeKnown = known;
            RefreshRows();
        }

        // ------------------------------------------------------------------ the steps

        /// <summary>
        /// Reads every row. False if one can't be used (ErrorText says which and why), or the sequence is empty
        /// or longer than the ESP32 accepts.
        /// </summary>
        private bool TryReadSteps(out List<PlanStep> steps)
        {
            gridSequence.EndEdit(); // a value still being typed counts
            steps = new List<PlanStep>();
            ErrorText = "";
            int number = 0;
            foreach (DataGridViewRow row in gridSequence.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }
                number++;
                if (!Resolve(row, out PlanStep step, out string error))
                {
                    ErrorText = "Step " + number + ": " + error;
                    steps.Clear();
                    return false;
                }
                steps.Add(step);
            }
            if (steps.Count == 0)
            {
                ErrorText = "The sequence is empty: add a step";
                return false;
            }
            if (steps.Count > SequenceMath.MaxSteps)
            {
                ErrorText = "At most " + SequenceMath.MaxSteps + " steps (the ESP32 refuses more), there are " + steps.Count;
                steps.Clear();
                return false;
            }
            return true;
        }

        private bool Resolve(DataGridViewRow row, out PlanStep step, out string error)
        {
            return SequenceMath.TryResolve(CellText(row, columnSpeed), CellText(row, columnSpeedUnit),
                                           CellText(row, columnDuration), CellText(row, columnDurationUnit),
                                           strokeMm, strokeKnown, out step, out error);
        }

        private static string CellText(DataGridViewRow row, DataGridViewColumn column)
        {
            return Convert.ToString(row.Cells[column.Index].Value) ?? "";
        }

        /// <summary>
        /// Builds one GUI_send_sequence command per row, in the order they are shown.
        /// Payload: [order][number of cycles][Modbus frame setting the drive frequency (8 bytes)]
        /// </summary>
        public bool MakeSequence(out List<CommandPacket> sequence)
        {
            sequence = new List<CommandPacket>();
            if (!TryReadSteps(out List<PlanStep> steps))
            {
                return false;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                byte[] driveCommand;
                try
                {
                    driveCommand = DriveCommands.BuildSetFrequency(steps[i].DriveHz);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    ErrorText = "Step " + (i + 1) + ": " + ex.Message;
                    sequence.Clear();
                    return false;
                }

                byte[] payload = new byte[2 + driveCommand.Length];
                payload[0] = (byte)i;
                payload[1] = (byte)steps[i].Cycles;
                driveCommand.CopyTo(payload, 2);

                // the encoder sends each char as one byte, so Latin1 keeps all 256 values intact
                sequence.Add(new CommandPacket(CommandFlags.GUI_send_sequence, Encoding.Latin1.GetString(payload)));
            }
            return true;
        }

        /// <summary> The steps as a plan (target speed in mm/s, drive Hz, cycles), in the order they are sent. </summary>
        public bool GetPlan(out List<(double targetMmS, double hz, int cycles)> plan)
        {
            plan = new List<(double, double, int)>();
            if (!TryReadSteps(out List<PlanStep> steps))
            {
                return false;
            }
            foreach (PlanStep s in steps)
            {
                plan.Add((s.TargetMmS, s.DriveHz, s.Cycles));
            }
            return true;
        }

        // ------------------------------------------------------------------ rows

        /// <summary> Units and duration of the last row (what a new row starts with), or the defaults. </summary>
        private (string speedUnit, string duration, string durationUnit) NewRowValues()
        {
            for (int i = gridSequence.Rows.Count - 1; i >= 0; i--)
            {
                DataGridViewRow row = gridSequence.Rows[i];
                if (row.IsNewRow)
                {
                    continue;
                }
                string speedUnit = CellText(row, columnSpeedUnit), duration = CellText(row, columnDuration), durationUnit = CellText(row, columnDurationUnit);
                return (speedUnit.Length > 0 ? speedUnit : DefaultSpeedUnit,
                        duration.Length > 0 ? duration : DefaultDuration,
                        durationUnit.Length > 0 ? durationUnit : DefaultDurationUnit);
            }
            return (DefaultSpeedUnit, DefaultDuration, DefaultDurationUnit);
        }

        private void AddRow()
        {
            var (speedUnit, duration, durationUnit) = NewRowValues();
            int index = gridSequence.Rows.Add("", "", "", speedUnit, duration, durationUnit, "");
            gridSequence.CurrentCell = gridSequence[columnSpeed.Index, index];
            gridSequence.BeginEdit(true);
        }

        private void MoveRow(int index, int delta)
        {
            int target = index + delta;
            int last = gridSequence.Rows.Count - (gridSequence.AllowUserToAddRows ? 2 : 1); // not past the empty row
            if (target < 0 || target > last)
            {
                return;
            }
            DataGridViewRow row = gridSequence.Rows[index];
            gridSequence.Rows.RemoveAt(index);
            gridSequence.Rows.Insert(target, row);
            gridSequence.CurrentCell = gridSequence[columnSpeed.Index, target];
        }

        /// <summary> Shades the rows that can't be used and puts what each one means in its tooltip. </summary>
        private void RefreshRows()
        {
            DataGridViewColumn[] inputs = { columnSpeed, columnSpeedUnit, columnDuration, columnDurationUnit };
            foreach (DataGridViewRow row in gridSequence.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }
                bool ok = Resolve(row, out PlanStep step, out string error);
                foreach (DataGridViewColumn column in inputs)
                {
                    DataGridViewCell cell = row.Cells[column.Index];
                    cell.Style.BackColor = ok ? Color.Empty : Color.MistyRose;
                    cell.ToolTipText = ok ? SequenceMath.Describe(step) : error;
                }
            }
        }

        // ------------------------------------------------------------------ files

        /// <summary> Where sequences are kept: the Sequences folder of the data folder. </summary>
        private static string SequenceFolder
        {
            get
            {
                string folder = Path.Combine(AppSettings.Current.DataFolder, "Sequences");
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception)
                {
                    return "";
                }
                return folder;
            }
        }

        /// <summary>
        /// Writes the table to a text file, one step per line: speed, speed unit, duration, duration unit.
        /// Rows without a speed are left out. Rows that can't be used yet are saved as typed.
        /// </summary>
        private void SaveToFile()
        {
            List<string> lines = BuildFileLines();
            if (lines.Count == 1)
            {
                MessageBox.Show(this, "There is nothing to save: the table has no step.", "Save sequence", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Sequence (*.seq)|*.seq|All files|*.*",
                DefaultExt = "seq",
                InitialDirectory = SequenceFolder,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            try
            {
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the sequence:\n\n" + ex.Message, "Save sequence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary> The text of the file: the header, then one line per row that has a speed. </summary>
        private List<string> BuildFileLines()
        {
            gridSequence.EndEdit();
            var lines = new List<string> { FileHeader };
            foreach (DataGridViewRow row in gridSequence.Rows)
            {
                if (row.IsNewRow || CellText(row, columnSpeed).Trim().Length == 0)
                {
                    continue;
                }
                lines.Add(string.Join(", ", Number(CellText(row, columnSpeed)), CellText(row, columnSpeedUnit),
                                      Number(CellText(row, columnDuration)), CellText(row, columnDurationUnit)));
            }
            return lines;
        }

        /// <summary> Replaces the table with parsed rows (speed, speed unit, duration, duration unit). </summary>
        private void ReplaceRows(List<string[]> rows)
        {
            gridSequence.EndEdit();
            gridSequence.Rows.Clear();
            foreach (string[] r in rows)
            {
                gridSequence.Rows.Add("", "", r[0], r[1], r[2], r[3], "");
            }
            RefreshRows();
        }

        /// <summary> Replaces the table with the steps of a sequence file. </summary>
        private void LoadFromFile()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Sequence (*.seq)|*.seq|All files|*.*",
                InitialDirectory = SequenceFolder,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            try
            {
                ReplaceRows(ParseSequenceFile(File.ReadAllLines(dialog.FileName)));
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Could not load the sequence:\n\n" + ex.Message, "Load sequence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// One step per line: speed, speed unit, duration, duration unit. Text after a # is a comment. The units must be
        /// ones of the lists (any case); a number that isn't one is loaded as it is, and its row is shaded.
        /// </summary>
        internal static List<string[]> ParseSequenceFile(IEnumerable<string> lines)
        {
            var rows = new List<string[]>();
            int number = 0;
            foreach (string raw in lines)
            {
                number++;
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0)
                {
                    line = line.Substring(0, hash);
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                string[] f = line.Split(',').Select(x => x.Trim()).ToArray();
                if (f.Length != 4)
                {
                    throw new FormatException("Line " + number + ": expected \"speed, speed unit, duration, duration unit\" but got \"" + raw.Trim() + "\"");
                }
                if (!SequenceMath.TryParseSpeedUnit(f[1], out SpeedUnit speedUnit))
                {
                    throw new FormatException("Line " + number + ": unknown speed unit \"" + f[1] + "\" (" + string.Join(", ", SequenceMath.SpeedUnitNames) + ")");
                }
                if (!SequenceMath.TryParseDurationUnit(f[3], out DurationUnit durationUnit))
                {
                    throw new FormatException("Line " + number + ": unknown duration unit \"" + f[3] + "\" (" + string.Join(", ", SequenceMath.DurationUnitNames) + ")");
                }
                rows.Add(new[] { f[0], SequenceMath.SpeedUnitNames[(int)speedUnit], f[2], SequenceMath.DurationUnitNames[(int)durationUnit] });
            }
            return rows;
        }

        /// <summary> A number written with a point whatever the regional settings are (text that isn't a number: as it is). </summary>
        private static string Number(string text)
        {
            return SequenceMath.TryParseNumber(text, out double value) ? value.ToString("R", CultureInfo.InvariantCulture) : text.Trim();
        }

        // ------------------------------------------------------------------ events (wired in SequencePanel.Designer.cs)

        private void buttonAdd_Click(object? sender, EventArgs e)
        {
            AddRow();
        }

        private void buttonSave_Click(object? sender, EventArgs e)
        {
            SaveToFile();
        }

        private void buttonLoad_Click(object? sender, EventArgs e)
        {
            LoadFromFile();
        }

        /// <summary> The empty row at the bottom shows the button symbols too, before it is a step. </summary>
        private void gridSequence_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex == gridSequence.NewRowIndex && gridSequence.Columns[e.ColumnIndex] is DataGridViewButtonColumn column)
            {
                e.Value = column.Text;
                e.FormattingApplied = true;
            }
        }

        private void gridSequence_DefaultValuesNeeded(object? sender, DataGridViewRowEventArgs e)
        {
            // the empty row at the bottom: typing a speed in it makes it a step with these
            var (speedUnit, duration, durationUnit) = NewRowValues();
            e.Row.Cells[columnSpeedUnit.Index].Value = speedUnit;
            e.Row.Cells[columnDuration.Index].Value = duration;
            e.Row.Cells[columnDurationUnit.Index].Value = durationUnit;
        }

        private void gridSequence_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || gridSequence.Rows[e.RowIndex].IsNewRow)
            {
                return;
            }
            if (e.ColumnIndex == columnDown.Index)
            {
                MoveRow(e.RowIndex, 1);
            }
            else if (e.ColumnIndex == columnUp.Index)
            {
                MoveRow(e.RowIndex, -1);
            }
            else if (e.ColumnIndex == columnRemove.Index)
            {
                gridSequence.Rows.RemoveAt(e.RowIndex);
            }
        }

        private void gridSequence_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            // one click opens a unit list
            if (e.RowIndex >= 0 && (e.ColumnIndex == columnSpeedUnit.Index || e.ColumnIndex == columnDurationUnit.Index))
            {
                gridSequence.CurrentCell = gridSequence[e.ColumnIndex, e.RowIndex];
                gridSequence.BeginEdit(true);
                if (gridSequence.EditingControl is ComboBox box)
                {
                    box.DroppedDown = true;
                }
            }
        }

        private void gridSequence_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            // a unit picked in a list counts at once, without leaving the cell
            if (gridSequence.IsCurrentCellDirty && gridSequence.CurrentCell is DataGridViewComboBoxCell)
            {
                gridSequence.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void gridSequence_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                RefreshRows();
            }
        }

        private void gridSequence_RowsChanged(object? sender, EventArgs e)
        {
            RefreshRows();
        }

        private void gridSequence_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false; // a unit that isn't in the list: the row is shaded instead
        }
    }
}
