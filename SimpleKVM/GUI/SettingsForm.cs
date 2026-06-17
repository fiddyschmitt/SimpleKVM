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

            chkRunAtStartup.Checked = IsStartupShortcutValid();
            chkForceInputChange.Checked = AppSettingsManager.Current.ForceInputChange;
            chkFollowSourceChanges.Checked = AppSettingsManager.Current.FollowSourceChanges;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            try
            {
                ApplyStartupSetting(chkRunAtStartup.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to update startup shortcut:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AppSettingsManager.Current.ForceInputChange = chkForceInputChange.Checked;
            AppSettingsManager.Current.FollowSourceChanges = chkFollowSourceChanges.Checked;
            AppSettingsManager.Save();

            DialogResult = DialogResult.OK;
            Close();
        }

        static bool IsStartupShortcutValid()
        {
            if (!File.Exists(ShortcutPath)) return false;

            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                try
                {
                    var shortcut = shell.CreateShortcut(ShortcutPath);
                    try
                    {
                        string targetPath = shortcut.TargetPath;
                        return string.Equals(targetPath, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
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
            catch
            {
                return false;
            }
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
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not available on this system.");
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
