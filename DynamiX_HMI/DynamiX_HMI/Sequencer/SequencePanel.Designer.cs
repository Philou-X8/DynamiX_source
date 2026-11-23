#nullable disable
namespace DynamiX_HMI.Sequencer
{
    partial class SequencePanel
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            gridSequence = new DataGridView();
            columnDown = new DataGridViewButtonColumn();
            columnUp = new DataGridViewButtonColumn();
            columnSpeed = new DataGridViewTextBoxColumn();
            columnSpeedUnit = new DataGridViewComboBoxColumn();
            columnDuration = new DataGridViewTextBoxColumn();
            columnDurationUnit = new DataGridViewComboBoxColumn();
            columnRemove = new DataGridViewButtonColumn();
            tableLayoutPanel1 = new TableLayoutPanel();
            buttonAdd = new Button();
            buttonLoad = new Button();
            buttonSave = new Button();
            labelRunName = new Label();
            textBoxRunName = new TextBox();
            ((System.ComponentModel.ISupportInitialize)gridSequence).BeginInit();
            tableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // gridSequence
            // 
            gridSequence.AllowUserToDeleteRows = false;
            gridSequence.AllowUserToResizeRows = false;
            gridSequence.BackgroundColor = SystemColors.Control;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowFrame;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            gridSequence.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            gridSequence.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridSequence.Columns.AddRange(new DataGridViewColumn[] { columnDown, columnUp, columnSpeed, columnSpeedUnit, columnDuration, columnDurationUnit, columnRemove });
            tableLayoutPanel1.SetColumnSpan(gridSequence, 3);
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.WindowFrame;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            gridSequence.DefaultCellStyle = dataGridViewCellStyle2;
            gridSequence.Dock = DockStyle.Fill;
            gridSequence.EditMode = DataGridViewEditMode.EditOnEnter;
            gridSequence.Location = new Point(3, 43);
            gridSequence.MultiSelect = false;
            gridSequence.Name = "gridSequence";
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = SystemColors.Control;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle3.ForeColor = SystemColors.WindowFrame;
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            gridSequence.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            gridSequence.RowHeadersVisible = false;
            gridSequence.SelectionMode = DataGridViewSelectionMode.CellSelect;
            gridSequence.Size = new Size(314, 134);
            gridSequence.TabIndex = 1;
            gridSequence.CellClick += gridSequence_CellClick;
            gridSequence.CellContentClick += gridSequence_CellContentClick;
            gridSequence.CellFormatting += gridSequence_CellFormatting;
            gridSequence.CellValueChanged += gridSequence_CellValueChanged;
            gridSequence.CurrentCellDirtyStateChanged += gridSequence_CurrentCellDirtyStateChanged;
            gridSequence.DataError += gridSequence_DataError;
            gridSequence.DefaultValuesNeeded += gridSequence_DefaultValuesNeeded;
            gridSequence.RowsAdded += gridSequence_RowsChanged;
            gridSequence.RowsRemoved += gridSequence_RowsChanged;
            // 
            // columnDown
            // 
            columnDown.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            columnDown.HeaderText = "";
            columnDown.MinimumWidth = 24;
            columnDown.Name = "columnDown";
            columnDown.Resizable = DataGridViewTriState.False;
            columnDown.Text = "↓";
            columnDown.ToolTipText = "Move this step down";
            columnDown.UseColumnTextForButtonValue = true;
            columnDown.Width = 26;
            // 
            // columnUp
            // 
            columnUp.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            columnUp.HeaderText = "";
            columnUp.MinimumWidth = 24;
            columnUp.Name = "columnUp";
            columnUp.Resizable = DataGridViewTriState.False;
            columnUp.Text = "↑";
            columnUp.ToolTipText = "Move this step up";
            columnUp.UseColumnTextForButtonValue = true;
            columnUp.Width = 26;
            // 
            // columnSpeed
            // 
            columnSpeed.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            columnSpeed.FillWeight = 30F;
            columnSpeed.HeaderText = "Speed";
            columnSpeed.MinimumWidth = 46;
            columnSpeed.Name = "columnSpeed";
            columnSpeed.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // columnSpeedUnit
            // 
            columnSpeedUnit.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            columnSpeedUnit.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
            columnSpeedUnit.DisplayStyleForCurrentCellOnly = true;
            columnSpeedUnit.FillWeight = 36F;
            columnSpeedUnit.FlatStyle = FlatStyle.Flat;
            columnSpeedUnit.HeaderText = "";
            columnSpeedUnit.Items.AddRange(new object[] { "mm/s", "m/s", "drive Hz", "yoke RPM" });
            columnSpeedUnit.MinimumWidth = 66;
            columnSpeedUnit.Name = "columnSpeedUnit";
            // 
            // columnDuration
            // 
            columnDuration.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            columnDuration.FillWeight = 24F;
            columnDuration.HeaderText = "Duration";
            columnDuration.MinimumWidth = 40;
            columnDuration.Name = "columnDuration";
            columnDuration.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // columnDurationUnit
            // 
            columnDurationUnit.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            columnDurationUnit.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
            columnDurationUnit.DisplayStyleForCurrentCellOnly = true;
            columnDurationUnit.FillWeight = 30F;
            columnDurationUnit.FlatStyle = FlatStyle.Flat;
            columnDurationUnit.HeaderText = "";
            columnDurationUnit.Items.AddRange(new object[] { "cycles", "seconds" });
            columnDurationUnit.MinimumWidth = 62;
            columnDurationUnit.Name = "columnDurationUnit";
            // 
            // columnRemove
            // 
            columnRemove.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            columnRemove.HeaderText = "";
            columnRemove.MinimumWidth = 24;
            columnRemove.Name = "columnRemove";
            columnRemove.Resizable = DataGridViewTriState.False;
            columnRemove.Text = "X";
            columnRemove.ToolTipText = "Remove this step";
            columnRemove.UseColumnTextForButtonValue = true;
            columnRemove.Width = 26;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.Controls.Add(buttonAdd, 0, 2);
            tableLayoutPanel1.Controls.Add(gridSequence, 0, 1);
            tableLayoutPanel1.Controls.Add(buttonLoad, 1, 2);
            tableLayoutPanel1.Controls.Add(buttonSave, 2, 2);
            tableLayoutPanel1.Controls.Add(labelRunName, 0, 0);
            tableLayoutPanel1.Controls.Add(textBoxRunName, 1, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            tableLayoutPanel1.Size = new Size(320, 220);
            tableLayoutPanel1.TabIndex = 2;
            // 
            // buttonAdd
            // 
            buttonAdd.Dock = DockStyle.Fill;
            buttonAdd.Location = new Point(3, 183);
            buttonAdd.Name = "buttonAdd";
            buttonAdd.Size = new Size(100, 34);
            buttonAdd.TabIndex = 3;
            buttonAdd.Text = "Add";
            buttonAdd.UseVisualStyleBackColor = true;
            // 
            // buttonLoad
            // 
            buttonLoad.Dock = DockStyle.Fill;
            buttonLoad.Location = new Point(109, 183);
            buttonLoad.Name = "buttonLoad";
            buttonLoad.Size = new Size(100, 34);
            buttonLoad.TabIndex = 1;
            buttonLoad.Text = "Load";
            buttonLoad.UseVisualStyleBackColor = true;
            buttonLoad.Click += buttonLoad_Click;
            // 
            // buttonSave
            // 
            buttonSave.Dock = DockStyle.Fill;
            buttonSave.Location = new Point(215, 183);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(102, 34);
            buttonSave.TabIndex = 2;
            buttonSave.Text = "Save";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += buttonSave_Click;
            // 
            // labelRunName
            // 
            labelRunName.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            labelRunName.AutoSize = true;
            labelRunName.Location = new Point(31, 3);
            labelRunName.Margin = new Padding(3);
            labelRunName.Name = "labelRunName";
            labelRunName.Padding = new Padding(3);
            labelRunName.Size = new Size(72, 21);
            labelRunName.TabIndex = 4;
            labelRunName.Text = "Run Name:";
            // 
            // textBoxRunName
            // 
            tableLayoutPanel1.SetColumnSpan(textBoxRunName, 2);
            textBoxRunName.Dock = DockStyle.Fill;
            textBoxRunName.Location = new Point(109, 3);
            textBoxRunName.Name = "textBoxRunName";
            textBoxRunName.Size = new Size(208, 23);
            textBoxRunName.TabIndex = 5;
            // 
            // SequencePanel
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tableLayoutPanel1);
            Name = "SequencePanel";
            Size = new Size(320, 220);
            ((System.ComponentModel.ISupportInitialize)gridSequence).EndInit();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private DataGridView gridSequence;
        private DataGridViewButtonColumn columnDown;
        private DataGridViewButtonColumn columnUp;
        private DataGridViewTextBoxColumn columnSpeed;
        private DataGridViewComboBoxColumn columnSpeedUnit;
        private DataGridViewTextBoxColumn columnDuration;
        private DataGridViewComboBoxColumn columnDurationUnit;
        private DataGridViewButtonColumn columnRemove;
        private TableLayoutPanel tableLayoutPanel1;
        private Button buttonLoad;
        private Button buttonSave;
        private Button buttonAdd;
        private Label labelRunName;
        private TextBox textBoxRunName;
    }
}
