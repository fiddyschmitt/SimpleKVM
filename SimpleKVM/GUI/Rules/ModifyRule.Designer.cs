namespace SimpleKVM.GUI.Rules
{
    partial class ModifyRule
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            btnSave = new System.Windows.Forms.Button();
            errorProvider1 = new System.Windows.Forms.ErrorProvider(components);
            label1 = new System.Windows.Forms.Label();
            txtRuleName = new System.Windows.Forms.TextBox();
            btnTest = new System.Windows.Forms.Button();
            label2 = new System.Windows.Forms.Label();
            lblDelay = new System.Windows.Forms.Label();
            nudDelay = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)errorProvider1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDelay).BeginInit();
            SuspendLayout();
            // 
            // btnSave
            // 
            btnSave.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnSave.Location = new System.Drawing.Point(12, 150);
            btnSave.Margin = new System.Windows.Forms.Padding(2);
            btnSave.Name = "btnSave";
            btnSave.Size = new System.Drawing.Size(75, 23);
            btnSave.TabIndex = 3;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += BtnSave_Click;
            // 
            // errorProvider1
            // 
            errorProvider1.ContainerControl = this;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(8, 10);
            label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(65, 15);
            label1.TabIndex = 0;
            label1.Text = "Rule Name";
            // 
            // txtRuleName
            // 
            txtRuleName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtRuleName.Location = new System.Drawing.Point(81, 8);
            txtRuleName.Margin = new System.Windows.Forms.Padding(2);
            txtRuleName.Name = "txtRuleName";
            txtRuleName.Size = new System.Drawing.Size(323, 23);
            txtRuleName.TabIndex = 0;
            txtRuleName.Text = "Switch to this computer";
            // 
            // btnTest
            // 
            btnTest.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnTest.Location = new System.Drawing.Point(142, 150);
            btnTest.Margin = new System.Windows.Forms.Padding(2);
            btnTest.Name = "btnTest";
            btnTest.Size = new System.Drawing.Size(75, 23);
            btnTest.TabIndex = 4;
            btnTest.Text = "Test";
            btnTest.UseVisualStyleBackColor = true;
            btnTest.Click += BtnTest_Click;
            // 
            // label2
            // 
            label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(218, 154);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(182, 15);
            label2.TabIndex = 4;
            label2.Text = "(will revert back after 10 seconds)";
            //
            // lblDelay
            //
            lblDelay.AutoSize = true;
            lblDelay.Location = new System.Drawing.Point(8, 42);
            lblDelay.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            lblDelay.Name = "lblDelay";
            lblDelay.Size = new System.Drawing.Size(65, 15);
            lblDelay.TabIndex = 5;
            lblDelay.Text = "Delay (s):";
            //
            // nudDelay
            //
            nudDelay.Location = new System.Drawing.Point(81, 39);
            nudDelay.Margin = new System.Windows.Forms.Padding(2);
            nudDelay.Maximum = new decimal(new int[] { 3600, 0, 0, 0 });
            nudDelay.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
            nudDelay.Name = "nudDelay";
            nudDelay.Size = new System.Drawing.Size(60, 23);
            nudDelay.TabIndex = 1;
            nudDelay.Value = new decimal(new int[] { 0, 0, 0, 0 });
            //
            // ModifyRule
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(424, 186);
            Controls.Add(nudDelay);
            Controls.Add(lblDelay);
            Controls.Add(label2);
            Controls.Add(btnTest);
            Controls.Add(txtRuleName);
            Controls.Add(label1);
            Controls.Add(btnSave);
            Margin = new System.Windows.Forms.Padding(2);
            Name = "ModifyRule";
            Text = "ModifyRule";
            ((System.ComponentModel.ISupportInitialize)errorProvider1).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDelay).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ErrorProvider errorProvider1;
        private System.Windows.Forms.TextBox txtRuleName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lblDelay;
        private System.Windows.Forms.NumericUpDown nudDelay;
    }
}