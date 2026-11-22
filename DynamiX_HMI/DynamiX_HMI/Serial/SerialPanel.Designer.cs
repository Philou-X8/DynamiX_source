namespace DynamiX_HMI.Serial
{
    partial class SerialPanel
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
            layoutGrid = new TableLayoutPanel();
            buttonDisconnect = new Button();
            buttonReconnect = new Button();
            messageSatus = new Label();
            labelCom = new Label();
            labelStatus = new Label();
            comboBoxCom = new ComboBox();
            labelBaud = new Label();
            buttonConnect = new Button();
            comboBoxBaud = new ComboBox();
            layoutGrid.SuspendLayout();
            SuspendLayout();
            // 
            // layoutGrid
            // 
            layoutGrid.ColumnCount = 2;
            layoutGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            layoutGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutGrid.Controls.Add(buttonDisconnect, 0, 5);
            layoutGrid.Controls.Add(buttonReconnect, 0, 4);
            layoutGrid.Controls.Add(messageSatus, 1, 0);
            layoutGrid.Controls.Add(labelCom, 0, 1);
            layoutGrid.Controls.Add(labelStatus, 0, 0);
            layoutGrid.Controls.Add(comboBoxCom, 1, 1);
            layoutGrid.Controls.Add(labelBaud, 0, 2);
            layoutGrid.Controls.Add(buttonConnect, 0, 3);
            layoutGrid.Controls.Add(comboBoxBaud, 1, 2);
            layoutGrid.Dock = DockStyle.Fill;
            layoutGrid.Location = new Point(0, 0);
            layoutGrid.Name = "layoutGrid";
            layoutGrid.RowCount = 6;
            layoutGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6666641F));
            layoutGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6666641F));
            layoutGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6666641F));
            layoutGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6666641F));
            layoutGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6666641F));
            layoutGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6666641F));
            layoutGrid.Size = new Size(200, 220);
            layoutGrid.TabIndex = 12;
            // 
            // buttonDisconnect
            // 
            buttonDisconnect.AutoSize = true;
            layoutGrid.SetColumnSpan(buttonDisconnect, 2);
            buttonDisconnect.Dock = DockStyle.Fill;
            buttonDisconnect.Location = new Point(3, 183);
            buttonDisconnect.Name = "buttonDisconnect";
            buttonDisconnect.Size = new Size(194, 34);
            buttonDisconnect.TabIndex = 8;
            buttonDisconnect.Text = "Disconnect";
            buttonDisconnect.UseVisualStyleBackColor = true;
            buttonDisconnect.Click += ButtonDisconnect_Click;
            // 
            // buttonReconnect
            // 
            buttonReconnect.AutoSize = true;
            layoutGrid.SetColumnSpan(buttonReconnect, 2);
            buttonReconnect.Dock = DockStyle.Fill;
            buttonReconnect.Location = new Point(3, 147);
            buttonReconnect.Name = "buttonReconnect";
            buttonReconnect.Size = new Size(194, 30);
            buttonReconnect.TabIndex = 7;
            buttonReconnect.Text = "Reconnect";
            buttonReconnect.UseVisualStyleBackColor = true;
            buttonReconnect.Click += ButtonReconnect_Click;
            // 
            // messageSatus
            // 
            messageSatus.AutoSize = true;
            messageSatus.Dock = DockStyle.Fill;
            messageSatus.Location = new Point(83, 3);
            messageSatus.Margin = new Padding(3);
            messageSatus.Name = "messageSatus";
            messageSatus.Size = new Size(114, 30);
            messageSatus.TabIndex = 3;
            messageSatus.Text = "label2";
            // 
            // labelCom
            // 
            labelCom.AutoSize = true;
            labelCom.Dock = DockStyle.Fill;
            labelCom.Location = new Point(3, 39);
            labelCom.Margin = new Padding(3);
            labelCom.Name = "labelCom";
            labelCom.Size = new Size(74, 30);
            labelCom.TabIndex = 4;
            labelCom.Text = "COM Port :";
            labelCom.TextAlign = ContentAlignment.TopRight;
            // 
            // labelStatus
            // 
            labelStatus.AutoSize = true;
            labelStatus.Dock = DockStyle.Fill;
            labelStatus.Location = new Point(3, 3);
            labelStatus.Margin = new Padding(3);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(74, 30);
            labelStatus.TabIndex = 0;
            labelStatus.Text = "Satus :";
            labelStatus.TextAlign = ContentAlignment.TopRight;
            // 
            // comboBoxCom
            // 
            comboBoxCom.Dock = DockStyle.Fill;
            comboBoxCom.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCom.FormattingEnabled = true;
            comboBoxCom.Location = new Point(83, 39);
            comboBoxCom.Name = "comboBoxCom";
            comboBoxCom.Size = new Size(114, 23);
            comboBoxCom.TabIndex = 1;
            // 
            // labelBaud
            // 
            labelBaud.AutoSize = true;
            labelBaud.Dock = DockStyle.Fill;
            labelBaud.Location = new Point(3, 75);
            labelBaud.Margin = new Padding(3);
            labelBaud.Name = "labelBaud";
            labelBaud.Size = new Size(74, 30);
            labelBaud.TabIndex = 6;
            labelBaud.Text = "Baud rate :";
            labelBaud.TextAlign = ContentAlignment.TopRight;
            // 
            // buttonConnect
            // 
            buttonConnect.AutoSize = true;
            layoutGrid.SetColumnSpan(buttonConnect, 2);
            buttonConnect.Dock = DockStyle.Fill;
            buttonConnect.Location = new Point(3, 111);
            buttonConnect.Name = "buttonConnect";
            buttonConnect.Size = new Size(194, 30);
            buttonConnect.TabIndex = 5;
            buttonConnect.Text = "Connect";
            buttonConnect.UseVisualStyleBackColor = true;
            buttonConnect.Click += ButtonConnect_Click;
            // 
            // comboBoxBaud
            // 
            comboBoxBaud.Dock = DockStyle.Fill;
            comboBoxBaud.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxBaud.FormattingEnabled = true;
            comboBoxBaud.Location = new Point(83, 75);
            comboBoxBaud.Name = "comboBoxBaud";
            comboBoxBaud.Size = new Size(114, 23);
            comboBoxBaud.TabIndex = 2;
            // 
            // SerialPanel
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(layoutGrid);
            MinimumSize = new Size(200, 220);
            Name = "SerialPanel";
            Size = new Size(200, 220);
            layoutGrid.ResumeLayout(false);
            layoutGrid.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel layoutGrid;
        private Button buttonDisconnect;
        private Button buttonReconnect;
        private Label messageSatus;
        private Label labelCom;
        private Label labelStatus;
        private ComboBox comboBoxCom;
        private Label labelBaud;
        private Button buttonConnect;
        private ComboBox comboBoxBaud;
    }
}
