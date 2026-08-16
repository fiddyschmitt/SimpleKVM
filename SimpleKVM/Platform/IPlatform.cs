using SimpleKVM.Input;
using System;
using System.Collections.Generic;

namespace SimpleKVM.Platform
{
    /// <summary>
    /// Everything the app needs from the operating system, grouped per subsystem.
    /// One implementation per OS (Platform\win, Platform\mac, ...); PlatformServices
    /// picks the right one at startup. Adding an OS means implementing these
    /// interfaces and adding one line to PlatformServices.Create.
    /// </summary>
    public interface IPlatform
    {
        IDisplayPlatform Displays { get; }
        IHotkeyBackend Hotkeys { get; }
        IIdleProvider Idle { get; }

        /// <summary>Run-at-startup registration. Null until the platform implements it.</summary>
        IStartupManager? Startup { get; }

        /// <summary>The USB watcher singleton for this platform. Starts watching on construction.</summary>
        USB.USBSystem Usb { get; }
    }

    public interface IDisplayPlatform
    {
        IList<Displays.Monitor> GetMonitors();

        /// <summary>Current VCP 0x60 value per MonitorUniqueId, in a single enumeration pass.</summary>
        Dictionary<string, int> GetCurrentSources();

        /// <summary>Bounds of every screen in the OS's global desktop coordinate space.</summary>
        List<ScreenRect> GetScreenBounds();
    }

    public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom);

    public interface IHotkeyBackend
    {
        /// <summary>
        /// Registers a system-wide hotkey. Throws when the gesture cannot be mapped or is
        /// already taken. Dispose the returned registration to unregister.
        /// </summary>
        IDisposable Register(HotkeyGesture gesture, Action action);
    }

    public interface IIdleProvider
    {
        TimeSpan GetIdleTimeSpan();
    }

    public interface IStartupManager
    {
        bool IsEnabled();
        void SetEnabled(bool enabled);
    }
}
