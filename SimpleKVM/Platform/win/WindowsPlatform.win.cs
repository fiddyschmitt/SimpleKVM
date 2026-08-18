using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.win
{
    [SupportedOSPlatform("windows6.1")]
    public class WindowsPlatform : IPlatform
    {
        public IDisplayPlatform Displays { get; } = new WindowsDisplayPlatform();
        public IHotkeyBackend Hotkeys { get; } = new WindowsHotkeys();
        public IIdleProvider Idle { get; } = new WindowsIdle();
        public IStartupManager? Startup { get; } = new WindowsStartupManager();

        USB.USBSystem? usb;
        public USB.USBSystem Usb => usb ??= new USB.win.USBSystem();
    }

    [SupportedOSPlatform("windows6.1")]
    class WindowsDisplayPlatform : IDisplayPlatform
    {
        public IList<SimpleKVM.Displays.Monitor> GetMonitors()
        {
            return SimpleKVM.Displays.win.DisplaySystem
                    .GetMonitors()
                    .Cast<SimpleKVM.Displays.Monitor>()
                    .ToList();
        }

        public Dictionary<string, int> GetCurrentSources()
        {
            return SimpleKVM.Displays.win.DisplaySystem.GetCurrentSources();
        }

        public List<ScreenRect> GetScreenBounds()
        {
            return WindowsScreens
                    .All()
                    .Select(screen => new ScreenRect(screen.Left, screen.Top, screen.Right, screen.Bottom))
                    .ToList();
        }
    }
}
