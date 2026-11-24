using System;
using System.Text;

namespace SimpleKVM.Platform.linux
{
    /// <summary>
    /// The text of the app's freedesktop .desktop files: its entry in the applications menu
    /// and its autostart entry. Both carry the same file name, the application id, because
    /// that name is how a desktop knows the app: it ties the app's windows to their launcher
    /// and icon, and the XDG portals keep what the user granted the app under it.
    /// </summary>
    public static class DesktopEntry
    {
        /// <summary>
        /// Reverse-DNS, as the portals require (GNOME refuses a shortcut request from an id
        /// without dots), after the project's home on GitHub, and lower case like everything
        /// else the app is called on Linux.
        /// </summary>
        public const string AppId = "io.github.fiddyschmitt.simplekvm";

        public const string FileName = AppId + ".desktop";

        /// <summary>The key the launcher entry records the raw executable path under, so a later start can tell whether it still points at a program that exists.</summary>
        public const string ExecutableKey = "X-SimpleKVM-Executable";

        /// <summary>The argument that asks the running copy to exit; the launcher entry offers it as a right-click action.</summary>
        public const string QuitArgument = "--quit";

        /// <summary>
        /// Escaping for an argument of the Exec key, which has two layers: the argument is
        /// double-quoted with backslash escapes for the shell-like parser, and the whole value
        /// is then a "string" in which every backslash is itself escaped and every literal
        /// percent sign is doubled (single ones are field codes).
        /// </summary>
        public static string QuoteExecArgument(string argument)
        {
            var quoted = new StringBuilder("\"");
            foreach (char c in argument)
            {
                if (c is '"' or '`' or '$' or '\\') quoted.Append('\\');
                quoted.Append(c);
            }
            quoted.Append('"');

            return quoted.ToString().Replace("\\", "\\\\").Replace("%", "%%");
        }

        /// <summary>
        /// The applications-menu entry. Hidden, it stays out of the menu but still gives the
        /// desktop the app's name, icon and identity. StartupWMClass is the class Avalonia
        /// gives its X11 windows (the assembly name), which is what ties a window to this entry.
        /// </summary>
        public static string Launcher(string executablePath, bool shown)
        {
            var exec = QuoteExecArgument(executablePath);

            return $"""
                [Desktop Entry]
                Type=Application
                Name=Simple KVM
                Comment=Switch monitor inputs on USB switch events or hotkeys
                Exec={exec}
                Icon={AppId}
                Terminal=false
                Categories=Utility;
                StartupWMClass=SimpleKVM
                NoDisplay={(shown ? "false" : "true")}
                {ExecutableKey}={executablePath}
                Actions=quit;

                [Desktop Action quit]
                Name=Quit Simple KVM
                Exec={exec} {QuitArgument}

                """.ReplaceLineEndings("\n");
        }

        public static string Autostart(string executablePath, string arguments)
        {
            return $"""
                [Desktop Entry]
                Type=Application
                Name=Simple KVM
                Comment=Switch monitor inputs on USB switch events or hotkeys
                Exec={QuoteExecArgument(executablePath)} {arguments}
                Icon={AppId}
                Terminal=false
                X-GNOME-Autostart-enabled=true

                """.ReplaceLineEndings("\n");
        }

        /// <summary>The value of a key in the entry's first group, or null when it isn't there.</summary>
        public static string? ReadKey(string entryText, string key)
        {
            foreach (var rawLine in entryText.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith('[') && !line.StartsWith("[Desktop Entry]", StringComparison.Ordinal)) break;
                if (line.StartsWith(key + "=", StringComparison.Ordinal)) return line[(key.Length + 1)..];
            }

            return null;
        }
    }
}
