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
            buttons = new FlowLayoutPanel();
            buttonOpen = new Button();
            buttonExport = new Button();
            buttonRefresh = new Button();
            groupBoxRuns = new GroupBox();
            flowRuns = new FlowLayoutPanel();
            toolTip = new ToolTip(components);
            layout.SuspendLayout();
            buttons.SuspendLayout();
            groupBoxRuns.SuspendLayout();
            SuspendLayout();
            //
            // layout
            //
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.Controls.Add(buttons, 0, 0);
            layout.Controls.Add(groupBoxRuns, 0, 1);
            layout.Dock = DockStyle.Fill;
            layout.Location = new Point(0, 0);
            layout.Margin = new Padding(0);
            layout.Name = "layout";
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle());
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Size = new Size(280, 400);
            layout.TabIndex = 0;
            //
            // buttons
            //
            buttons.AutoSize = true;
            buttons.Controls.Add(buttonOpen);
            buttons.Controls.Add(buttonExport);
            buttons.Controls.Add(buttonRefresh);
            buttons.Dock = DockStyle.Fill;
            buttons.Location = new Point(0, 0);
            buttons.Margin = new Padding(0);
            buttons.Name = "buttons";
            buttons.Size = new Size(280, 34);
            buttons.TabIndex = 0;
            //
            // buttonOpen
            //
            buttonOpen.AutoSize = true;
            buttonOpen.Location = new Point(2, 2);
            buttonOpen.Margin = new Padding(2);
            buttonOpen.Name = "buttonOpen";
            buttonOpen.Size = new Size(72, 30);
            buttonOpen.TabIndex = 0;
            buttonOpen.Text = "Open...";
            buttonOpen.UseVisualStyleBackColor = true;
            buttonOpen.Click += buttonOpen_Click;
            //
            // buttonExport
            //
            buttonExport.AutoSize = true;
            buttonExport.Location = new Point(78, 2);
            buttonExport.Margin = new Padding(2);
            buttonExport.Name = "buttonExport";
            buttonExport.Size = new Size(124, 30);
            buttonExport.TabIndex = 1;
            buttonExport.Text = "Export all summary...";
            toolTip.SetToolTip(buttonExport, "One summary file with every run that is shown (the Show Speeds box ticked), one block per run");
            buttonExport.UseVisualStyleBackColor = true;
            buttonExport.Click += buttonExport_Click;
            //
            // buttonRefresh
            //
            buttonRefresh.AutoSize = true;
            buttonRefresh.Location = new Point(206, 2);
            buttonRefresh.Margin = new Padding(2);
            buttonRefresh.Name = "buttonRefresh";
            buttonRefresh.Size = new Size(60, 30);
            buttonRefresh.TabIndex = 2;
            buttonRefresh.Text = "Refresh";
            toolTip.SetToolTip(buttonRefresh, "Read the runs folder again");
            buttonRefresh.UseVisualStyleBackColor = true;
            buttonRefresh.Click += buttonRefresh_Click;
            //
            // groupBoxRuns
            //
            groupBoxRuns.Controls.Add(flowRuns);
            groupBoxRuns.Dock = DockStyle.Fill;
            groupBoxRuns.Location = new Point(3, 37);
            groupBoxRuns.Name = "groupBoxRuns";
            groupBoxRuns.Padding = new Padding(4);
            groupBoxRuns.Size = new Size(274, 360);
            groupBoxRuns.TabIndex = 1;
            groupBoxRuns.TabStop = false;
            groupBoxRuns.Text = "Runs";
            //
            // flowRuns
            //
            flowRuns.AutoScroll = true;
            flowRuns.Dock = DockStyle.Fill;
            flowRuns.FlowDirection = FlowDirection.TopDown;
            flowRuns.Location = new Point(4, 20);
            flowRuns.Name = "flowRuns";
            flowRuns.Size = new Size(266, 336);
            flowRuns.TabIndex = 0;
            flowRuns.WrapContents = false;
            //
            // RunPanel
            //
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(layout);
            Name = "RunPanel";
            Size = new Size(280, 400);
            layout.ResumeLayout(false);
            layout.PerformLayout();
            buttons.ResumeLayout(false);
            buttons.PerformLayout();
            groupBoxRuns.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel layout;
        private FlowLayoutPanel buttons;
        private Button buttonOpen;
        private Button buttonExport;
        private Button buttonRefresh;
        private GroupBox groupBoxRuns;
        private FlowLayoutPanel flowRuns;
        private ToolTip toolTip;
    }
}
