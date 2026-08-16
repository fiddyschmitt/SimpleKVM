using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SimpleKVM.Platform.win
{
    public class WindowsPlatform : IPlatform
    {
        public IDisplayPlatform Displays { get; } = new WindowsDisplayPlatform();
        public IHotkeyBackend Hotkeys { get; } = new WindowsHotkeys();
        public IIdleProvider Idle { get; } = new WindowsIdle();
        public IStartupManager? Startup { get; } = new WindowsStartupManager();

        USB.USBSystem? usb;
        public USB.USBSystem Usb => usb ??= new USB.win.USBSystem();
    }

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
            return Screen
                    .AllScreens
                    .Select(screen => new ScreenRect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Right, screen.Bounds.Bottom))
                    .ToList();
        }
    }
}
