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
            buttonOpen = new Button();
            groupBoxRuns = new GroupBox();
            flowRuns = new FlowLayoutPanel();
            buttonExport = new Button();
            toolTip = new ToolTip(components);
            layout.SuspendLayout();
            groupBoxRuns.SuspendLayout();
            SuspendLayout();
            // 
            // layout
            // 
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.Controls.Add(flowRuns, 0, 1);
            layout.Controls.Add(buttonOpen, 0, 0);
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
            // buttonOpen
            // 
            buttonOpen.AutoSize = true;
            buttonOpen.Dock = DockStyle.Fill;
            buttonOpen.Location = new Point(3, 3);
            buttonOpen.Name = "buttonOpen";
            buttonOpen.Size = new Size(132, 34);
            buttonOpen.TabIndex = 0;
            buttonOpen.Text = "Import Run";
            buttonOpen.UseVisualStyleBackColor = true;
            buttonOpen.Click += buttonOpen_Click;
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
            // flowRuns
            // 
            flowRuns.AutoScroll = true;
            layout.SetColumnSpan(flowRuns, 2);
            flowRuns.Dock = DockStyle.Fill;
            flowRuns.FlowDirection = FlowDirection.TopDown;
            flowRuns.Location = new Point(3, 43);
            flowRuns.Name = "flowRuns";
            flowRuns.Size = new Size(270, 337);
            flowRuns.TabIndex = 0;
            flowRuns.WrapContents = false;
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
            toolTip.SetToolTip(buttonExport, "One summary file with every run that is shown (the Show Speeds box ticked), one block per run");
            buttonExport.UseVisualStyleBackColor = true;
            buttonExport.Click += buttonExport_Click;
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
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel layout;
        private Button buttonOpen;
        private Button buttonExport;
        private GroupBox groupBoxRuns;
        private FlowLayoutPanel flowRuns;
        private ToolTip toolTip;
    }
}
