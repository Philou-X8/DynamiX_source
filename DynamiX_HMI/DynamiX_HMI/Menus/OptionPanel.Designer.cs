#nullable disable
namespace DynamiX_HMI.Menus
{
    partial class OptionPanel
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
            layoutMain = new TableLayoutPanel();
            tabs = new TabControl();
            pageSettings = new TabPage();
            grid = new PropertyGrid();
            pageCalibration = new TabPage();
            flowCalibration = new FlowLayoutPanel();
            groupLive = new GroupBox();
            stackLive = new FlowLayoutPanel();
            labelForce = new Label();
            labelPosition = new Label();
            groupForce = new GroupBox();
            stackForce = new FlowLayoutPanel();
            labelHintZero = new Label();
            buttonZeroForce = new Button();
            labelHintGain = new Label();
            rowKnownForce = new FlowLayoutPanel();
            labelKnownForce = new Label();
            textKnownForce = new TextBox();
            buttonSetGain = new Button();
            groupPosition = new GroupBox();
            stackPosition = new FlowLayoutPanel();
            labelHintPosition = new Label();
            rowPositionA = new FlowLayoutPanel();
            labelPositionA = new Label();
            textPositionA = new TextBox();
            buttonCaptureA = new Button();
            rowPositionB = new FlowLayoutPanel();
            labelPositionB = new Label();
            textPositionB = new TextBox();
            buttonCaptureB = new Button();
            labelCaptured = new Label();
            pageMachine = new TabPage();
            flowMachine = new FlowLayoutPanel();
            groupEsp = new GroupBox();
            stackEsp = new FlowLayoutPanel();
            buttonSendLimit = new Button();
            buttonReboot = new Button();
            groupFiles = new GroupBox();
            stackFiles = new FlowLayoutPanel();
            buttonOpenFolder = new Button();
            buttonReloadSettings = new Button();
            buttonResetCalibration = new Button();
            labelStatus = new Label();
            layoutMain.SuspendLayout();
            tabs.SuspendLayout();
            pageSettings.SuspendLayout();
            pageCalibration.SuspendLayout();
            flowCalibration.SuspendLayout();
            groupLive.SuspendLayout();
            stackLive.SuspendLayout();
            groupForce.SuspendLayout();
            stackForce.SuspendLayout();
            rowKnownForce.SuspendLayout();
            groupPosition.SuspendLayout();
            stackPosition.SuspendLayout();
            rowPositionA.SuspendLayout();
            rowPositionB.SuspendLayout();
            pageMachine.SuspendLayout();
            flowMachine.SuspendLayout();
            groupEsp.SuspendLayout();
            stackEsp.SuspendLayout();
            groupFiles.SuspendLayout();
            stackFiles.SuspendLayout();
            SuspendLayout();
            //
            // layoutMain
            //
            layoutMain.ColumnCount = 1;
            layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutMain.Controls.Add(tabs, 0, 0);
            layoutMain.Controls.Add(labelStatus, 0, 1);
            layoutMain.Dock = DockStyle.Fill;
            layoutMain.Location = new Point(0, 0);
            layoutMain.Margin = new Padding(0);
            layoutMain.Name = "layoutMain";
            layoutMain.RowCount = 2;
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layoutMain.Size = new Size(520, 480);
            layoutMain.TabIndex = 0;
            //
            // tabs
            //
            tabs.Controls.Add(pageSettings);
            tabs.Controls.Add(pageCalibration);
            tabs.Controls.Add(pageMachine);
            tabs.Dock = DockStyle.Fill;
            tabs.Location = new Point(3, 3);
            tabs.Name = "tabs";
            tabs.SelectedIndex = 0;
            tabs.Size = new Size(514, 430);
            tabs.TabIndex = 0;
            //
            // pageSettings
            //
            pageSettings.Controls.Add(grid);
            pageSettings.Location = new Point(4, 24);
            pageSettings.Name = "pageSettings";
            pageSettings.Size = new Size(506, 402);
            pageSettings.TabIndex = 0;
            pageSettings.Text = "Settings";
            pageSettings.UseVisualStyleBackColor = true;
            //
            // grid
            //
            grid.Dock = DockStyle.Fill;
            grid.Location = new Point(0, 0);
            grid.Name = "grid";
            grid.PropertySort = PropertySort.Categorized;
            grid.Size = new Size(506, 402);
            grid.TabIndex = 0;
            grid.ToolbarVisible = false;
            grid.PropertyValueChanged += grid_PropertyValueChanged;
            //
            // pageCalibration
            //
            pageCalibration.Controls.Add(flowCalibration);
            pageCalibration.Location = new Point(4, 24);
            pageCalibration.Name = "pageCalibration";
            pageCalibration.Size = new Size(506, 402);
            pageCalibration.TabIndex = 1;
            pageCalibration.Text = "Sensor calibration";
            pageCalibration.UseVisualStyleBackColor = true;
            //
            // flowCalibration
            //
            flowCalibration.AutoScroll = true;
            flowCalibration.Controls.Add(groupLive);
            flowCalibration.Controls.Add(groupForce);
            flowCalibration.Controls.Add(groupPosition);
            flowCalibration.Dock = DockStyle.Fill;
            flowCalibration.FlowDirection = FlowDirection.TopDown;
            flowCalibration.Location = new Point(0, 0);
            flowCalibration.Name = "flowCalibration";
            flowCalibration.Size = new Size(506, 402);
            flowCalibration.TabIndex = 0;
            flowCalibration.WrapContents = false;
            //
            // groupLive
            //
            groupLive.AutoSize = true;
            groupLive.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupLive.Controls.Add(stackLive);
            groupLive.Location = new Point(3, 3);
            groupLive.Name = "groupLive";
            groupLive.Padding = new Padding(6);
            groupLive.Size = new Size(330, 70);
            groupLive.TabIndex = 0;
            groupLive.TabStop = false;
            groupLive.Text = "Live readings";
            //
            // stackLive
            //
            stackLive.AutoSize = true;
            stackLive.Controls.Add(labelForce);
            stackLive.Controls.Add(labelPosition);
            stackLive.Dock = DockStyle.Fill;
            stackLive.FlowDirection = FlowDirection.TopDown;
            stackLive.Location = new Point(6, 22);
            stackLive.Name = "stackLive";
            stackLive.Size = new Size(318, 42);
            stackLive.TabIndex = 0;
            stackLive.WrapContents = false;
            //
            // labelForce
            //
            labelForce.AutoSize = true;
            labelForce.Location = new Point(3, 0);
            labelForce.Name = "labelForce";
            labelForce.Size = new Size(131, 15);
            labelForce.TabIndex = 0;
            labelForce.Text = "Force: no reading yet";
            //
            // labelPosition
            //
            labelPosition.AutoSize = true;
            labelPosition.Location = new Point(3, 15);
            labelPosition.Name = "labelPosition";
            labelPosition.Size = new Size(131, 15);
            labelPosition.TabIndex = 1;
            labelPosition.Text = "Position: no reading yet";
            //
            // groupForce
            //
            groupForce.AutoSize = true;
            groupForce.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupForce.Controls.Add(stackForce);
            groupForce.Location = new Point(3, 79);
            groupForce.Name = "groupForce";
            groupForce.Padding = new Padding(6);
            groupForce.Size = new Size(330, 170);
            groupForce.TabIndex = 1;
            groupForce.TabStop = false;
            groupForce.Text = "Force calibration";
            //
            // stackForce
            //
            stackForce.AutoSize = true;
            stackForce.Controls.Add(labelHintZero);
            stackForce.Controls.Add(buttonZeroForce);
            stackForce.Controls.Add(labelHintGain);
            stackForce.Controls.Add(rowKnownForce);
            stackForce.Controls.Add(buttonSetGain);
            stackForce.Dock = DockStyle.Fill;
            stackForce.FlowDirection = FlowDirection.TopDown;
            stackForce.Location = new Point(6, 22);
            stackForce.Name = "stackForce";
            stackForce.Size = new Size(318, 142);
            stackForce.TabIndex = 0;
            stackForce.WrapContents = false;
            //
            // labelHintZero
            //
            labelHintZero.AutoSize = true;
            labelHintZero.ForeColor = SystemColors.GrayText;
            labelHintZero.Location = new Point(3, 0);
            labelHintZero.MaximumSize = new Size(310, 0);
            labelHintZero.Name = "labelHintZero";
            labelHintZero.Size = new Size(263, 15);
            labelHintZero.TabIndex = 0;
            labelHintZero.Text = "1. With NOTHING on the load cell: press Zero.";
            //
            // buttonZeroForce
            //
            buttonZeroForce.AutoSize = true;
            buttonZeroForce.Location = new Point(5, 18);
            buttonZeroForce.Margin = new Padding(2, 3, 2, 3);
            buttonZeroForce.Name = "buttonZeroForce";
            buttonZeroForce.Size = new Size(215, 25);
            buttonZeroForce.TabIndex = 1;
            buttonZeroForce.Text = "Zero force (nothing on the load cell)";
            buttonZeroForce.UseVisualStyleBackColor = true;
            buttonZeroForce.Click += buttonZeroForce_Click;
            //
            // labelHintGain
            //
            labelHintGain.AutoSize = true;
            labelHintGain.ForeColor = SystemColors.GrayText;
            labelHintGain.Location = new Point(3, 46);
            labelHintGain.MaximumSize = new Size(310, 0);
            labelHintGain.Name = "labelHintGain";
            labelHintGain.Size = new Size(263, 15);
            labelHintGain.TabIndex = 2;
            labelHintGain.Text = "2. Put a known weight or force on it, enter it:";
            //
            // rowKnownForce
            //
            rowKnownForce.AutoSize = true;
            rowKnownForce.Controls.Add(labelKnownForce);
            rowKnownForce.Controls.Add(textKnownForce);
            rowKnownForce.Location = new Point(3, 64);
            rowKnownForce.Name = "rowKnownForce";
            rowKnownForce.Size = new Size(190, 29);
            rowKnownForce.TabIndex = 3;
            rowKnownForce.WrapContents = false;
            //
            // labelKnownForce
            //
            labelKnownForce.Anchor = AnchorStyles.Left;
            labelKnownForce.AutoSize = true;
            labelKnownForce.Location = new Point(3, 7);
            labelKnownForce.Name = "labelKnownForce";
            labelKnownForce.Size = new Size(97, 15);
            labelKnownForce.TabIndex = 0;
            labelKnownForce.Text = "Known force (N)";
            //
            // textKnownForce
            //
            textKnownForce.Location = new Point(106, 3);
            textKnownForce.Name = "textKnownForce";
            textKnownForce.Size = new Size(70, 23);
            textKnownForce.TabIndex = 1;
            textKnownForce.Text = "100";
            //
            // buttonSetGain
            //
            buttonSetGain.AutoSize = true;
            buttonSetGain.Location = new Point(5, 99);
            buttonSetGain.Margin = new Padding(2, 3, 2, 3);
            buttonSetGain.Name = "buttonSetGain";
            buttonSetGain.Size = new Size(185, 25);
            buttonSetGain.TabIndex = 4;
            buttonSetGain.Text = "Set gain from the known force";
            buttonSetGain.UseVisualStyleBackColor = true;
            buttonSetGain.Click += buttonSetGain_Click;
            //
            // groupPosition
            //
            groupPosition.AutoSize = true;
            groupPosition.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupPosition.Controls.Add(stackPosition);
            groupPosition.Location = new Point(3, 255);
            groupPosition.Name = "groupPosition";
            groupPosition.Padding = new Padding(6);
            groupPosition.Size = new Size(330, 190);
            groupPosition.TabIndex = 2;
            groupPosition.TabStop = false;
            groupPosition.Text = "Position calibration";
            //
            // stackPosition
            //
            stackPosition.AutoSize = true;
            stackPosition.Controls.Add(labelHintPosition);
            stackPosition.Controls.Add(rowPositionA);
            stackPosition.Controls.Add(rowPositionB);
            stackPosition.Controls.Add(labelCaptured);
            stackPosition.Dock = DockStyle.Fill;
            stackPosition.FlowDirection = FlowDirection.TopDown;
            stackPosition.Location = new Point(6, 22);
            stackPosition.Name = "stackPosition";
            stackPosition.Size = new Size(318, 162);
            stackPosition.TabIndex = 0;
            stackPosition.WrapContents = false;
            //
            // labelHintPosition
            //
            labelHintPosition.AutoSize = true;
            labelHintPosition.ForeColor = SystemColors.GrayText;
            labelHintPosition.Location = new Point(3, 0);
            labelHintPosition.MaximumSize = new Size(310, 0);
            labelHintPosition.Name = "labelHintPosition";
            labelHintPosition.Size = new Size(307, 45);
            labelHintPosition.TabIndex = 0;
            labelHintPosition.Text = "Move the stroke to two known positions, enter each position, capture it. The scale is computed once both are captured.";
            //
            // rowPositionA
            //
            rowPositionA.AutoSize = true;
            rowPositionA.Controls.Add(labelPositionA);
            rowPositionA.Controls.Add(textPositionA);
            rowPositionA.Controls.Add(buttonCaptureA);
            rowPositionA.Location = new Point(3, 48);
            rowPositionA.Name = "rowPositionA";
            rowPositionA.Size = new Size(260, 35);
            rowPositionA.TabIndex = 1;
            rowPositionA.WrapContents = false;
            //
            // labelPositionA
            //
            labelPositionA.Anchor = AnchorStyles.Left;
            labelPositionA.AutoSize = true;
            labelPositionA.Location = new Point(3, 10);
            labelPositionA.Name = "labelPositionA";
            labelPositionA.Size = new Size(97, 15);
            labelPositionA.TabIndex = 0;
            labelPositionA.Text = "Position A (mm)";
            //
            // textPositionA
            //
            textPositionA.Location = new Point(106, 6);
            textPositionA.Name = "textPositionA";
            textPositionA.Size = new Size(70, 23);
            textPositionA.TabIndex = 1;
            textPositionA.Text = "0";
            //
            // buttonCaptureA
            //
            buttonCaptureA.AutoSize = true;
            buttonCaptureA.Location = new Point(184, 3);
            buttonCaptureA.Margin = new Padding(2, 3, 2, 3);
            buttonCaptureA.Name = "buttonCaptureA";
            buttonCaptureA.Size = new Size(74, 29);
            buttonCaptureA.TabIndex = 2;
            buttonCaptureA.Text = "Capture A";
            buttonCaptureA.UseVisualStyleBackColor = true;
            buttonCaptureA.Click += buttonCaptureA_Click;
            //
            // rowPositionB
            //
            rowPositionB.AutoSize = true;
            rowPositionB.Controls.Add(labelPositionB);
            rowPositionB.Controls.Add(textPositionB);
            rowPositionB.Controls.Add(buttonCaptureB);
            rowPositionB.Location = new Point(3, 89);
            rowPositionB.Name = "rowPositionB";
            rowPositionB.Size = new Size(260, 35);
            rowPositionB.TabIndex = 2;
            rowPositionB.WrapContents = false;
            //
            // labelPositionB
            //
            labelPositionB.Anchor = AnchorStyles.Left;
            labelPositionB.AutoSize = true;
            labelPositionB.Location = new Point(3, 10);
            labelPositionB.Name = "labelPositionB";
            labelPositionB.Size = new Size(97, 15);
            labelPositionB.TabIndex = 0;
            labelPositionB.Text = "Position B (mm)";
            //
            // textPositionB
            //
            textPositionB.Location = new Point(106, 6);
            textPositionB.Name = "textPositionB";
            textPositionB.Size = new Size(70, 23);
            textPositionB.TabIndex = 1;
            textPositionB.Text = "25";
            //
            // buttonCaptureB
            //
            buttonCaptureB.AutoSize = true;
            buttonCaptureB.Location = new Point(184, 3);
            buttonCaptureB.Margin = new Padding(2, 3, 2, 3);
            buttonCaptureB.Name = "buttonCaptureB";
            buttonCaptureB.Size = new Size(74, 29);
            buttonCaptureB.TabIndex = 2;
            buttonCaptureB.Text = "Capture B";
            buttonCaptureB.UseVisualStyleBackColor = true;
            buttonCaptureB.Click += buttonCaptureB_Click;
            //
            // labelCaptured
            //
            labelCaptured.AutoSize = true;
            labelCaptured.Location = new Point(3, 127);
            labelCaptured.Name = "labelCaptured";
            labelCaptured.Size = new Size(86, 15);
            labelCaptured.TabIndex = 3;
            labelCaptured.Text = "A: -    B: -";
            //
            // pageMachine
            //
            pageMachine.Controls.Add(flowMachine);
            pageMachine.Location = new Point(4, 24);
            pageMachine.Name = "pageMachine";
            pageMachine.Size = new Size(506, 402);
            pageMachine.TabIndex = 2;
            pageMachine.Text = "ESP32 and files";
            pageMachine.UseVisualStyleBackColor = true;
            //
            // flowMachine
            //
            flowMachine.AutoScroll = true;
            flowMachine.Controls.Add(groupEsp);
            flowMachine.Controls.Add(groupFiles);
            flowMachine.Dock = DockStyle.Fill;
            flowMachine.FlowDirection = FlowDirection.TopDown;
            flowMachine.Location = new Point(0, 0);
            flowMachine.Name = "flowMachine";
            flowMachine.Size = new Size(506, 402);
            flowMachine.TabIndex = 0;
            flowMachine.WrapContents = false;
            //
            // groupEsp
            //
            groupEsp.AutoSize = true;
            groupEsp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupEsp.Controls.Add(stackEsp);
            groupEsp.Location = new Point(3, 3);
            groupEsp.Name = "groupEsp";
            groupEsp.Padding = new Padding(6);
            groupEsp.Size = new Size(330, 100);
            groupEsp.TabIndex = 0;
            groupEsp.TabStop = false;
            groupEsp.Text = "ESP32";
            //
            // stackEsp
            //
            stackEsp.AutoSize = true;
            stackEsp.Controls.Add(buttonSendLimit);
            stackEsp.Controls.Add(buttonReboot);
            stackEsp.Dock = DockStyle.Fill;
            stackEsp.FlowDirection = FlowDirection.TopDown;
            stackEsp.Location = new Point(6, 22);
            stackEsp.Name = "stackEsp";
            stackEsp.Size = new Size(318, 72);
            stackEsp.TabIndex = 0;
            stackEsp.WrapContents = false;
            //
            // buttonSendLimit
            //
            buttonSendLimit.AutoSize = true;
            buttonSendLimit.Location = new Point(5, 3);
            buttonSendLimit.Margin = new Padding(2, 3, 2, 3);
            buttonSendLimit.Name = "buttonSendLimit";
            buttonSendLimit.Size = new Size(160, 25);
            buttonSendLimit.TabIndex = 0;
            buttonSendLimit.Text = "Send the force limit again";
            buttonSendLimit.UseVisualStyleBackColor = true;
            buttonSendLimit.Click += buttonSendLimit_Click;
            //
            // buttonReboot
            //
            buttonReboot.AutoSize = true;
            buttonReboot.Location = new Point(5, 34);
            buttonReboot.Margin = new Padding(2, 3, 2, 3);
            buttonReboot.Name = "buttonReboot";
            buttonReboot.Size = new Size(120, 25);
            buttonReboot.TabIndex = 1;
            buttonReboot.Text = "Reboot the ESP32";
            buttonReboot.UseVisualStyleBackColor = true;
            buttonReboot.Click += buttonReboot_Click;
            //
            // groupFiles
            //
            groupFiles.AutoSize = true;
            groupFiles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupFiles.Controls.Add(stackFiles);
            groupFiles.Location = new Point(3, 109);
            groupFiles.Name = "groupFiles";
            groupFiles.Padding = new Padding(6);
            groupFiles.Size = new Size(330, 130);
            groupFiles.TabIndex = 1;
            groupFiles.TabStop = false;
            groupFiles.Text = "Files";
            //
            // stackFiles
            //
            stackFiles.AutoSize = true;
            stackFiles.Controls.Add(buttonOpenFolder);
            stackFiles.Controls.Add(buttonReloadSettings);
            stackFiles.Controls.Add(buttonResetCalibration);
            stackFiles.Dock = DockStyle.Fill;
            stackFiles.FlowDirection = FlowDirection.TopDown;
            stackFiles.Location = new Point(6, 22);
            stackFiles.Name = "stackFiles";
            stackFiles.Size = new Size(318, 102);
            stackFiles.TabIndex = 0;
            stackFiles.WrapContents = false;
            //
            // buttonOpenFolder
            //
            buttonOpenFolder.AutoSize = true;
            buttonOpenFolder.Location = new Point(5, 3);
            buttonOpenFolder.Margin = new Padding(2, 3, 2, 3);
            buttonOpenFolder.Name = "buttonOpenFolder";
            buttonOpenFolder.Size = new Size(140, 25);
            buttonOpenFolder.TabIndex = 0;
            buttonOpenFolder.Text = "Open the data folder";
            buttonOpenFolder.UseVisualStyleBackColor = true;
            buttonOpenFolder.Click += buttonOpenFolder_Click;
            //
            // buttonReloadSettings
            //
            buttonReloadSettings.AutoSize = true;
            buttonReloadSettings.Location = new Point(5, 34);
            buttonReloadSettings.Margin = new Padding(2, 3, 2, 3);
            buttonReloadSettings.Name = "buttonReloadSettings";
            buttonReloadSettings.Size = new Size(150, 25);
            buttonReloadSettings.TabIndex = 1;
            buttonReloadSettings.Text = "Reload the settings file";
            buttonReloadSettings.UseVisualStyleBackColor = true;
            buttonReloadSettings.Click += buttonReloadSettings_Click;
            //
            // buttonResetCalibration
            //
            buttonResetCalibration.AutoSize = true;
            buttonResetCalibration.Location = new Point(5, 65);
            buttonResetCalibration.Margin = new Padding(2, 3, 2, 3);
            buttonResetCalibration.Name = "buttonResetCalibration";
            buttonResetCalibration.Size = new Size(260, 25);
            buttonResetCalibration.TabIndex = 2;
            buttonResetCalibration.Text = "Reset the sensor calibration to the defaults";
            buttonResetCalibration.UseVisualStyleBackColor = true;
            buttonResetCalibration.Click += buttonResetCalibration_Click;
            //
            // labelStatus
            //
            labelStatus.Dock = DockStyle.Fill;
            labelStatus.Location = new Point(3, 436);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(514, 44);
            labelStatus.TabIndex = 1;
            //
            // OptionPanel
            //
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(layoutMain);
            Name = "OptionPanel";
            Size = new Size(520, 480);
            layoutMain.ResumeLayout(false);
            tabs.ResumeLayout(false);
            pageSettings.ResumeLayout(false);
            pageCalibration.ResumeLayout(false);
            flowCalibration.ResumeLayout(false);
            flowCalibration.PerformLayout();
            groupLive.ResumeLayout(false);
            groupLive.PerformLayout();
            stackLive.ResumeLayout(false);
            stackLive.PerformLayout();
            groupForce.ResumeLayout(false);
            groupForce.PerformLayout();
            stackForce.ResumeLayout(false);
            stackForce.PerformLayout();
            rowKnownForce.ResumeLayout(false);
            rowKnownForce.PerformLayout();
            groupPosition.ResumeLayout(false);
            groupPosition.PerformLayout();
            stackPosition.ResumeLayout(false);
            stackPosition.PerformLayout();
            rowPositionA.ResumeLayout(false);
            rowPositionA.PerformLayout();
            rowPositionB.ResumeLayout(false);
            rowPositionB.PerformLayout();
            pageMachine.ResumeLayout(false);
            flowMachine.ResumeLayout(false);
            flowMachine.PerformLayout();
            groupEsp.ResumeLayout(false);
            groupEsp.PerformLayout();
            stackEsp.ResumeLayout(false);
            stackEsp.PerformLayout();
            groupFiles.ResumeLayout(false);
            groupFiles.PerformLayout();
            stackFiles.ResumeLayout(false);
            stackFiles.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel layoutMain;
        private TabControl tabs;
        private TabPage pageSettings;
        private PropertyGrid grid;
        private TabPage pageCalibration;
        private FlowLayoutPanel flowCalibration;
        private GroupBox groupLive;
        private FlowLayoutPanel stackLive;
        private Label labelForce;
        private Label labelPosition;
        private GroupBox groupForce;
        private FlowLayoutPanel stackForce;
        private Label labelHintZero;
        private Button buttonZeroForce;
        private Label labelHintGain;
        private FlowLayoutPanel rowKnownForce;
        private Label labelKnownForce;
        private TextBox textKnownForce;
        private Button buttonSetGain;
        private GroupBox groupPosition;
        private FlowLayoutPanel stackPosition;
        private Label labelHintPosition;
        private FlowLayoutPanel rowPositionA;
        private Label labelPositionA;
        private TextBox textPositionA;
        private Button buttonCaptureA;
        private FlowLayoutPanel rowPositionB;
        private Label labelPositionB;
        private TextBox textPositionB;
        private Button buttonCaptureB;
        private Label labelCaptured;
        private TabPage pageMachine;
        private FlowLayoutPanel flowMachine;
        private GroupBox groupEsp;
        private FlowLayoutPanel stackEsp;
        private Button buttonSendLimit;
        private Button buttonReboot;
        private GroupBox groupFiles;
        private FlowLayoutPanel stackFiles;
        private Button buttonOpenFolder;
        private Button buttonReloadSettings;
        private Button buttonResetCalibration;
        private Label labelStatus;
    }
}
