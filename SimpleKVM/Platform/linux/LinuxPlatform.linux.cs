using SimpleKVM.Input.linux;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace SimpleKVM.Platform.linux
{
    [SupportedOSPlatform("linux")]
    public class LinuxPlatform : IPlatform
    {
        public IDisplayPlatform Displays { get; } = new LinuxDisplayPlatform();
        public IHotkeyBackend Hotkeys { get; } = new LinuxHotkeys();
        public IIdleProvider Idle { get; } = new LinuxIdle();
        public IStartupManager? Startup { get; } = new LinuxStartupManager();

        USB.USBSystem? usb;
        public USB.USBSystem Usb => usb ??= new USB.linux.USBSystem();
    }

    [SupportedOSPlatform("linux")]
    class LinuxDisplayPlatform : IDisplayPlatform
    {
        public IList<SimpleKVM.Displays.Monitor> GetMonitors()
        {
            return SimpleKVM.Displays.linux.DisplaySystem
                    .GetMonitors()
                    .Cast<SimpleKVM.Displays.Monitor>()
                    .ToList();
        }

        public void InvalidateMonitors() => SimpleKVM.Displays.linux.DisplaySystem.InvalidateMonitors();

        public Dictionary<string, int> GetCurrentSources() => SimpleKVM.Displays.linux.DisplaySystem.GetCurrentSources();

        public List<ScreenRect> GetScreenBounds() => SimpleKVM.Displays.linux.DisplaySystem.GetScreenBounds();
    }

    /// <summary>
    /// Idle time from evdev when the input devices are readable (any desktop, Wayland included).
    /// Otherwise GNOME's Mutter IdleMonitor or the freedesktop ScreenSaver interface over D-Bus.
    /// </summary>
    [SupportedOSPlatform("linux")]
    class LinuxIdle : IIdleProvider
    {
        public TimeSpan GetIdleTimeSpan()
        {
            if (EvdevInput.Instance.ReadableDeviceCount > 0)
                return EvdevInput.Instance.IdleTime;

            var ms = QueryDbus("--dest org.gnome.Mutter.IdleMonitor --object-path /org/gnome/Mutter/IdleMonitor/Core --method org.gnome.Mutter.IdleMonitor.GetIdletime")
                  ?? QueryDbus("--dest org.freedesktop.ScreenSaver --object-path /org/freedesktop/ScreenSaver --method org.freedesktop.ScreenSaver.GetSessionIdleTime");

            return ms != null ? TimeSpan.FromMilliseconds(ms.Value) : TimeSpan.Zero;
        }

        static readonly HashSet<string> unsupported = [];

        static long? QueryDbus(string args)
        {
            if (unsupported.Contains(args)) return null;

            try
            {
                using var process = Process.Start(new ProcessStartInfo("gdbus", "call --session " + args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });
                if (process == null) return null;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);

                var match = Regex.Match(output, @"(\d+)");
                if (process.ExitCode == 0 && match.Success) return long.Parse(match.Value);
            }
            catch
            {
            }

            unsupported.Add(args);
            return null;
        }
    }
}
