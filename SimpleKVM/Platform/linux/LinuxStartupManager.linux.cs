using System;
using System.IO;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.linux
{
    /// <summary>Run-at-startup via an XDG autostart entry (~/.config/autostart), honoured by KDE, GNOME and most desktops.</summary>
    [SupportedOSPlatform("linux")]
    public class LinuxStartupManager : IStartupManager
    {
        static string DesktopFilePath
        {
            get
            {
                var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(config))
                    config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(config, "autostart", "simplekvm.desktop");
            }
        }

        static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path");

        static string ExecLine => $"Exec={DesktopEntry.QuoteExecArgument(ExecutablePath)} {Program.StartMinimizedArg}";

        public bool IsEnabled()
        {
            try
            {
                return File.Exists(DesktopFilePath) && File.ReadAllText(DesktopFilePath).Contains(ExecLine);
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

            var entry = $"""
                [Desktop Entry]
                Type=Application
                Name=Simple KVM
                Comment=Switch monitor inputs on USB switch events or hotkeys
                {ExecLine}
                Terminal=false
                X-GNOME-Autostart-enabled=true

                """;

            Directory.CreateDirectory(Path.GetDirectoryName(DesktopFilePath)!);
            Extensions.WriteTextFile(DesktopFilePath, entry);
        }
    }
}
