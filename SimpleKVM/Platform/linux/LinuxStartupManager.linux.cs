using System;
using System.IO;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.linux
{
    /// <summary>
    /// Run-at-startup via an XDG autostart entry (~/.config/autostart), honoured by KDE, GNOME
    /// and most desktops. The entry is named after the application id, like the launcher: a
    /// desktop that starts it puts the app in a scope named after the file, and the portals
    /// take the app's identity from that scope's name.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class LinuxStartupManager : IStartupManager
    {
        static string AutostartDirectory
        {
            get
            {
                var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(config))
                    config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(config, "autostart");
            }
        }

        static string DesktopFilePath => Path.Combine(AutostartDirectory, DesktopEntry.FileName);

        static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path");

        static string Entry => DesktopEntry.Autostart(ExecutablePath, Program.StartMinimizedArg);

        public bool IsEnabled()
        {
            try
            {
                return File.Exists(DesktopFilePath) && File.ReadAllText(DesktopFilePath) == Entry;
            }
            catch
            {
                return false;
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                if (File.Exists(DesktopFilePath)) File.Delete(DesktopFilePath);
                return;
            }

            Directory.CreateDirectory(AutostartDirectory);
            Extensions.WriteTextFile(DesktopFilePath, Entry);

            //The copy the desktop starts at login is known to it by this name, so the name has to lead somewhere
            LinuxMenuEntry.EnsureExists();
        }
    }
}
