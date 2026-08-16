using System;
using System.IO;

namespace SimpleKVM.Configuration
{
    /// <summary>
    /// Where the app's data files live. Windows keeps them next to the exe (the app is
    /// distributed as a portable folder and existing installs already have files there).
    /// macOS uses ~/Library/Application Support, because a .app bundle must not write into itself.
    /// </summary>
    public static class AppPaths
    {
        public static string DataDirectory { get; } = GetDataDirectory();

        public static string RulesFile { get; } = Path.Combine(DataDirectory, "rules.json");
        public static string SettingsFile { get; } = Path.Combine(DataDirectory, "settings.json");
        public static string ConfigFile { get; } = Path.Combine(DataDirectory, "config.json");

        static string GetDataDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }

            if (OperatingSystem.IsMacOS())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var dir = Path.Combine(home, "Library", "Application Support", "SimpleKVM");
                Directory.CreateDirectory(dir);
                return dir;
            }

            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleKVM");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
