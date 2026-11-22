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
            TreeNode treeNode1 = new TreeNode("[ ]  [ ]  10 mm/s");
            TreeNode treeNode2 = new TreeNode("[ ]  [ ]  20 mm/s");
            TreeNode treeNode3 = new TreeNode("[ ]  [ ]  40 mm/s");
            TreeNode treeNode4 = new TreeNode("[ ]  [ ]  Show Speeds", new TreeNode[] { treeNode1, treeNode2, treeNode3 });
            buttonSaveSummary = new Button();
            buttonSavePoints = new Button();
            buttonRemove = new Button();
            treeView1 = new TreeView();
            groupBoxRunItem = new GroupBox();
            groupBoxRunItem.SuspendLayout();
            SuspendLayout();
            // 
            // buttonSaveSummary
            // 
            buttonSaveSummary.Location = new Point(6, 22);
            buttonSaveSummary.Name = "buttonSaveSummary";
            buttonSaveSummary.Size = new Size(100, 23);
            buttonSaveSummary.TabIndex = 1;
            buttonSaveSummary.Text = "Save Summary";
            buttonSaveSummary.UseVisualStyleBackColor = true;
            // 
            // buttonSavePoints
            // 
            buttonSavePoints.Location = new Point(114, 22);
            buttonSavePoints.Name = "buttonSavePoints";
            buttonSavePoints.Size = new Size(100, 23);
            buttonSavePoints.TabIndex = 2;
            buttonSavePoints.Text = "Save Points";
            buttonSavePoints.UseVisualStyleBackColor = true;
            // 
            // buttonRemove
            // 
            buttonRemove.Location = new Point(220, 22);
            buttonRemove.Name = "buttonRemove";
            buttonRemove.Size = new Size(25, 23);
            buttonRemove.TabIndex = 3;
            buttonRemove.Text = "X";
            buttonRemove.UseVisualStyleBackColor = true;
            // 
            // treeView1
            // 
            treeView1.Location = new Point(6, 51);
            treeView1.Name = "treeView1";
            treeNode1.Name = "Node1";
            treeNode1.Text = "[ ]  [ ]  10 mm/s";
            treeNode2.Name = "Node2";
            treeNode2.Text = "[ ]  [ ]  20 mm/s";
            treeNode3.Name = "Node3";
            treeNode3.Text = "[ ]  [ ]  40 mm/s";
            treeNode4.Checked = true;
            treeNode4.Name = "Node0";
            treeNode4.Text = "[ ]  [ ]  Show Speeds";
            treeView1.Nodes.AddRange(new TreeNode[] { treeNode4 });
            treeView1.Size = new Size(239, 80);
            treeView1.TabIndex = 4;
            // 
            // groupBoxRunItem
            // 
            groupBoxRunItem.Controls.Add(treeView1);
            groupBoxRunItem.Controls.Add(buttonSavePoints);
            groupBoxRunItem.Controls.Add(buttonRemove);
            groupBoxRunItem.Controls.Add(buttonSaveSummary);
            groupBoxRunItem.Dock = DockStyle.Fill;
            groupBoxRunItem.Location = new Point(0, 0);
            groupBoxRunItem.Name = "groupBoxRunItem";
            groupBoxRunItem.Size = new Size(255, 142);
            groupBoxRunItem.TabIndex = 5;
            groupBoxRunItem.TabStop = false;
            groupBoxRunItem.Text = "261122_171635_Run_1";
            // 
            // RunItem
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(groupBoxRunItem);
            Name = "RunItem";
            Size = new Size(255, 142);
            groupBoxRunItem.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private Button buttonSaveSummary;
        private Button buttonSavePoints;
        private Button buttonRemove;
        private TreeView treeView1;
        private GroupBox groupBoxRunItem;
    }
}
