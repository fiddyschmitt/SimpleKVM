namespace SimpleKVM.GUI
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            chkRunAtStartup = new System.Windows.Forms.CheckBox();
            chkForceInputChange = new System.Windows.Forms.CheckBox();
            lblForceInputChangeHint = new System.Windows.Forms.Label();
            btnOK = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // chkRunAtStartup
            // 
            chkRunAtStartup.AutoSize = true;
            chkRunAtStartup.Location = new System.Drawing.Point(20, 20);
            chkRunAtStartup.Name = "chkRunAtStartup";
            chkRunAtStartup.Size = new System.Drawing.Size(101, 19);
            chkRunAtStartup.TabIndex = 0;
            chkRunAtStartup.Text = "Run at Startup";
            chkRunAtStartup.UseVisualStyleBackColor = true;
            // 
            // chkForceInputChange
            // 
            chkForceInputChange.AutoSize = true;
            chkForceInputChange.Location = new System.Drawing.Point(20, 55);
            chkForceInputChange.Name = "chkForceInputChange";
            chkForceInputChange.Size = new System.Drawing.Size(128, 19);
            chkForceInputChange.TabIndex = 1;
            chkForceInputChange.Text = "Force input change";
            chkForceInputChange.UseVisualStyleBackColor = true;
            // 
            // lblForceInputChangeHint
            // 
            lblForceInputChangeHint.AutoSize = true;
            lblForceInputChangeHint.ForeColor = System.Drawing.SystemColors.GrayText;
            lblForceInputChangeHint.Location = new System.Drawing.Point(38, 77);
            lblForceInputChangeHint.Name = "lblForceInputChangeHint";
            lblForceInputChangeHint.Size = new System.Drawing.Size(403, 15);
            lblForceInputChangeHint.TabIndex = 2;
            lblForceInputChangeHint.Text = "Switch input even if the monitor reports it is already on the selected source.";
            // 
            // btnOK
            // 
            btnOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnOK.Location = new System.Drawing.Point(289, 115);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(75, 23);
            btnOK.TabIndex = 2;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += BtnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(370, 115);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 23);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            // 
            // SettingsForm
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(458, 151);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(lblForceInputChangeHint);
            Controls.Add(chkForceInputChange);
            Controls.Add(chkRunAtStartup);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SettingsForm";
            Text = "Settings";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.CheckBox chkRunAtStartup;
        private System.Windows.Forms.CheckBox chkForceInputChange;
        private System.Windows.Forms.Label lblForceInputChangeHint;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}
