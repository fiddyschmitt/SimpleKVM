using SimpleKVM.Input.linux;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.linux
{
    [SupportedOSPlatform("linux")]
    public class LinuxPlatform : IPlatform
    {
        public IDisplayPlatform Displays { get; } = new LinuxDisplayPlatform();
        public IHotkeyBackend Hotkeys { get; } = new LinuxHotkeys();
        public IIdleProvider Idle { get; } = new Utilities.linux.LinuxIdle();
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
}
