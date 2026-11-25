#nullable disable
namespace DynamiX_HMI.Grapher
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
            components = new System.ComponentModel.Container();
            tabs = new TabControl();
            pageLive = new TabPage();
            tableLayoutLive = new TableLayoutPanel();
            labelStats = new Label();
            plotLiveF = new PlotControl();
            plotLiveX = new PlotControl();
            pageForcePos = new TabPage();
            plotFx = new PlotControl();
            pageForceSpeed = new TabPage();
            plotFv = new PlotControl();
            pagePeak = new TabPage();
            plotPeak = new PlotControl();
            pageTime = new TabPage();
            tableLayoutTime = new TableLayoutPanel();
            plotTimeF = new PlotControl();
            plotTimeX = new PlotControl();
            plotTimeV = new PlotControl();
            pageDebug = new TabPage();
            labelDebug = new Label();
            liveTimer = new System.Windows.Forms.Timer(components);
            tabs.SuspendLayout();
            pageLive.SuspendLayout();
            tableLayoutLive.SuspendLayout();
            pageForcePos.SuspendLayout();
            pageForceSpeed.SuspendLayout();
            pagePeak.SuspendLayout();
            pageTime.SuspendLayout();
            tableLayoutTime.SuspendLayout();
            pageDebug.SuspendLayout();
            SuspendLayout();
            //
            // tabs
            //
            tabs.Controls.Add(pageLive);
            tabs.Controls.Add(pageForcePos);
            tabs.Controls.Add(pageForceSpeed);
            tabs.Controls.Add(pagePeak);
            tabs.Controls.Add(pageTime);
            tabs.Controls.Add(pageDebug);
            tabs.Dock = DockStyle.Fill;
            tabs.Location = new Point(0, 0);
            tabs.Name = "tabs";
            tabs.SelectedIndex = 0;
            tabs.Size = new Size(600, 450);
            tabs.TabIndex = 0;
            //
            // pageLive
            //
            pageLive.Controls.Add(tableLayoutLive);
            pageLive.Location = new Point(4, 24);
            pageLive.Name = "pageLive";
            pageLive.Size = new Size(592, 422);
            pageLive.TabIndex = 0;
            pageLive.Text = "Live";
            pageLive.UseVisualStyleBackColor = true;
            //
            // tableLayoutLive
            //
            tableLayoutLive.ColumnCount = 1;
            tableLayoutLive.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutLive.Controls.Add(labelStats, 0, 0);
            tableLayoutLive.Controls.Add(plotLiveF, 0, 1);
            tableLayoutLive.Controls.Add(plotLiveX, 0, 2);
            tableLayoutLive.Dock = DockStyle.Fill;
            tableLayoutLive.Location = new Point(0, 0);
            tableLayoutLive.Margin = new Padding(0);
            tableLayoutLive.Name = "tableLayoutLive";
            tableLayoutLive.RowCount = 3;
            tableLayoutLive.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            tableLayoutLive.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutLive.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutLive.Size = new Size(592, 422);
            tableLayoutLive.TabIndex = 0;
            //
            // labelStats
            //
            labelStats.Dock = DockStyle.Fill;
            labelStats.Location = new Point(0, 0);
            labelStats.Margin = new Padding(0);
            labelStats.Name = "labelStats";
            labelStats.Padding = new Padding(6, 0, 0, 0);
            labelStats.Size = new Size(592, 26);
            labelStats.TabIndex = 0;
            labelStats.Text = "Waiting for data...";
            labelStats.TextAlign = ContentAlignment.MiddleLeft;
            //
            // plotLiveF
            //
            plotLiveF.BackColor = Color.White;
            plotLiveF.Dock = DockStyle.Fill;
            plotLiveF.Location = new Point(3, 29);
            plotLiveF.Name = "plotLiveF";
            plotLiveF.Size = new Size(586, 187);
            plotLiveF.TabIndex = 1;
            //
            // plotLiveX
            //
            plotLiveX.BackColor = Color.White;
            plotLiveX.Dock = DockStyle.Fill;
            plotLiveX.Location = new Point(3, 222);
            plotLiveX.Name = "plotLiveX";
            plotLiveX.Size = new Size(586, 197);
            plotLiveX.TabIndex = 2;
            //
            // pageForcePos
            //
            pageForcePos.Controls.Add(plotFx);
            pageForcePos.Location = new Point(4, 24);
            pageForcePos.Name = "pageForcePos";
            pageForcePos.Size = new Size(592, 422);
            pageForcePos.TabIndex = 1;
            pageForcePos.Text = "Force/Pos";
            pageForcePos.UseVisualStyleBackColor = true;
            //
            // plotFx
            //
            plotFx.BackColor = Color.White;
            plotFx.Dock = DockStyle.Fill;
            plotFx.Location = new Point(0, 0);
            plotFx.Name = "plotFx";
            plotFx.Size = new Size(592, 422);
            plotFx.TabIndex = 0;
            //
            // pageForceSpeed
            //
            pageForceSpeed.Controls.Add(plotFv);
            pageForceSpeed.Location = new Point(4, 24);
            pageForceSpeed.Name = "pageForceSpeed";
            pageForceSpeed.Size = new Size(592, 422);
            pageForceSpeed.TabIndex = 2;
            pageForceSpeed.Text = "Force/Speed";
            pageForceSpeed.UseVisualStyleBackColor = true;
            //
            // plotFv
            //
            plotFv.BackColor = Color.White;
            plotFv.Dock = DockStyle.Fill;
            plotFv.Location = new Point(0, 0);
            plotFv.Name = "plotFv";
            plotFv.Size = new Size(592, 422);
            plotFv.TabIndex = 0;
            //
            // pagePeak
            //
            pagePeak.Controls.Add(plotPeak);
            pagePeak.Location = new Point(4, 24);
            pagePeak.Name = "pagePeak";
            pagePeak.Size = new Size(592, 422);
            pagePeak.TabIndex = 3;
            pagePeak.Text = "Peak";
            pagePeak.UseVisualStyleBackColor = true;
            //
            // plotPeak
            //
            plotPeak.BackColor = Color.White;
            plotPeak.Dock = DockStyle.Fill;
            plotPeak.Location = new Point(0, 0);
            plotPeak.Name = "plotPeak";
            plotPeak.Size = new Size(592, 422);
            plotPeak.TabIndex = 0;
            //
            // pageTime
            //
            pageTime.Controls.Add(tableLayoutTime);
            pageTime.Location = new Point(4, 24);
            pageTime.Name = "pageTime";
            pageTime.Size = new Size(592, 422);
            pageTime.TabIndex = 4;
            pageTime.Text = "Time";
            pageTime.UseVisualStyleBackColor = true;
            //
            // tableLayoutTime
            //
            tableLayoutTime.ColumnCount = 1;
            tableLayoutTime.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutTime.Controls.Add(plotTimeF, 0, 0);
            tableLayoutTime.Controls.Add(plotTimeX, 0, 1);
            tableLayoutTime.Controls.Add(plotTimeV, 0, 2);
            tableLayoutTime.Dock = DockStyle.Fill;
            tableLayoutTime.Location = new Point(0, 0);
            tableLayoutTime.Margin = new Padding(0);
            tableLayoutTime.Name = "tableLayoutTime";
            tableLayoutTime.RowCount = 3;
            tableLayoutTime.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            tableLayoutTime.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            tableLayoutTime.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            tableLayoutTime.Size = new Size(592, 422);
            tableLayoutTime.TabIndex = 0;
            //
            // plotTimeF
            //
            plotTimeF.BackColor = Color.White;
            plotTimeF.Dock = DockStyle.Fill;
            plotTimeF.Location = new Point(3, 3);
            plotTimeF.Name = "plotTimeF";
            plotTimeF.Size = new Size(586, 134);
            plotTimeF.TabIndex = 0;
            //
            // plotTimeX
            //
            plotTimeX.BackColor = Color.White;
            plotTimeX.Dock = DockStyle.Fill;
            plotTimeX.Location = new Point(3, 143);
            plotTimeX.Name = "plotTimeX";
            plotTimeX.Size = new Size(586, 134);
            plotTimeX.TabIndex = 1;
            //
            // plotTimeV
            //
            plotTimeV.BackColor = Color.White;
            plotTimeV.Dock = DockStyle.Fill;
            plotTimeV.Location = new Point(3, 283);
            plotTimeV.Name = "plotTimeV";
            plotTimeV.Size = new Size(586, 136);
            plotTimeV.TabIndex = 2;
            //
            // pageDebug
            //
            pageDebug.Controls.Add(labelDebug);
            pageDebug.Location = new Point(4, 24);
            pageDebug.Name = "pageDebug";
            pageDebug.Size = new Size(592, 422);
            pageDebug.TabIndex = 5;
            pageDebug.Text = "Debug";
            pageDebug.UseVisualStyleBackColor = true;
            //
            // labelDebug
            //
            labelDebug.Dock = DockStyle.Fill;
            labelDebug.Font = new Font("Consolas", 9F);
            labelDebug.Location = new Point(0, 0);
            labelDebug.Name = "labelDebug";
            labelDebug.Padding = new Padding(6);
            labelDebug.Size = new Size(592, 422);
            labelDebug.TabIndex = 0;
            //
            // liveTimer
            //
            liveTimer.Tick += liveTimer_Tick;
            //
            // GraphPanel
            //
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(tabs);
            Name = "GraphPanel";
            Size = new Size(600, 450);
            tabs.ResumeLayout(false);
            pageLive.ResumeLayout(false);
            tableLayoutLive.ResumeLayout(false);
            pageForcePos.ResumeLayout(false);
            pageForceSpeed.ResumeLayout(false);
            pagePeak.ResumeLayout(false);
            pageTime.ResumeLayout(false);
            tableLayoutTime.ResumeLayout(false);
            pageDebug.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabs;
        private TabPage pageLive;
        private TableLayoutPanel tableLayoutLive;
        private Label labelStats;
        private PlotControl plotLiveF;
        private PlotControl plotLiveX;
        private TabPage pageForcePos;
        private PlotControl plotFx;
        private TabPage pageForceSpeed;
        private PlotControl plotFv;
        private TabPage pagePeak;
        private PlotControl plotPeak;
        private TabPage pageTime;
        private TableLayoutPanel tableLayoutTime;
        private PlotControl plotTimeF;
        private PlotControl plotTimeX;
        private PlotControl plotTimeV;
        private TabPage pageDebug;
        private Label labelDebug;
        private System.Windows.Forms.Timer liveTimer;
    }
}
