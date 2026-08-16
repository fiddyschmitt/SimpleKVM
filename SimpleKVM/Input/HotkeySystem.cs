using SimpleKVM.Platform;
using System;

namespace SimpleKVM.Input
{
    /// <summary>
    /// Facade over the current platform's hotkey backend.
    /// </summary>
    public static class HotkeySystem
    {
        /// <summary>Registers a system-wide hotkey; throws when the string cannot be parsed or the hotkey is taken.</summary>
        public static IDisposable Register(string hotkeyAsString, Action action)
        {
            var gesture = HotkeyGesture.Parse(hotkeyAsString);
            return PlatformServices.Current.Hotkeys.Register(gesture, action);
        }

        /// <summary>True when the hotkey parses and is not currently taken by another application.</summary>
        public static bool IsAvailable(string hotkeyAsString)
        {
            try
            {
                using var registration = Register(hotkeyAsString, () => { });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
