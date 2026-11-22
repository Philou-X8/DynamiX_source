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
            checkBoxVisible = new CheckBox();
            buttonRemove = new Button();
            comboBoxColor = new ComboBox();
            groupBox1 = new GroupBox();
            buttonSaveRun = new Button();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // checkBoxVisible
            // 
            checkBoxVisible.Checked = true;
            checkBoxVisible.CheckState = CheckState.Checked;
            checkBoxVisible.Location = new Point(6, 22);
            checkBoxVisible.Name = "checkBoxVisible";
            checkBoxVisible.Size = new Size(55, 23);
            checkBoxVisible.TabIndex = 0;
            checkBoxVisible.Text = "Show";
            checkBoxVisible.UseVisualStyleBackColor = true;
            checkBoxVisible.CheckedChanged += checkBoxVisible_CheckedChanged;
            // 
            // buttonRemove
            // 
            buttonRemove.Location = new Point(213, 22);
            buttonRemove.Name = "buttonRemove";
            buttonRemove.Size = new Size(29, 23);
            buttonRemove.TabIndex = 2;
            buttonRemove.Text = "X";
            buttonRemove.UseVisualStyleBackColor = true;
            buttonRemove.Click += buttonRemove_Click;
            // 
            // comboBoxColor
            // 
            comboBoxColor.FormattingEnabled = true;
            comboBoxColor.Location = new Point(67, 22);
            comboBoxColor.Name = "comboBoxColor";
            comboBoxColor.Size = new Size(82, 23);
            comboBoxColor.TabIndex = 3;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(buttonSaveRun);
            groupBox1.Controls.Add(buttonRemove);
            groupBox1.Controls.Add(comboBoxColor);
            groupBox1.Controls.Add(checkBoxVisible);
            groupBox1.Dock = DockStyle.Fill;
            groupBox1.Location = new Point(0, 0);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(248, 51);
            groupBox1.TabIndex = 1;
            groupBox1.TabStop = false;
            groupBox1.Text = "RunName";
            // 
            // buttonSaveRun
            // 
            buttonSaveRun.Location = new Point(155, 22);
            buttonSaveRun.Name = "buttonSaveRun";
            buttonSaveRun.Size = new Size(52, 23);
            buttonSaveRun.TabIndex = 4;
            buttonSaveRun.Text = "Save";
            buttonSaveRun.UseVisualStyleBackColor = true;
            // 
            // RunItem
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(groupBox1);
            Name = "RunItem";
            Size = new Size(248, 51);
            groupBox1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private CheckBox checkBoxVisible;
        private Button buttonRemove;
        private ComboBox comboBoxColor;
        private GroupBox groupBox1;
        private Button buttonSaveRun;
    }
}
