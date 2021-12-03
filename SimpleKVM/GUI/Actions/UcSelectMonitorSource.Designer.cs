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
            this.lblMonitorName = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.cmbSources = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // lblMonitorName
            // 
            this.lblMonitorName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMonitorName.Location = new System.Drawing.Point(14, 8);
            this.lblMonitorName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblMonitorName.Name = "lblMonitorName";
            this.lblMonitorName.Size = new System.Drawing.Size(197, 15);
            this.lblMonitorName.TabIndex = 0;
            this.lblMonitorName.Text = "lblMonitorName";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(215, 8);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 15);
            this.label2.TabIndex = 1;
            this.label2.Text = "Source:";
            // 
            // cmbSources
            // 
            this.cmbSources.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbSources.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSources.FormattingEnabled = true;
            this.cmbSources.Location = new System.Drawing.Point(268, 6);
            this.cmbSources.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cmbSources.Name = "cmbSources";
            this.cmbSources.Size = new System.Drawing.Size(144, 23);
            this.cmbSources.TabIndex = 2;
            // 
            // UcSelectMonitorSource
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.cmbSources);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.lblMonitorName);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "UcSelectMonitorSource";
            this.Size = new System.Drawing.Size(420, 34);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblMonitorName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cmbSources;
    }
}
