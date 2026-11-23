#nullable disable
namespace DynamiX_HMI.Grapher
{
    partial class RunPanel
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
            components = new System.ComponentModel.Container();
            layout = new TableLayoutPanel();
            treeRuns = new CheckTreeView();
            swatches = new ImageList(components);
            buttonImport = new Button();
            buttonExport = new Button();
            groupBoxRuns = new GroupBox();
            toolTip = new ToolTip(components);
            menuRun = new ContextMenuStrip(components);
            menuShowAll = new ToolStripMenuItem();
            menuHideAll = new ToolStripMenuItem();
            menuDefaultColour = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            menuSaveSummary = new ToolStripMenuItem();
            menuSavePoints = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            menuShowInFolder = new ToolStripMenuItem();
            menuDeleteFile = new ToolStripMenuItem();
            menuRemove = new ToolStripMenuItem();
            layout.SuspendLayout();
            groupBoxRuns.SuspendLayout();
            menuRun.SuspendLayout();
            SuspendLayout();
            //
            // layout
            //
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.Controls.Add(treeRuns, 0, 1);
            layout.Controls.Add(buttonImport, 0, 0);
            layout.Controls.Add(buttonExport, 1, 0);
            layout.Dock = DockStyle.Fill;
            layout.Location = new Point(4, 20);
            layout.Margin = new Padding(0);
            layout.Name = "layout";
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Size = new Size(276, 383);
            layout.TabIndex = 0;
            //
            // treeRuns
            //
            treeRuns.BorderStyle = BorderStyle.FixedSingle;
            treeRuns.CheckBoxes = true;
            layout.SetColumnSpan(treeRuns, 2);
            treeRuns.Dock = DockStyle.Fill;
            treeRuns.HideSelection = false;
            treeRuns.ImageIndex = 0;
            treeRuns.ImageList = swatches;
            treeRuns.ItemHeight = 20;
            treeRuns.Location = new Point(3, 43);
            treeRuns.Name = "treeRuns";
            treeRuns.SelectedImageIndex = 0;
            treeRuns.ShowNodeToolTips = true;
            treeRuns.Size = new Size(270, 337);
            treeRuns.TabIndex = 0;
            treeRuns.AfterCheck += treeRuns_AfterCheck;
            treeRuns.NodeMouseClick += treeRuns_NodeMouseClick;
            treeRuns.MouseClick += treeRuns_MouseClick;
            //
            // swatches
            //
            swatches.ColorDepth = ColorDepth.Depth32Bit;
            swatches.ImageSize = new Size(24, 14);
            swatches.TransparentColor = Color.Transparent;
            //
            // buttonImport
            //
            buttonImport.AutoSize = true;
            buttonImport.Dock = DockStyle.Fill;
            buttonImport.Location = new Point(3, 3);
            buttonImport.Name = "buttonImport";
            buttonImport.Size = new Size(132, 34);
            buttonImport.TabIndex = 0;
            buttonImport.Text = "Import Run";
            toolTip.SetToolTip(buttonImport, "Copy run files into the active project's folder and list them");
            buttonImport.UseVisualStyleBackColor = true;
            buttonImport.Click += buttonImport_Click;
            //
            // buttonExport
            //
            buttonExport.AutoSize = true;
            buttonExport.Dock = DockStyle.Fill;
            buttonExport.Location = new Point(141, 3);
            buttonExport.Name = "buttonExport";
            buttonExport.Size = new Size(132, 34);
            buttonExport.TabIndex = 1;
            buttonExport.Text = "Export Selected";
            toolTip.SetToolTip(buttonExport, "One summary file with every run that has a speed ticked, one block per run");
            buttonExport.UseVisualStyleBackColor = true;
            buttonExport.Click += buttonExport_Click;
            //
            // groupBoxRuns
            //
            groupBoxRuns.Controls.Add(layout);
            groupBoxRuns.Dock = DockStyle.Fill;
            groupBoxRuns.Location = new Point(0, 0);
            groupBoxRuns.Name = "groupBoxRuns";
            groupBoxRuns.Padding = new Padding(4);
            groupBoxRuns.Size = new Size(284, 407);
            groupBoxRuns.TabIndex = 1;
            groupBoxRuns.TabStop = false;
            groupBoxRuns.Text = "Runs";
            //
            // menuRun
            //
            menuRun.Items.AddRange(new ToolStripItem[] { menuShowAll, menuHideAll, menuDefaultColour, toolStripSeparator1, menuSaveSummary, menuSavePoints, toolStripSeparator2, menuShowInFolder, menuDeleteFile, menuRemove });
            menuRun.Name = "menuRun";
            menuRun.Size = new Size(181, 192);
            menuRun.Closed += menuRun_Closed;
            //
            // menuShowAll
            //
            menuShowAll.Name = "menuShowAll";
            menuShowAll.Size = new Size(180, 22);
            menuShowAll.Text = "Show all speeds";
            menuShowAll.Click += menuShowAll_Click;
            //
            // menuHideAll
            //
            menuHideAll.Name = "menuHideAll";
            menuHideAll.Size = new Size(180, 22);
            menuHideAll.Text = "Hide all speeds";
            menuHideAll.Click += menuHideAll_Click;
            //
            // menuDefaultColour
            //
            menuDefaultColour.Name = "menuDefaultColour";
            menuDefaultColour.Size = new Size(180, 22);
            menuDefaultColour.Text = "Default colour";
            menuDefaultColour.Click += menuDefaultColour_Click;
            //
            // toolStripSeparator1
            //
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(177, 6);
            //
            // menuSaveSummary
            //
            menuSaveSummary.Name = "menuSaveSummary";
            menuSaveSummary.Size = new Size(180, 22);
            menuSaveSummary.Text = "Save summary...";
            menuSaveSummary.ToolTipText = "Write the peak speeds and forces of this run (the ticked speeds) to an Excel or CSV file";
            menuSaveSummary.Click += menuSaveSummary_Click;
            //
            // menuSavePoints
            //
            menuSavePoints.Name = "menuSavePoints";
            menuSavePoints.Size = new Size(180, 22);
            menuSavePoints.Text = "Save points as...";
            menuSavePoints.ToolTipText = "Write a copy of the raw data points of this run to a CSV file";
            menuSavePoints.Click += menuSavePoints_Click;
            //
            // toolStripSeparator2
            //
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(177, 6);
            //
            // menuShowInFolder
            //
            menuShowInFolder.Name = "menuShowInFolder";
            menuShowInFolder.Size = new Size(180, 22);
            menuShowInFolder.Text = "Show in folder";
            menuShowInFolder.Click += menuShowInFolder_Click;
            //
            // menuDeleteFile
            //
            menuDeleteFile.Name = "menuDeleteFile";
            menuDeleteFile.Size = new Size(180, 22);
            menuDeleteFile.Text = "Delete file...";
            menuDeleteFile.Click += menuDeleteFile_Click;
            //
            // menuRemove
            //
            menuRemove.Name = "menuRemove";
            menuRemove.Size = new Size(180, 22);
            menuRemove.Text = "Remove from list";
            menuRemove.ToolTipText = "Take the run off the list (a saved file stays in the project folder; load the project again to bring it back)";
            menuRemove.Click += menuRemove_Click;
            //
            // RunPanel
            //
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(groupBoxRuns);
            Name = "RunPanel";
            Size = new Size(284, 407);
            layout.ResumeLayout(false);
            layout.PerformLayout();
            groupBoxRuns.ResumeLayout(false);
            menuRun.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel layout;
        private Button buttonImport;
        private Button buttonExport;
        private GroupBox groupBoxRuns;
        private CheckTreeView treeRuns;
        private ImageList swatches;
        private ToolTip toolTip;
        private ContextMenuStrip menuRun;
        private ToolStripMenuItem menuShowAll;
        private ToolStripMenuItem menuHideAll;
        private ToolStripMenuItem menuDefaultColour;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem menuSaveSummary;
        private ToolStripMenuItem menuSavePoints;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem menuShowInFolder;
        private ToolStripMenuItem menuDeleteFile;
        private ToolStripMenuItem menuRemove;
    }
}
