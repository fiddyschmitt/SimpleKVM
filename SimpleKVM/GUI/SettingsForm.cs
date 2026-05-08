using SimpleKVM.Configuration;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimpleKVM.GUI
{
    public partial class SettingsForm : Form
    {
        static readonly string StartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        static readonly string ShortcutPath = Path.Combine(StartupFolder, "SimpleKVM.lnk");

        public SettingsForm()
        {
            InitializeComponent();

            chkRunAtStartup.Checked = File.Exists(ShortcutPath);
            chkForceInputChange.Checked = AppSettingsManager.Current.ForceInputChange;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            ApplyStartupSetting(chkRunAtStartup.Checked);

            AppSettingsManager.Current.ForceInputChange = chkForceInputChange.Checked;
            AppSettingsManager.Save();

            DialogResult = DialogResult.OK;
            Close();
        }

        static void ApplyStartupSetting(bool enable)
        {
            if (enable)
            {
                CreateStartupShortcut();
            }
            else if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
        }

        static void CreateStartupShortcut()
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                var shortcut = shell.CreateShortcut(ShortcutPath);
                try
                {
                    shortcut.TargetPath = Application.ExecutablePath;
                    shortcut.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    shortcut.WindowStyle = 7; // WshWindowStyle.Minimized
                    shortcut.Save();
                }
                finally
                {
                    Marshal.ReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }
}
