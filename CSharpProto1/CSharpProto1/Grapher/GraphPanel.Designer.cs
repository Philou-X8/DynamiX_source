namespace CSharpProto1.Grapher
{
    partial class GraphPanel
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
            tabControl1 = new TabControl();
            tabPageRaw = new TabPage();
            panelGraphRaw = new Panel();
            tabPageForcePos = new TabPage();
            panelGraphForcePos = new Panel();
            tabPageForceSpd = new TabPage();
            panelGraphForceSpd = new Panel();
            tabPageDebug = new TabPage();
            labelDataDebug = new Label();
            labelRangeY = new Label();
            labelRangeX = new Label();
            label1 = new Label();
            label2 = new Label();
            labelSpdRangeY = new Label();
            labelSpdRangeX = new Label();
            tabControl1.SuspendLayout();
            tabPageRaw.SuspendLayout();
            tabPageForcePos.SuspendLayout();
            tabPageForceSpd.SuspendLayout();
            tabPageDebug.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPageRaw);
            tabControl1.Controls.Add(tabPageForcePos);
            tabControl1.Controls.Add(tabPageForceSpd);
            tabControl1.Controls.Add(tabPageDebug);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(600, 600);
            tabControl1.TabIndex = 0;
            // 
            // tabPageRaw
            // 
            tabPageRaw.Controls.Add(label2);
            tabPageRaw.Controls.Add(label1);
            tabPageRaw.Controls.Add(panelGraphRaw);
            tabPageRaw.Location = new Point(4, 24);
            tabPageRaw.Name = "tabPageRaw";
            tabPageRaw.Padding = new Padding(3);
            tabPageRaw.Size = new Size(592, 572);
            tabPageRaw.TabIndex = 0;
            tabPageRaw.Text = "raw view";
            tabPageRaw.UseVisualStyleBackColor = true;
            // 
            // panelGraphRaw
            // 
            panelGraphRaw.BorderStyle = BorderStyle.Fixed3D;
            panelGraphRaw.Location = new Point(34, 36);
            panelGraphRaw.Name = "panelGraphRaw";
            panelGraphRaw.Size = new Size(512, 512);
            panelGraphRaw.TabIndex = 0;
            // 
            // tabPageForcePos
            // 
            tabPageForcePos.Controls.Add(labelRangeX);
            tabPageForcePos.Controls.Add(labelRangeY);
            tabPageForcePos.Controls.Add(panelGraphForcePos);
            tabPageForcePos.Location = new Point(4, 24);
            tabPageForcePos.Name = "tabPageForcePos";
            tabPageForcePos.Padding = new Padding(3);
            tabPageForcePos.Size = new Size(592, 572);
            tabPageForcePos.TabIndex = 1;
            tabPageForcePos.Text = "Force/Pos";
            tabPageForcePos.UseVisualStyleBackColor = true;
            // 
            // panelGraphForcePos
            // 
            panelGraphForcePos.BorderStyle = BorderStyle.Fixed3D;
            panelGraphForcePos.Location = new Point(40, 30);
            panelGraphForcePos.Name = "panelGraphForcePos";
            panelGraphForcePos.Size = new Size(512, 512);
            panelGraphForcePos.TabIndex = 1;
            // 
            // tabPageForceSpd
            // 
            tabPageForceSpd.Controls.Add(labelSpdRangeX);
            tabPageForceSpd.Controls.Add(labelSpdRangeY);
            tabPageForceSpd.Controls.Add(panelGraphForceSpd);
            tabPageForceSpd.Location = new Point(4, 24);
            tabPageForceSpd.Name = "tabPageForceSpd";
            tabPageForceSpd.Size = new Size(592, 572);
            tabPageForceSpd.TabIndex = 2;
            tabPageForceSpd.Text = "Force/Spd";
            tabPageForceSpd.UseVisualStyleBackColor = true;
            // 
            // panelGraphForceSpd
            // 
            panelGraphForceSpd.BorderStyle = BorderStyle.Fixed3D;
            panelGraphForceSpd.Location = new Point(40, 30);
            panelGraphForceSpd.Name = "panelGraphForceSpd";
            panelGraphForceSpd.Size = new Size(512, 512);
            panelGraphForceSpd.TabIndex = 1;
            // 
            // labelSpdRangeY
            // 
            labelSpdRangeY.AutoSize = true;
            labelSpdRangeY.Location = new Point(40, 12);
            labelSpdRangeY.Name = "labelSpdRangeY";
            labelSpdRangeY.Size = new Size(134, 15);
            labelSpdRangeY.TabIndex = 2;
            labelSpdRangeY.Text = "Force min and max: ? , ?";
            // 
            // labelSpdRangeX
            // 
            labelSpdRangeX.AutoSize = true;
            labelSpdRangeX.Location = new Point(274, 12);
            labelSpdRangeX.Name = "labelSpdRangeX";
            labelSpdRangeX.Size = new Size(134, 15);
            labelSpdRangeX.TabIndex = 3;
            labelSpdRangeX.Text = "Speed min and max: ? , ?";
            // 
            // tabPageDebug
            // 
            tabPageDebug.Controls.Add(labelDataDebug);
            tabPageDebug.Location = new Point(4, 24);
            tabPageDebug.Name = "tabPageDebug";
            tabPageDebug.Padding = new Padding(3);
            tabPageDebug.Size = new Size(592, 572);
            tabPageDebug.TabIndex = 3;
            tabPageDebug.Text = "tab debug";
            tabPageDebug.UseVisualStyleBackColor = true;
            // 
            // labelDataDebug
            // 
            labelDataDebug.AutoSize = true;
            labelDataDebug.Location = new Point(46, 33);
            labelDataDebug.Name = "labelDataDebug";
            labelDataDebug.Size = new Size(67, 15);
            labelDataDebug.TabIndex = 0;
            labelDataDebug.Text = "data debug";
            // 
            // labelRangeY
            // 
            labelRangeY.AutoSize = true;
            labelRangeY.Location = new Point(40, 12);
            labelRangeY.Name = "labelRangeY";
            labelRangeY.Size = new Size(134, 15);
            labelRangeY.TabIndex = 2;
            labelRangeY.Text = "Force min and max: ? , ?";
            // 
            // labelRangeX
            // 
            labelRangeX.AutoSize = true;
            labelRangeX.Location = new Point(274, 12);
            labelRangeX.Name = "labelRangeX";
            labelRangeX.Size = new Size(148, 15);
            labelRangeX.TabIndex = 3;
            labelRangeX.Text = "Position min and max: ? , ?";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(34, 18);
            label1.Name = "label1";
            label1.Size = new Size(134, 15);
            label1.TabIndex = 3;
            label1.Text = "Force min and max: ? , ?";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(284, 18);
            label2.Name = "label2";
            label2.Size = new Size(148, 15);
            label2.TabIndex = 4;
            label2.Text = "Position min and max: ? , ?";
            // 
            // GraphPanel
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tabControl1);
            Name = "GraphPanel";
            Size = new Size(600, 600);
            tabControl1.ResumeLayout(false);
            tabPageRaw.ResumeLayout(false);
            tabPageRaw.PerformLayout();
            tabPageForcePos.ResumeLayout(false);
            tabPageForcePos.PerformLayout();
            tabPageForceSpd.ResumeLayout(false);
            tabPageForceSpd.PerformLayout();
            tabPageDebug.ResumeLayout(false);
            tabPageDebug.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl1;
        private TabPage tabPageRaw;
        private TabPage tabPageForcePos;
        private TabPage tabPageForceSpd;
        private Panel panelGraphRaw;
        private TabPage tabPageDebug;
        private Panel panelGraphForcePos;
        private Label labelDataDebug;
        private Panel panelGraphForceSpd;
        private Label labelRangeX;
        private Label labelRangeY;
        private Label labelSpdRangeX;
        private Label labelSpdRangeY;
        private Label label2;
        private Label label1;
    }
}
