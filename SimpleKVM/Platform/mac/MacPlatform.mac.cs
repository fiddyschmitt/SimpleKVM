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

        public Dictionary<string, int> GetCurrentSources()
        {
            return SimpleKVM.Displays.mac.DisplaySystem.GetCurrentSources();
        }

        public List<ScreenRect> GetScreenBounds()
        {
            return SimpleKVM.Displays.mac.CoreGraphicsNative
                    .GetActiveDisplays()
                    .Select(id =>
                    {
                        var bounds = SimpleKVM.Displays.mac.CoreGraphicsNative.CGDisplayBounds(id);
                        return new ScreenRect(
                            (int)System.Math.Round(bounds.X),
                            (int)System.Math.Round(bounds.Y),
                            (int)System.Math.Round(bounds.X + bounds.Width),
                            (int)System.Math.Round(bounds.Y + bounds.Height));
                    })
                    .ToList();
        }
    }
}
