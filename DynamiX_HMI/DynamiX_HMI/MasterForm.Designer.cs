namespace DynamiX_HMI
{
    partial class MasterForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            graphPanel1 = new DynamiX_HMI.Grapher.GraphPanel();
            labelDebugMessage = new Label();
            buttonConnection = new Button();
            leftGridLayout = new TableLayoutPanel();
            serialPanel = new DynamiX_HMI.Serial.SerialPanel();
            groupBoxMachine = new GroupBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            buttonMachineStop = new Button();
            buttonMachineStart = new Button();
            buttonTar = new Button();
            groupBoxSequence = new GroupBox();
            sequencePanel1 = new DynamiX_HMI.Sequencer.SequencePanel();
            mainSplitContainerLeft = new SplitContainer();
            mainSplitContainerRight = new SplitContainer();
            centerGridLayout = new TableLayoutPanel();
            groupBoxLiveData = new GroupBox();
            tableLayoutPanel3 = new TableLayoutPanel();
            labelForceMaxVal = new Label();
            labelForceMinVal = new Label();
            labelForceMin = new Label();
            labelPosMax = new Label();
            labelPosMin = new Label();
            labelForce = new Label();
            labelStrokeVal = new Label();
            labelStroke = new Label();
            labelPosVal = new Label();
            labelPosMinVal = new Label();
            labelForceVal = new Label();
            labelPosMaxVal = new Label();
            labelPosition = new Label();
            labelTempVal = new Label();
            labelTemp = new Label();
            labelForceMax = new Label();
            rightGridLayout = new TableLayoutPanel();
            tableLayoutPanel4 = new TableLayoutPanel();
            labelStatus = new Label();
            buttonOptions = new Button();
            optionPanel1 = new DynamiX_HMI.Menus.OptionPanel();
            runPanel1 = new DynamiX_HMI.Grapher.RunPanel();
            groupBoxProject = new GroupBox();
            tableLayoutPanelProject = new TableLayoutPanel();
            buttonProjectNew = new Button();
            textBoxProject = new TextBox();
            buttonProjectLoad = new Button();
            buttonProjectBrowse = new Button();
            leftGridLayout.SuspendLayout();
            groupBoxMachine.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            groupBoxSequence.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mainSplitContainerLeft).BeginInit();
            mainSplitContainerLeft.Panel1.SuspendLayout();
            mainSplitContainerLeft.Panel2.SuspendLayout();
            mainSplitContainerLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mainSplitContainerRight).BeginInit();
            mainSplitContainerRight.Panel1.SuspendLayout();
            mainSplitContainerRight.Panel2.SuspendLayout();
            mainSplitContainerRight.SuspendLayout();
            centerGridLayout.SuspendLayout();
            groupBoxLiveData.SuspendLayout();
            tableLayoutPanel3.SuspendLayout();
            rightGridLayout.SuspendLayout();
            tableLayoutPanel4.SuspendLayout();
            groupBoxProject.SuspendLayout();
            tableLayoutPanelProject.SuspendLayout();
            SuspendLayout();
            // 
            // graphPanel1
            // 
            graphPanel1.Dock = DockStyle.Fill;
            graphPanel1.Location = new Point(4, 45);
            graphPanel1.Name = "graphPanel1";
            graphPanel1.Size = new Size(511, 507);
            graphPanel1.TabIndex = 1;
            // 
            // labelDebugMessage
            // 
            labelDebugMessage.AutoSize = true;
            labelDebugMessage.Location = new Point(4, 4);
            labelDebugMessage.Margin = new Padding(3);
            labelDebugMessage.Name = "labelDebugMessage";
            labelDebugMessage.Padding = new Padding(3);
            labelDebugMessage.Size = new Size(82, 21);
            labelDebugMessage.TabIndex = 3;
            labelDebugMessage.Text = "message box";
            labelDebugMessage.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // buttonConnection
            // 
            buttonConnection.Dock = DockStyle.Fill;
            buttonConnection.Location = new Point(4, 4);
            buttonConnection.Name = "buttonConnection";
            buttonConnection.Size = new Size(265, 34);
            buttonConnection.TabIndex = 2;
            buttonConnection.Text = "Not Connected";
            buttonConnection.UseVisualStyleBackColor = true;
            buttonConnection.Click += ButtonConnection_Click;
            // 
            // leftGridLayout
            // 
            leftGridLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            leftGridLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            leftGridLayout.ColumnCount = 1;
            leftGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            leftGridLayout.Controls.Add(serialPanel, 0, 1);
            leftGridLayout.Controls.Add(buttonConnection, 0, 0);
            leftGridLayout.Controls.Add(groupBoxMachine, 0, 3);
            leftGridLayout.Controls.Add(groupBoxSequence, 0, 2);
            leftGridLayout.Dock = DockStyle.Fill;
            leftGridLayout.Location = new Point(0, 0);
            leftGridLayout.Name = "leftGridLayout";
            leftGridLayout.RowCount = 4;
            leftGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            leftGridLayout.RowStyles.Add(new RowStyle());
            leftGridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            leftGridLayout.Size = new Size(273, 657);
            leftGridLayout.TabIndex = 1;
            // 
            // serialPanel
            // 
            serialPanel.AutoSize = true;
            serialPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            serialPanel.Dock = DockStyle.Fill;
            serialPanel.Location = new Point(1, 42);
            serialPanel.Margin = new Padding(0);
            serialPanel.MinimumSize = new Size(200, 220);
            serialPanel.Name = "serialPanel";
            serialPanel.Size = new Size(271, 220);
            serialPanel.TabIndex = 2;
            // 
            // groupBoxMachine
            // 
            groupBoxMachine.Controls.Add(tableLayoutPanel1);
            groupBoxMachine.Dock = DockStyle.Fill;
            groupBoxMachine.Location = new Point(4, 509);
            groupBoxMachine.MinimumSize = new Size(0, 100);
            groupBoxMachine.Name = "groupBoxMachine";
            groupBoxMachine.Size = new Size(265, 144);
            groupBoxMachine.TabIndex = 3;
            groupBoxMachine.TabStop = false;
            groupBoxMachine.Text = "Machine Control";
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(buttonMachineStop, 0, 2);
            tableLayoutPanel1.Controls.Add(buttonMachineStart, 0, 1);
            tableLayoutPanel1.Controls.Add(buttonTar, 0, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(3, 19);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.Size = new Size(259, 122);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // buttonMachineStop
            // 
            buttonMachineStop.Dock = DockStyle.Fill;
            buttonMachineStop.Location = new Point(3, 83);
            buttonMachineStop.Name = "buttonMachineStop";
            buttonMachineStop.Size = new Size(253, 36);
            buttonMachineStop.TabIndex = 2;
            buttonMachineStop.Text = "Abort";
            buttonMachineStop.UseVisualStyleBackColor = true;
            buttonMachineStop.Click += buttonMachineStop_Click;
            // 
            // buttonMachineStart
            // 
            buttonMachineStart.Dock = DockStyle.Fill;
            buttonMachineStart.Location = new Point(3, 43);
            buttonMachineStart.Name = "buttonMachineStart";
            buttonMachineStart.Size = new Size(253, 34);
            buttonMachineStart.TabIndex = 0;
            buttonMachineStart.Text = "Start Run";
            buttonMachineStart.UseVisualStyleBackColor = true;
            buttonMachineStart.Click += buttonMachineStart_Click;
            // 
            // buttonTar
            // 
            buttonTar.Dock = DockStyle.Fill;
            buttonTar.Location = new Point(3, 3);
            buttonTar.Name = "buttonTar";
            buttonTar.Size = new Size(253, 34);
            buttonTar.TabIndex = 3;
            buttonTar.Text = "TAR and bottom out";
            buttonTar.UseVisualStyleBackColor = true;
            buttonTar.Click += buttonTar_Click;
            // 
            // groupBoxSequence
            // 
            groupBoxSequence.Controls.Add(sequencePanel1);
            groupBoxSequence.Dock = DockStyle.Fill;
            groupBoxSequence.Location = new Point(4, 266);
            groupBoxSequence.Name = "groupBoxSequence";
            groupBoxSequence.Size = new Size(265, 236);
            groupBoxSequence.TabIndex = 0;
            groupBoxSequence.TabStop = false;
            groupBoxSequence.Text = "Test Sequence";
            // 
            // sequencePanel1
            // 
            sequencePanel1.Dock = DockStyle.Fill;
            sequencePanel1.Location = new Point(3, 19);
            sequencePanel1.Name = "sequencePanel1";
            sequencePanel1.Size = new Size(259, 214);
            sequencePanel1.TabIndex = 0;
            // 
            // mainSplitContainerLeft
            // 
            mainSplitContainerLeft.Dock = DockStyle.Fill;
            mainSplitContainerLeft.Location = new Point(0, 0);
            mainSplitContainerLeft.Name = "mainSplitContainerLeft";
            // 
            // mainSplitContainerLeft.Panel1
            // 
            mainSplitContainerLeft.Panel1.Controls.Add(leftGridLayout);
            mainSplitContainerLeft.Panel1MinSize = 206;
            // 
            // mainSplitContainerLeft.Panel2
            // 
            mainSplitContainerLeft.Panel2.Controls.Add(mainSplitContainerRight);
            mainSplitContainerLeft.Size = new Size(1053, 657);
            mainSplitContainerLeft.SplitterDistance = 273;
            mainSplitContainerLeft.TabIndex = 1;
            // 
            // mainSplitContainerRight
            // 
            mainSplitContainerRight.Dock = DockStyle.Fill;
            mainSplitContainerRight.Location = new Point(0, 0);
            mainSplitContainerRight.Name = "mainSplitContainerRight";
            // 
            // mainSplitContainerRight.Panel1
            // 
            mainSplitContainerRight.Panel1.Controls.Add(centerGridLayout);
            // 
            // mainSplitContainerRight.Panel2
            // 
            mainSplitContainerRight.Panel2.Controls.Add(rightGridLayout);
            mainSplitContainerRight.Panel2MinSize = 150;
            mainSplitContainerRight.Size = new Size(776, 657);
            mainSplitContainerRight.SplitterDistance = 519;
            mainSplitContainerRight.TabIndex = 0;
            mainSplitContainerRight.TabStop = false;
            // 
            // centerGridLayout
            // 
            centerGridLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            centerGridLayout.ColumnCount = 1;
            centerGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            centerGridLayout.Controls.Add(graphPanel1, 0, 1);
            centerGridLayout.Controls.Add(labelDebugMessage, 0, 0);
            centerGridLayout.Controls.Add(groupBoxLiveData, 0, 2);
            centerGridLayout.Dock = DockStyle.Fill;
            centerGridLayout.Location = new Point(0, 0);
            centerGridLayout.Name = "centerGridLayout";
            centerGridLayout.RowCount = 3;
            centerGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            centerGridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            centerGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            centerGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            centerGridLayout.Size = new Size(519, 657);
            centerGridLayout.TabIndex = 0;
            // 
            // groupBoxLiveData
            // 
            groupBoxLiveData.Controls.Add(tableLayoutPanel3);
            groupBoxLiveData.Dock = DockStyle.Fill;
            groupBoxLiveData.Location = new Point(4, 559);
            groupBoxLiveData.Name = "groupBoxLiveData";
            groupBoxLiveData.Size = new Size(511, 94);
            groupBoxLiveData.TabIndex = 4;
            groupBoxLiveData.TabStop = false;
            groupBoxLiveData.Text = "Live data";
            // 
            // tableLayoutPanel3
            // 
            tableLayoutPanel3.ColumnCount = 8;
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.4993782F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.4993753F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.4993753F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.4993753F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.4993782F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.4993753F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5018749F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5018749F));
            tableLayoutPanel3.Controls.Add(labelForceMaxVal, 7, 1);
            tableLayoutPanel3.Controls.Add(labelForceMinVal, 5, 1);
            tableLayoutPanel3.Controls.Add(labelForceMin, 4, 1);
            tableLayoutPanel3.Controls.Add(labelPosMax, 2, 1);
            tableLayoutPanel3.Controls.Add(labelPosMin, 0, 1);
            tableLayoutPanel3.Controls.Add(labelForce, 4, 0);
            tableLayoutPanel3.Controls.Add(labelStrokeVal, 3, 0);
            tableLayoutPanel3.Controls.Add(labelStroke, 2, 0);
            tableLayoutPanel3.Controls.Add(labelPosVal, 1, 0);
            tableLayoutPanel3.Controls.Add(labelPosMinVal, 1, 1);
            tableLayoutPanel3.Controls.Add(labelForceVal, 5, 0);
            tableLayoutPanel3.Controls.Add(labelPosMaxVal, 3, 1);
            tableLayoutPanel3.Controls.Add(labelPosition, 0, 0);
            tableLayoutPanel3.Controls.Add(labelTempVal, 7, 0);
            tableLayoutPanel3.Controls.Add(labelTemp, 6, 0);
            tableLayoutPanel3.Controls.Add(labelForceMax, 6, 1);
            tableLayoutPanel3.Dock = DockStyle.Fill;
            tableLayoutPanel3.Location = new Point(3, 19);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            tableLayoutPanel3.RowCount = 2;
            tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel3.Size = new Size(505, 72);
            tableLayoutPanel3.TabIndex = 7;
            // 
            // labelForceMaxVal
            // 
            labelForceMaxVal.AutoSize = true;
            labelForceMaxVal.Location = new Point(444, 39);
            labelForceMaxVal.Margin = new Padding(3);
            labelForceMaxVal.Name = "labelForceMaxVal";
            labelForceMaxVal.Padding = new Padding(3);
            labelForceMaxVal.Size = new Size(23, 21);
            labelForceMaxVal.TabIndex = 19;
            labelForceMaxVal.Text = "??";
            // 
            // labelForceMinVal
            // 
            labelForceMinVal.AutoSize = true;
            labelForceMinVal.Location = new Point(318, 39);
            labelForceMinVal.Margin = new Padding(3);
            labelForceMinVal.Name = "labelForceMinVal";
            labelForceMinVal.Padding = new Padding(3);
            labelForceMinVal.Size = new Size(23, 21);
            labelForceMinVal.TabIndex = 18;
            labelForceMinVal.Text = "??";
            // 
            // labelForceMin
            // 
            labelForceMin.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            labelForceMin.AutoSize = true;
            labelForceMin.Location = new Point(269, 39);
            labelForceMin.Margin = new Padding(3);
            labelForceMin.Name = "labelForceMin";
            labelForceMin.Padding = new Padding(3);
            labelForceMin.Size = new Size(43, 21);
            labelForceMin.TabIndex = 16;
            labelForceMin.Text = "F Bot:";
            labelForceMin.TextAlign = ContentAlignment.TopRight;
            // 
            // labelPosMax
            // 
            labelPosMax.AutoSize = true;
            labelPosMax.Dock = DockStyle.Fill;
            labelPosMax.Location = new Point(129, 39);
            labelPosMax.Margin = new Padding(3);
            labelPosMax.Name = "labelPosMax";
            labelPosMax.Padding = new Padding(3);
            labelPosMax.Size = new Size(57, 30);
            labelPosMax.TabIndex = 13;
            labelPosMax.Text = "Top:";
            labelPosMax.TextAlign = ContentAlignment.TopRight;
            // 
            // labelPosMin
            // 
            labelPosMin.AutoSize = true;
            labelPosMin.Dock = DockStyle.Fill;
            labelPosMin.Location = new Point(3, 39);
            labelPosMin.Margin = new Padding(3);
            labelPosMin.Name = "labelPosMin";
            labelPosMin.Padding = new Padding(3);
            labelPosMin.Size = new Size(57, 30);
            labelPosMin.TabIndex = 12;
            labelPosMin.Text = "Bottom:";
            labelPosMin.TextAlign = ContentAlignment.TopRight;
            // 
            // labelForce
            // 
            labelForce.AutoSize = true;
            labelForce.Dock = DockStyle.Fill;
            labelForce.Location = new Point(255, 3);
            labelForce.Margin = new Padding(3);
            labelForce.Name = "labelForce";
            labelForce.Padding = new Padding(3);
            labelForce.Size = new Size(57, 30);
            labelForce.TabIndex = 11;
            labelForce.Text = "Force:";
            labelForce.TextAlign = ContentAlignment.TopRight;
            // 
            // labelStrokeVal
            // 
            labelStrokeVal.AutoSize = true;
            labelStrokeVal.Dock = DockStyle.Fill;
            labelStrokeVal.Location = new Point(192, 3);
            labelStrokeVal.Margin = new Padding(3);
            labelStrokeVal.Name = "labelStrokeVal";
            labelStrokeVal.Padding = new Padding(3);
            labelStrokeVal.Size = new Size(57, 30);
            labelStrokeVal.TabIndex = 10;
            labelStrokeVal.Text = "??";
            // 
            // labelStroke
            // 
            labelStroke.AutoSize = true;
            labelStroke.Dock = DockStyle.Fill;
            labelStroke.Location = new Point(129, 3);
            labelStroke.Margin = new Padding(3);
            labelStroke.Name = "labelStroke";
            labelStroke.Padding = new Padding(3);
            labelStroke.Size = new Size(57, 30);
            labelStroke.TabIndex = 9;
            labelStroke.Text = "Stroke:";
            labelStroke.TextAlign = ContentAlignment.TopRight;
            // 
            // labelPosVal
            // 
            labelPosVal.AutoSize = true;
            labelPosVal.Dock = DockStyle.Fill;
            labelPosVal.Location = new Point(66, 3);
            labelPosVal.Margin = new Padding(3);
            labelPosVal.Name = "labelPosVal";
            labelPosVal.Padding = new Padding(3);
            labelPosVal.Size = new Size(57, 30);
            labelPosVal.TabIndex = 8;
            labelPosVal.Text = "??";
            // 
            // labelPosMinVal
            // 
            labelPosMinVal.AutoSize = true;
            labelPosMinVal.Dock = DockStyle.Fill;
            labelPosMinVal.Location = new Point(66, 39);
            labelPosMinVal.Margin = new Padding(3);
            labelPosMinVal.Name = "labelPosMinVal";
            labelPosMinVal.Padding = new Padding(3);
            labelPosMinVal.Size = new Size(57, 30);
            labelPosMinVal.TabIndex = 3;
            labelPosMinVal.Text = "??";
            // 
            // labelForceVal
            // 
            labelForceVal.AutoSize = true;
            labelForceVal.Dock = DockStyle.Fill;
            labelForceVal.Location = new Point(318, 3);
            labelForceVal.Margin = new Padding(3);
            labelForceVal.Name = "labelForceVal";
            labelForceVal.Padding = new Padding(3);
            labelForceVal.Size = new Size(57, 30);
            labelForceVal.TabIndex = 6;
            labelForceVal.Text = "??";
            // 
            // labelPosMaxVal
            // 
            labelPosMaxVal.AutoSize = true;
            labelPosMaxVal.Dock = DockStyle.Fill;
            labelPosMaxVal.Location = new Point(192, 39);
            labelPosMaxVal.Margin = new Padding(3);
            labelPosMaxVal.Name = "labelPosMaxVal";
            labelPosMaxVal.Padding = new Padding(3);
            labelPosMaxVal.Size = new Size(57, 30);
            labelPosMaxVal.TabIndex = 4;
            labelPosMaxVal.Text = "??";
            // 
            // labelPosition
            // 
            labelPosition.AutoSize = true;
            labelPosition.Dock = DockStyle.Fill;
            labelPosition.Location = new Point(3, 3);
            labelPosition.Margin = new Padding(3);
            labelPosition.Name = "labelPosition";
            labelPosition.Padding = new Padding(3);
            labelPosition.Size = new Size(57, 30);
            labelPosition.TabIndex = 7;
            labelPosition.Text = "Position:";
            labelPosition.TextAlign = ContentAlignment.TopRight;
            // 
            // labelTempVal
            // 
            labelTempVal.AutoSize = true;
            labelTempVal.Location = new Point(444, 3);
            labelTempVal.Margin = new Padding(3);
            labelTempVal.Name = "labelTempVal";
            labelTempVal.Padding = new Padding(3);
            labelTempVal.Size = new Size(23, 21);
            labelTempVal.TabIndex = 15;
            labelTempVal.Text = "??";
            // 
            // labelTemp
            // 
            labelTemp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            labelTemp.AutoSize = true;
            labelTemp.Location = new Point(393, 3);
            labelTemp.Margin = new Padding(3);
            labelTemp.Name = "labelTemp";
            labelTemp.Padding = new Padding(3);
            labelTemp.Size = new Size(45, 21);
            labelTemp.TabIndex = 14;
            labelTemp.Text = "Temp:";
            labelTemp.TextAlign = ContentAlignment.TopRight;
            // 
            // labelForceMax
            // 
            labelForceMax.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            labelForceMax.AutoSize = true;
            labelForceMax.Location = new Point(394, 39);
            labelForceMax.Margin = new Padding(3);
            labelForceMax.Name = "labelForceMax";
            labelForceMax.Padding = new Padding(3);
            labelForceMax.Size = new Size(44, 21);
            labelForceMax.TabIndex = 17;
            labelForceMax.Text = "F Top:";
            labelForceMax.TextAlign = ContentAlignment.TopRight;
            // 
            // rightGridLayout
            // 
            rightGridLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            rightGridLayout.ColumnCount = 1;
            rightGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightGridLayout.Controls.Add(tableLayoutPanel4, 0, 0);
            rightGridLayout.Controls.Add(optionPanel1, 0, 1);
            rightGridLayout.Controls.Add(runPanel1, 0, 3);
            rightGridLayout.Controls.Add(groupBoxProject, 0, 2);
            rightGridLayout.Dock = DockStyle.Fill;
            rightGridLayout.Location = new Point(0, 0);
            rightGridLayout.Name = "rightGridLayout";
            rightGridLayout.RowCount = 4;
            rightGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            rightGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));
            rightGridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            rightGridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rightGridLayout.Size = new Size(253, 657);
            rightGridLayout.TabIndex = 0;
            // 
            // tableLayoutPanel4
            // 
            tableLayoutPanel4.ColumnCount = 3;
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            tableLayoutPanel4.Controls.Add(labelStatus, 0, 0);
            tableLayoutPanel4.Controls.Add(buttonOptions, 2, 0);
            tableLayoutPanel4.Dock = DockStyle.Fill;
            tableLayoutPanel4.Location = new Point(1, 1);
            tableLayoutPanel4.Margin = new Padding(0);
            tableLayoutPanel4.Name = "tableLayoutPanel4";
            tableLayoutPanel4.RowCount = 1;
            tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel4.Size = new Size(251, 40);
            tableLayoutPanel4.TabIndex = 4;
            // 
            // labelStatus
            // 
            labelStatus.AutoSize = true;
            labelStatus.Location = new Point(3, 3);
            labelStatus.Margin = new Padding(3);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(32, 15);
            labelStatus.TabIndex = 1;
            labelStatus.Text = "state";
            // 
            // buttonOptions
            // 
            buttonOptions.Dock = DockStyle.Fill;
            buttonOptions.Location = new Point(153, 3);
            buttonOptions.Name = "buttonOptions";
            buttonOptions.Size = new Size(95, 34);
            buttonOptions.TabIndex = 2;
            buttonOptions.Text = "Options";
            buttonOptions.UseVisualStyleBackColor = true;
            buttonOptions.Click += buttonOptions_Click;
            // 
            // optionPanel1
            // 
            optionPanel1.Dock = DockStyle.Fill;
            optionPanel1.Location = new Point(4, 45);
            optionPanel1.Name = "optionPanel1";
            optionPanel1.Size = new Size(245, 174);
            optionPanel1.TabIndex = 5;
            // 
            // runPanel1
            // 
            runPanel1.Dock = DockStyle.Fill;
            runPanel1.Location = new Point(4, 327);
            runPanel1.Name = "runPanel1";
            runPanel1.Size = new Size(245, 326);
            runPanel1.TabIndex = 2;
            // 
            // groupBoxProject
            // 
            groupBoxProject.Controls.Add(tableLayoutPanelProject);
            groupBoxProject.Dock = DockStyle.Fill;
            groupBoxProject.Location = new Point(4, 226);
            groupBoxProject.Name = "groupBoxProject";
            groupBoxProject.Size = new Size(245, 94);
            groupBoxProject.TabIndex = 0;
            groupBoxProject.TabStop = false;
            groupBoxProject.Text = "Active Project";
            // 
            // tableLayoutPanelProject
            // 
            tableLayoutPanelProject.ColumnCount = 3;
            tableLayoutPanelProject.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanelProject.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanelProject.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34F));
            tableLayoutPanelProject.Controls.Add(buttonProjectNew, 0, 1);
            tableLayoutPanelProject.Controls.Add(textBoxProject, 0, 0);
            tableLayoutPanelProject.Controls.Add(buttonProjectLoad, 1, 1);
            tableLayoutPanelProject.Controls.Add(buttonProjectBrowse, 2, 0);
            tableLayoutPanelProject.Dock = DockStyle.Fill;
            tableLayoutPanelProject.Location = new Point(3, 19);
            tableLayoutPanelProject.Name = "tableLayoutPanelProject";
            tableLayoutPanelProject.RowCount = 2;
            tableLayoutPanelProject.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanelProject.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanelProject.Size = new Size(239, 72);
            tableLayoutPanelProject.TabIndex = 5;
            // 
            // buttonProjectNew
            // 
            buttonProjectNew.Dock = DockStyle.Fill;
            buttonProjectNew.Location = new Point(3, 39);
            buttonProjectNew.Name = "buttonProjectNew";
            buttonProjectNew.Size = new Size(113, 30);
            buttonProjectNew.TabIndex = 3;
            buttonProjectNew.Text = "New Project";
            buttonProjectNew.UseVisualStyleBackColor = true;
            buttonProjectNew.Click += buttonProjectNew_Click;
            // 
            // textBoxProject
            // 
            tableLayoutPanelProject.SetColumnSpan(textBoxProject, 2);
            textBoxProject.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            textBoxProject.ReadOnly = true;
            textBoxProject.Location = new Point(3, 3);
            textBoxProject.Name = "textBoxProject";
            textBoxProject.Size = new Size(233, 23);
            textBoxProject.TabIndex = 1;
            // 
            // buttonProjectLoad
            // 
            tableLayoutPanelProject.SetColumnSpan(buttonProjectLoad, 2);
            buttonProjectLoad.Dock = DockStyle.Fill;
            buttonProjectLoad.Location = new Point(122, 39);
            buttonProjectLoad.Name = "buttonProjectLoad";
            buttonProjectLoad.Size = new Size(114, 30);
            buttonProjectLoad.TabIndex = 4;
            buttonProjectLoad.Text = "Load Project";
            buttonProjectLoad.UseVisualStyleBackColor = true;
            buttonProjectLoad.Click += buttonProjectLoad_Click;
            // 
            // buttonProjectBrowse
            // 
            buttonProjectBrowse.Dock = DockStyle.Fill;
            buttonProjectBrowse.Margin = new Padding(0, 3, 3, 3);
            buttonProjectBrowse.Name = "buttonProjectBrowse";
            buttonProjectBrowse.Size = new Size(31, 27);
            buttonProjectBrowse.TabIndex = 2;
            buttonProjectBrowse.Text = "...";
            buttonProjectBrowse.UseVisualStyleBackColor = true;
            buttonProjectBrowse.Click += buttonProjectLoad_Click;
            // 
            // MasterForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1053, 657);
            Controls.Add(mainSplitContainerLeft);
            Name = "MasterForm";
            Text = "MasterForm";
            leftGridLayout.ResumeLayout(false);
            leftGridLayout.PerformLayout();
            groupBoxMachine.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            groupBoxSequence.ResumeLayout(false);
            mainSplitContainerLeft.Panel1.ResumeLayout(false);
            mainSplitContainerLeft.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mainSplitContainerLeft).EndInit();
            mainSplitContainerLeft.ResumeLayout(false);
            mainSplitContainerRight.Panel1.ResumeLayout(false);
            mainSplitContainerRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mainSplitContainerRight).EndInit();
            mainSplitContainerRight.ResumeLayout(false);
            centerGridLayout.ResumeLayout(false);
            centerGridLayout.PerformLayout();
            groupBoxLiveData.ResumeLayout(false);
            tableLayoutPanel3.ResumeLayout(false);
            tableLayoutPanel3.PerformLayout();
            rightGridLayout.ResumeLayout(false);
            tableLayoutPanel4.ResumeLayout(false);
            tableLayoutPanel4.PerformLayout();
            groupBoxProject.ResumeLayout(false);
            tableLayoutPanelProject.ResumeLayout(false);
            tableLayoutPanelProject.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private TableLayoutPanel leftGridLayout;
        private Button buttonConnection;
        private GroupBox groupBoxSequence;
        private Label labelDebugMessage;
        private Serial.SerialPanel serialPanel;
        private GroupBox groupBoxMachine;
        private TableLayoutPanel tableLayoutPanel1;
        private Button buttonMachineStart;
        private Button buttonMachineStop;
        private SplitContainer mainSplitContainerLeft;
        private SplitContainer mainSplitContainerRight;
        private Grapher.GraphPanel graphPanel1;
        private TableLayoutPanel centerGridLayout;
        private TableLayoutPanel rightGridLayout;
        private GroupBox groupBoxProject;
        private Label labelStatus;
        private Sequencer.SequencePanel sequencePanel1;
        private Grapher.RunPanel runPanel1;
        private GroupBox groupBoxLiveData;
        private Label labelPosMinVal;
        private Label labelPosMaxVal;
        private Label labelForceVal;
        private Button buttonTar;
        private TableLayoutPanel tableLayoutPanel3;
        private Label labelTempVal;
        private Label labelTemp;
        private Label labelPosMax;
        private Label labelPosMin;
        private Label labelForce;
        private Label labelStrokeVal;
        private Label labelStroke;
        private Label labelPosVal;
        private Label labelPosition;
        private TableLayoutPanel tableLayoutPanel4;
        private Button buttonOptions;
        private Menus.OptionPanel optionPanel1;
        private Label labelForceMaxVal;
        private Label labelForceMinVal;
        private Label labelForceMin;
        private Label labelForceMax;
        private TextBox textBoxProject;
        private Button buttonProjectLoad;
        private Button buttonProjectNew;
        private Button buttonProjectBrowse;
        private TableLayoutPanel tableLayoutPanelProject;
    }
}