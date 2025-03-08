namespace SimpleKVM.GUI.Actions
{
    partial class UcSelectMonitorsAndSources
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
            panel1 = new System.Windows.Forms.Panel();
            BtnRefresh = new System.Windows.Forms.Button();
            ucMonitorLayout1 = new UcMonitorLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panel1.Location = new System.Drawing.Point(118, 20);
            panel1.Margin = new System.Windows.Forms.Padding(2);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(427, 181);
            panel1.TabIndex = 5;
            // 
            // BtnRefresh
            // 
            BtnRefresh.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            BtnRefresh.Location = new System.Drawing.Point(550, 20);
            BtnRefresh.Name = "BtnRefresh";
            BtnRefresh.Size = new System.Drawing.Size(75, 23);
            BtnRefresh.TabIndex = 0;
            BtnRefresh.Text = "Refresh";
            BtnRefresh.UseVisualStyleBackColor = true;
            BtnRefresh.Click += BtnRefresh_Click;
            // 
            // ucMonitorLayout1
            // 
            ucMonitorLayout1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            ucMonitorLayout1.Location = new System.Drawing.Point(3, 206);
            ucMonitorLayout1.Name = "ucMonitorLayout1";
            ucMonitorLayout1.Size = new System.Drawing.Size(656, 256);
            ucMonitorLayout1.TabIndex = 6;
            // 
            // UcSelectMonitorsAndSources
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(BtnRefresh);
            Controls.Add(ucMonitorLayout1);
            Controls.Add(panel1);
            Margin = new System.Windows.Forms.Padding(2);
            Name = "UcSelectMonitorsAndSources";
            Size = new System.Drawing.Size(662, 476);
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private UcMonitorLayout ucMonitorLayout1;
        private System.Windows.Forms.Button BtnRefresh;
    }
}
