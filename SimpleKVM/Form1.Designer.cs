namespace SimpleKVM
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            btnNewRule = new System.Windows.Forms.Button();
            statsTimer = new System.Windows.Forms.Timer(components);
            panel1 = new System.Windows.Forms.Panel();
            notifyIcon1 = new System.Windows.Forms.NotifyIcon(components);
            SuspendLayout();
            // 
            // btnNewRule
            // 
            btnNewRule.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnNewRule.Location = new System.Drawing.Point(8, 289);
            btnNewRule.Margin = new System.Windows.Forms.Padding(2);
            btnNewRule.Name = "btnNewRule";
            btnNewRule.Size = new System.Drawing.Size(75, 23);
            btnNewRule.TabIndex = 2;
            btnNewRule.Text = "New rule";
            btnNewRule.UseVisualStyleBackColor = true;
            btnNewRule.Click += BtnNewRule_Click;
            // 
            // statsTimer
            // 
            statsTimer.Interval = 1000;
            statsTimer.Tick += StatsTimer_Tick;
            // 
            // panel1
            // 
            panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel1.Location = new System.Drawing.Point(8, 7);
            panel1.Margin = new System.Windows.Forms.Padding(2);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(680, 275);
            panel1.TabIndex = 3;
            panel1.Paint += Panel1_Paint;
            // 
            // notifyIcon1
            // 
            notifyIcon1.Text = "notifyIcon1";
            notifyIcon1.Visible = true;
            notifyIcon1.DoubleClick += NotifyIcon1_DoubleClick;
            notifyIcon1.MouseUp += NotifyIcon1_MouseUp;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(696, 320);
            Controls.Add(panel1);
            Controls.Add(btnNewRule);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(2);
            Name = "Form1";
            Text = "Simple KVM";
            FormClosing += Form1_FormClosing;
            Resize += Form1_Resize;
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Button btnNewRule;
        private System.Windows.Forms.Timer statsTimer;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.NotifyIcon notifyIcon1;
    }
}

