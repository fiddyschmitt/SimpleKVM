using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SimpleKVM.Platform.win
{
    /// <summary>
    /// Run-at-startup via a shortcut in the user's Startup folder, created through the
    /// WScript.Shell COM object (no extra package needed for .lnk writing).
    /// </summary>
    public class WindowsStartupManager : IStartupManager
    {
        static readonly string StartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        static readonly string ShortcutPath = Path.Combine(StartupFolder, "SimpleKVM.lnk");

        static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path");

        public bool IsEnabled()
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
                        return string.Equals(targetPath, ExecutablePath, StringComparison.OrdinalIgnoreCase);
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

        public void SetEnabled(bool enabled)
        {
            if (enabled)
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
                    shortcut.TargetPath = ExecutablePath;
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
