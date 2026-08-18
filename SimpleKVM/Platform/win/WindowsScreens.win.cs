using SimpleKVM.Displays;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.win
{
    /// <summary>
    /// A screen as the OS lays it out: bounds in the virtual desktop plus the GDI device name.
    /// </summary>
    public sealed record WindowsScreen(string DeviceName, int Left, int Top, int Right, int Bottom)
    {
        public string UniqueId => MonitorIdentity.FromBounds(Left, Top, Right, Bottom);
    }

    /// <summary>
    /// Enumerates the desktop's screens via EnumDisplayMonitors / GetMonitorInfo
    /// in the same order Windows itself numbers displays.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public static class WindowsScreens
    {
        public static unsafe List<WindowsScreen> All()
        {
            var screens = new List<WindowsScreen>();

            PInvoke.EnumDisplayMonitors(HDC.Null, (RECT*)null, (hMonitor, hdc, rect, lparam) =>
            {
                var info = new MONITORINFOEXW();
                info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);

                if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&info))
                {
                    var bounds = info.monitorInfo.rcMonitor;
                    screens.Add(new WindowsScreen(info.szDevice.ToString(), bounds.left, bounds.top, bounds.right, bounds.bottom));
                }

                return true;
            }, default);

            return screens;
        }

        /// <summary>
        /// 1-based monitor number in left-to-right, top-to-bottom order — the numbering shown in
        /// the monitor layout and used by MonitorOverrides in config.json.
        /// </summary>
        public static int ScreenNumber(this WindowsScreen screen, IReadOnlyList<WindowsScreen> allScreens)
        {
            return allScreens
                    .OrderBy(scr => scr.Left)
                    .ThenBy(scr => scr.Top)
                    .ThenBy(scr => scr.DeviceName)
                    .Select((scr, index) => (scr.DeviceName, Number: index + 1))
                    .First(entry => entry.DeviceName == screen.DeviceName)
                    .Number;
        }
    }
}
