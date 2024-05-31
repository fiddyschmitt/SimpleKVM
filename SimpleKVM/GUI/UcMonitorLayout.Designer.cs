namespace SimpleKVM.GUI
{
    partial class UcMonitorLayout
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
            monitorDrawer = new Drawing.Rendering.GDIDrawer();
            SuspendLayout();
            // 
            // gdiDrawer1
            // 
            monitorDrawer.AllowDragging = true;
            monitorDrawer.DrawArrowheads = true;
            monitorDrawer.Location = new System.Drawing.Point(3, 3);
            monitorDrawer.Name = "gdiDrawer1";
            monitorDrawer.Size = new System.Drawing.Size(650, 250);
            monitorDrawer.TabIndex = 0;
            // 
            // UcMonitorLayout
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(monitorDrawer);
            Name = "UcMonitorLayout";
            Size = new System.Drawing.Size(656, 256);
            Load += UcMonitorLayout_Load;
            ResumeLayout(false);
        }

        #endregion

        private Drawing.Rendering.GDIDrawer monitorDrawer;
    }
}
