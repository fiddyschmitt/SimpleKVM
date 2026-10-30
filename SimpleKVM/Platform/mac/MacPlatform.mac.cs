using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.mac
{
    [SupportedOSPlatform("macos")]
    public class MacPlatform : IPlatform
    {
        public IDisplayPlatform Displays { get; } = new MacDisplayPlatform();
        public IHotkeyBackend Hotkeys { get; } = new Input.mac.MacHotkeys();
        public IIdleProvider Idle { get; } = new Utilities.mac.MacIdle();
        public IStartupManager? Startup { get; } = new MacStartupManager();

        USB.USBSystem? usb;
        public USB.USBSystem Usb => usb ??= new USB.mac.USBSystem();
    }

    [SupportedOSPlatform("macos")]
    class MacDisplayPlatform : IDisplayPlatform
    {
        public IList<SimpleKVM.Displays.Monitor> GetMonitors()
        {
            return SimpleKVM.Displays.mac.DisplaySystem
                    .GetMonitors()
                    .Cast<SimpleKVM.Displays.Monitor>()
                    .ToList();
        }

        public void InvalidateMonitors()
        {
            SimpleKVM.Displays.mac.DisplaySystem.InvalidateMonitors();
        }

        public Dictionary<string, int> GetCurrentSources()
        {
            return SimpleKVM.Displays.mac.DisplaySystem.GetCurrentSources();
        }

        /// <summary>In pixels, like the monitor ids, so the rule editor's layout finds each monitor by its id.</summary>
        public List<ScreenRect> GetScreenBounds()
        {
            return SimpleKVM.Displays.mac.DisplaySystem
                    .Screens()
                    .Select(screen => screen.Pixels)
                    .ToList();
        }

        /// <summary>Before 2.5.0 the ids were made from the layout in points; see MacPixelLayout.</summary>
        public IReadOnlyDictionary<string, string> EarlierMonitorIds()
        {
            return SimpleKVM.Displays.mac.MacPixelLayout.EarlierIds(SimpleKVM.Displays.mac.DisplaySystem.Screens());
        }
    }
}
