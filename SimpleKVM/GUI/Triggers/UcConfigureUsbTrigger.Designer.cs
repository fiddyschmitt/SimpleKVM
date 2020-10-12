namespace SimpleKVM.GUI.Triggers
{
    partial class UcConfigureUsbTrigger
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
            this.linkChooseUsbDevice = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // linkChooseUsbDevice
            // 
            this.linkChooseUsbDevice.AutoSize = true;
            this.linkChooseUsbDevice.Location = new System.Drawing.Point(0, 0);
            this.linkChooseUsbDevice.Name = "linkChooseUsbDevice";
            this.linkChooseUsbDevice.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.linkChooseUsbDevice.Size = new System.Drawing.Size(515, 35);
            this.linkChooseUsbDevice.TabIndex = 6;
            this.linkChooseUsbDevice.TabStop = true;
            this.linkChooseUsbDevice.Text = "Whenever {this} USB device is {verb}, set the monitor sources to:";
            this.linkChooseUsbDevice.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkChooseUsbDevice_LinkClicked);
            // 
            // UcConfigureUsbTrigger
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.linkChooseUsbDevice);
            this.Name = "UcConfigureUsbTrigger";
            this.Size = new System.Drawing.Size(521, 47);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.LinkLabel linkChooseUsbDevice;
    }
}
