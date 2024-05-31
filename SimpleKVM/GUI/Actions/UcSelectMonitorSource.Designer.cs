namespace SimpleKVM.GUI
{
    partial class UcSelectMonitorSource
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
            lblMonitorName = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            cmbSources = new System.Windows.Forms.ComboBox();
            SuspendLayout();
            // 
            // lblMonitorName
            // 
            lblMonitorName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lblMonitorName.Location = new System.Drawing.Point(14, 8);
            lblMonitorName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            lblMonitorName.Name = "lblMonitorName";
            lblMonitorName.Size = new System.Drawing.Size(197, 15);
            lblMonitorName.TabIndex = 0;
            lblMonitorName.Text = "lblMonitorName";
            // 
            // label2
            // 
            label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(215, 8);
            label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(46, 15);
            label2.TabIndex = 1;
            label2.Text = "Source:";
            // 
            // cmbSources
            // 
            cmbSources.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            cmbSources.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbSources.FormattingEnabled = true;
            cmbSources.Location = new System.Drawing.Point(268, 6);
            cmbSources.Margin = new System.Windows.Forms.Padding(2);
            cmbSources.Name = "cmbSources";
            cmbSources.Size = new System.Drawing.Size(144, 23);
            cmbSources.TabIndex = 2;
            // 
            // UcSelectMonitorSource
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            Controls.Add(cmbSources);
            Controls.Add(label2);
            Controls.Add(lblMonitorName);
            Margin = new System.Windows.Forms.Padding(2);
            Name = "UcSelectMonitorSource";
            Size = new System.Drawing.Size(420, 34);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblMonitorName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cmbSources;
    }
}
