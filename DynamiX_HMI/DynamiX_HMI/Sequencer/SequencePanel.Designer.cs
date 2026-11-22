namespace DynamiX_HMI.Sequencer
{
    partial class SequencePanel
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
            buttonAdd = new Button();
            flowLayoutPanel1 = new FlowLayoutPanel();
            SuspendLayout();
            // 
            // buttonAdd
            // 
            buttonAdd.Dock = DockStyle.Bottom;
            buttonAdd.Location = new Point(0, 54);
            buttonAdd.Name = "buttonAdd";
            buttonAdd.Size = new Size(291, 26);
            buttonAdd.TabIndex = 0;
            buttonAdd.Text = "Add";
            buttonAdd.UseVisualStyleBackColor = true;
            buttonAdd.Click += buttonAdd_Click;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(0, 0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(291, 54);
            flowLayoutPanel1.TabIndex = 1;
            // 
            // SequencePanel
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(flowLayoutPanel1);
            Controls.Add(buttonAdd);
            Name = "SequencePanel";
            Size = new Size(291, 80);
            ResumeLayout(false);
        }

        #endregion

        private Button buttonAdd;
        private FlowLayoutPanel flowLayoutPanel1;
    }
}
