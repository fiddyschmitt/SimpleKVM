using System;
using System.IO;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.linux
{
    public enum MenuEntryState
    {
        /// <summary>No entry: the desktop doesn't know the app.</summary>
        Absent,
        /// <summary>An entry that stays out of the menu, kept so the desktop knows the app's name, icon and identity.</summary>
        Hidden,
        Shown,
    }

    /// <summary>
    /// The app's entry in the desktop's applications menu: a .desktop file under
    /// $XDG_DATA_HOME/applications and an icon in the user's hicolor theme. Without it the app
    /// can't be found in the menu, its windows have no icon in the dock, and on GNOME (no tray
    /// icons) there is nothing to launch again to get the window back. Writing it is the
    /// installer's job on Linux (install.sh does), or the user's, through Settings; an app
    /// that put itself in the menu unasked would be overstepping. The portals need the file
    /// too, since they look the app up by its name, so where they are in use it is written
    /// hidden, which leaves the menu alone.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class LinuxMenuEntry : IMenuEntry
    {
        const string IconResource = "simplekvm.png";

        static string DataHome
        {
            get
            {
                var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (string.IsNullOrEmpty(dataHome))
                    dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
                return dataHome;
            }
        }

        public static string DesktopFilePath => Path.Combine(DataHome, "applications", DesktopEntry.FileName);

        public static string IconFilePath => Path.Combine(DataHome, "icons", "hicolor", "256x256", "apps", DesktopEntry.AppId + ".png");

        static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path");

        public bool IsShown() => State == MenuEntryState.Shown;

        public void SetShown(bool shown) => Write(shown);

        public static MenuEntryState State
        {
            get
            {
                try
                {
                    if (!File.Exists(DesktopFilePath)) return MenuEntryState.Absent;

                    var noDisplay = DesktopEntry.ReadKey(File.ReadAllText(DesktopFilePath), "NoDisplay");
                    return string.Equals(noDisplay, "true", StringComparison.OrdinalIgnoreCase) ? MenuEntryState.Hidden : MenuEntryState.Shown;
                }
                catch
                {
                    return MenuEntryState.Absent;
                }
            }
        }

        /// <summary>Writes the entry for this executable, and the icon it names.</summary>
        public static void Write(bool shown)
        {
            WriteIcon();

            Directory.CreateDirectory(Path.GetDirectoryName(DesktopFilePath)!);
            Extensions.WriteTextFile(DesktopFilePath, DesktopEntry.Launcher(ExecutablePath, shown));
        }

        /// <summary>Removes the entry and the icon: what an uninstall does.</summary>
        public static void Remove()
        {
            if (File.Exists(DesktopFilePath)) File.Delete(DesktopFilePath);
            if (File.Exists(IconFilePath)) File.Delete(IconFilePath);
        }

        /// <summary>
        /// When the app starts (the app proper, not one of its diagnostic commands): keeps an
        /// entry that exists in working order. It is rewritten only when the program it points
        /// at is gone, which is what moving or renaming the executable looks like; one pointing
        /// at another copy that still exists is left alone, since that copy is the installed
        /// one and this one is just being run. No entry is written where there is none.
        /// </summary>
        public static void OnAppStart()
        {
            try
            {
                var state = State;
                if (state == MenuEntryState.Absent) return;

                var recorded = DesktopEntry.ReadKey(File.ReadAllText(DesktopFilePath), DesktopEntry.ExecutableKey);
                if (recorded != ExecutablePath && (recorded == null || !File.Exists(recorded)))
                {
                    Write(shown: state == MenuEntryState.Shown);
                }
                else if (!File.Exists(IconFilePath))
                {
                    WriteIcon();
                }
            }
            catch (Exception ex)
            {
                //A read-only home or an odd XDG_DATA_HOME must not keep the app from starting
                Console.WriteLine($"Could not update the applications menu entry: {ex.Message}");
            }
        }

        /// <summary>For the portals: the desktop must be able to look the app up, whether or not the user wants it in the menu.</summary>
        public static void EnsureExists()
        {
            try
            {
                if (State == MenuEntryState.Absent) Write(shown: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not write the applications menu entry: {ex.Message}");
            }
        }

        static void WriteIcon()
        {
            using var icon = typeof(LinuxMenuEntry).Assembly.GetManifestResourceStream(IconResource);
            if (icon == null) return;

            if (File.Exists(IconFilePath) && new FileInfo(IconFilePath).Length == icon.Length) return;

            Directory.CreateDirectory(Path.GetDirectoryName(IconFilePath)!);

            //Written whole under another name, then moved into place: a desktop scanning the folder never sees half an icon
            var temporary = IconFilePath + ".new";
            using (var file = File.Create(temporary))
            {
                icon.CopyTo(file);
            }
            File.Move(temporary, IconFilePath, overwrite: true);
        }
    }
}
