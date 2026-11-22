namespace DynamiX_HMI.Sequencer
{
    partial class SequenceItem
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
            buttonUp = new Button();
            flowLayoutPanel1 = new FlowLayoutPanel();
            buttonDown = new Button();
            textBoxSpeed = new TextBox();
            labelSpeed = new Label();
            textBoxDuration = new TextBox();
            labelDuration = new Label();
            buttonRemove = new Button();
            labelSpdRPM = new Label();
            labelSpdDrive = new Label();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // buttonUp
            // 
            buttonUp.Location = new Point(1, 3);
            buttonUp.Margin = new Padding(1, 3, 1, 3);
            buttonUp.Name = "buttonUp";
            buttonUp.Size = new Size(23, 23);
            buttonUp.TabIndex = 1;
            buttonUp.Text = "↑";
            buttonUp.UseVisualStyleBackColor = true;
            buttonUp.Click += buttonUp_Click;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(buttonUp);
            flowLayoutPanel1.Controls.Add(buttonDown);
            flowLayoutPanel1.Controls.Add(textBoxSpeed);
            flowLayoutPanel1.Controls.Add(labelSpeed);
            flowLayoutPanel1.Controls.Add(textBoxDuration);
            flowLayoutPanel1.Controls.Add(labelDuration);
            flowLayoutPanel1.Controls.Add(buttonRemove);
            flowLayoutPanel1.Controls.Add(labelSpdRPM);
            flowLayoutPanel1.Controls.Add(labelSpdDrive);
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.Location = new Point(0, 0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(288, 54);
            flowLayoutPanel1.TabIndex = 4;
            // 
            // buttonDown
            // 
            buttonDown.Location = new Point(26, 3);
            buttonDown.Margin = new Padding(1, 3, 1, 3);
            buttonDown.Name = "buttonDown";
            buttonDown.Size = new Size(23, 23);
            buttonDown.TabIndex = 2;
            buttonDown.Text = "↓";
            buttonDown.UseVisualStyleBackColor = true;
            buttonDown.Click += buttonDown_Click;
            // 
            // textBoxSpeed
            // 
            textBoxSpeed.Location = new Point(51, 3);
            textBoxSpeed.Margin = new Padding(1, 3, 1, 3);
            textBoxSpeed.Name = "textBoxSpeed";
            textBoxSpeed.Size = new Size(60, 23);
            textBoxSpeed.TabIndex = 3;
            // 
            // labelSpeed
            // 
            labelSpeed.AutoSize = true;
            labelSpeed.Location = new Point(116, 6);
            labelSpeed.Margin = new Padding(4, 6, 4, 0);
            labelSpeed.Name = "labelSpeed";
            labelSpeed.Size = new Size(28, 15);
            labelSpeed.TabIndex = 4;
            labelSpeed.Text = "m/s";
            // 
            // textBoxDuration
            // 
            textBoxDuration.Location = new Point(149, 3);
            textBoxDuration.Margin = new Padding(1, 3, 1, 3);
            textBoxDuration.Name = "textBoxDuration";
            textBoxDuration.Size = new Size(60, 23);
            textBoxDuration.TabIndex = 5;
            textBoxDuration.Text = "5";
            // 
            // labelDuration
            // 
            labelDuration.AutoSize = true;
            labelDuration.Location = new Point(214, 6);
            labelDuration.Margin = new Padding(4, 6, 4, 0);
            labelDuration.Name = "labelDuration";
            labelDuration.Size = new Size(24, 15);
            labelDuration.TabIndex = 6;
            labelDuration.Text = "sec";
            // 
            // buttonRemove
            // 
            buttonRemove.Location = new Point(243, 3);
            buttonRemove.Margin = new Padding(1, 3, 1, 3);
            buttonRemove.Name = "buttonRemove";
            buttonRemove.Size = new Size(23, 23);
            buttonRemove.TabIndex = 7;
            buttonRemove.Text = "X";
            buttonRemove.UseVisualStyleBackColor = true;
            buttonRemove.Click += buttonRemove_Click;
            // 
            // labelSpdRPM
            // 
            labelSpdRPM.AutoSize = true;
            labelSpdRPM.Location = new Point(3, 29);
            labelSpdRPM.Name = "labelSpdRPM";
            labelSpdRPM.Size = new Size(83, 15);
            labelSpdRPM.TabIndex = 8;
            labelSpdRPM.Text = "Speed RPM: ??";
            // 
            // labelSpdDrive
            // 
            labelSpdDrive.AutoSize = true;
            labelSpdDrive.Location = new Point(92, 29);
            labelSpdDrive.Name = "labelSpdDrive";
            labelSpdDrive.Size = new Size(85, 15);
            labelSpdDrive.TabIndex = 9;
            labelSpdDrive.Text = "Speed Drive: ??";
            // 
            // SequenceItem
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Controls.Add(flowLayoutPanel1);
            Name = "SequenceItem";
            Size = new Size(288, 54);
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private Button buttonUp;
        private FlowLayoutPanel flowLayoutPanel1;
        private Button buttonDown;
        private TextBox textBoxSpeed;
        private Label labelSpeed;
        private TextBox textBoxDuration;
        private Label labelDuration;
        private Button buttonRemove;
        private Label labelSpdRPM;
        private Label labelSpdDrive;
    }
}
