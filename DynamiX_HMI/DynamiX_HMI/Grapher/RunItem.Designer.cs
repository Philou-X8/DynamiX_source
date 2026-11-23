#nullable disable
namespace DynamiX_HMI.Grapher
{
    partial class RunItem
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
            TreeNode treeNode1 = new TreeNode("10 mm/s");
            TreeNode treeNode2 = new TreeNode("20 mm/s");
            TreeNode treeNode3 = new TreeNode("40 mm/s");
            TreeNode treeNode4 = new TreeNode("Show Speeds", new TreeNode[] { treeNode1, treeNode2, treeNode3 });
            buttonSaveSummary = new Button();
            buttonSavePoints = new Button();
            buttonRemove = new Button();
            treeSpeeds = new CheckTreeView();
            swatches = new ImageList(components);
            groupBoxRunItem = new GroupBox();
            menuRun = new ContextMenuStrip(components);
            menuShowAll = new ToolStripMenuItem();
            menuHideAll = new ToolStripMenuItem();
            menuDefaultColour = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            menuShowInFolder = new ToolStripMenuItem();
            menuDeleteFile = new ToolStripMenuItem();
            toolTip = new ToolTip(components);
            groupBoxRunItem.SuspendLayout();
            menuRun.SuspendLayout();
            SuspendLayout();
            // 
            // buttonSaveSummary
            // 
            buttonSaveSummary.Location = new Point(6, 22);
            buttonSaveSummary.Name = "buttonSaveSummary";
            buttonSaveSummary.Size = new Size(100, 23);
            buttonSaveSummary.TabIndex = 1;
            buttonSaveSummary.Text = "Save Summary";
            toolTip.SetToolTip(buttonSaveSummary, "Write the peak speeds and forces of this run (the shown speeds) to an Excel or CSV file");
            buttonSaveSummary.UseVisualStyleBackColor = true;
            buttonSaveSummary.Click += buttonSaveSummary_Click;
            // 
            // buttonSavePoints
            // 
            buttonSavePoints.Location = new Point(114, 22);
            buttonSavePoints.Name = "buttonSavePoints";
            buttonSavePoints.Size = new Size(100, 23);
            buttonSavePoints.TabIndex = 2;
            buttonSavePoints.Text = "Save Points";
            toolTip.SetToolTip(buttonSavePoints, "Write a copy of the raw data points of this run to a CSV file");
            buttonSavePoints.UseVisualStyleBackColor = true;
            buttonSavePoints.Click += buttonSavePoints_Click;
            // 
            // buttonRemove
            // 
            buttonRemove.Location = new Point(220, 22);
            buttonRemove.Name = "buttonRemove";
            buttonRemove.Size = new Size(25, 23);
            buttonRemove.TabIndex = 3;
            buttonRemove.Text = "X";
            toolTip.SetToolTip(buttonRemove, "Remove this run from the list (a saved file is kept: Refresh brings it back)");
            buttonRemove.UseVisualStyleBackColor = true;
            buttonRemove.Click += buttonRemove_Click;
            // 
            // treeSpeeds
            // 
            treeSpeeds.BorderStyle = BorderStyle.FixedSingle;
            treeSpeeds.CheckBoxes = true;
            treeSpeeds.HideSelection = false;
            treeSpeeds.ImageIndex = 0;
            treeSpeeds.ImageList = swatches;
            treeSpeeds.ItemHeight = 20;
            treeSpeeds.Location = new Point(6, 51);
            treeSpeeds.Name = "treeSpeeds";
            treeNode1.Name = "Node1";
            treeNode1.Text = "10 mm/s";
            treeNode2.Name = "Node2";
            treeNode2.Text = "20 mm/s";
            treeNode3.Name = "Node3";
            treeNode3.Text = "40 mm/s";
            treeNode4.Name = "Node0";
            treeNode4.Text = "Show Speeds";
            treeSpeeds.Nodes.AddRange(new TreeNode[] { treeNode4 });
            treeSpeeds.Scrollable = false;
            treeSpeeds.SelectedImageIndex = 0;
            treeSpeeds.ShowNodeToolTips = true;
            treeSpeeds.Size = new Size(239, 86);
            treeSpeeds.TabIndex = 4;
            treeSpeeds.AfterCheck += treeSpeeds_AfterCheck;
            treeSpeeds.AfterCollapse += treeSpeeds_AfterExpandCollapse;
            treeSpeeds.AfterExpand += treeSpeeds_AfterExpandCollapse;
            treeSpeeds.NodeMouseClick += treeSpeeds_NodeMouseClick;
            treeSpeeds.MouseClick += treeSpeeds_MouseClick;
            // 
            // swatches
            // 
            swatches.ColorDepth = ColorDepth.Depth32Bit;
            swatches.ImageSize = new Size(24, 14);
            swatches.TransparentColor = Color.Transparent;
            // 
            // groupBoxRunItem
            // 
            groupBoxRunItem.ContextMenuStrip = menuRun;
            groupBoxRunItem.Controls.Add(treeSpeeds);
            groupBoxRunItem.Controls.Add(buttonSavePoints);
            groupBoxRunItem.Controls.Add(buttonRemove);
            groupBoxRunItem.Controls.Add(buttonSaveSummary);
            groupBoxRunItem.Dock = DockStyle.Fill;
            groupBoxRunItem.Location = new Point(0, 0);
            groupBoxRunItem.Name = "groupBoxRunItem";
            groupBoxRunItem.Size = new Size(278, 155);
            groupBoxRunItem.TabIndex = 5;
            groupBoxRunItem.TabStop = false;
            groupBoxRunItem.Text = "261122_171635_Run_1";
            // 
            // menuRun
            // 
            menuRun.Items.AddRange(new ToolStripItem[] { menuShowAll, menuHideAll, menuDefaultColour, toolStripSeparator1, menuShowInFolder, menuDeleteFile });
            menuRun.Name = "menuRun";
            menuRun.Size = new Size(158, 120);
            menuRun.Closed += menuRun_Closed;
            // 
            // menuShowAll
            // 
            menuShowAll.Name = "menuShowAll";
            menuShowAll.Size = new Size(157, 22);
            menuShowAll.Text = "Show all speeds";
            menuShowAll.Click += menuShowAll_Click;
            // 
            // menuHideAll
            // 
            menuHideAll.Name = "menuHideAll";
            menuHideAll.Size = new Size(157, 22);
            menuHideAll.Text = "Hide all speeds";
            menuHideAll.Click += menuHideAll_Click;
            // 
            // menuDefaultColour
            // 
            menuDefaultColour.Name = "menuDefaultColour";
            menuDefaultColour.Size = new Size(157, 22);
            menuDefaultColour.Text = "Default colour";
            menuDefaultColour.Click += menuDefaultColour_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(154, 6);
            // 
            // menuShowInFolder
            // 
            menuShowInFolder.Name = "menuShowInFolder";
            menuShowInFolder.Size = new Size(157, 22);
            menuShowInFolder.Text = "Show in folder";
            menuShowInFolder.Click += menuShowInFolder_Click;
            // 
            // menuDeleteFile
            // 
            menuDeleteFile.Name = "menuDeleteFile";
            menuDeleteFile.Size = new Size(157, 22);
            menuDeleteFile.Text = "Delete file...";
            menuDeleteFile.Click += menuDeleteFile_Click;
            // 
            // RunItem
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(groupBoxRunItem);
            Name = "RunItem";
            Size = new Size(278, 155);
            groupBoxRunItem.ResumeLayout(false);
            menuRun.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private Button buttonSaveSummary;
        private Button buttonSavePoints;
        private Button buttonRemove;
        private CheckTreeView treeSpeeds;
        private ImageList swatches;
        private GroupBox groupBoxRunItem;
        private ContextMenuStrip menuRun;
        private ToolStripMenuItem menuShowAll;
        private ToolStripMenuItem menuHideAll;
        private ToolStripMenuItem menuDefaultColour;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem menuShowInFolder;
        private ToolStripMenuItem menuDeleteFile;
        private ToolTip toolTip;
    }
}
