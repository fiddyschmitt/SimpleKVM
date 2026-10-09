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

        /// <summary>
        /// The app's entry in the applications menu, on a platform where the app has to put it
        /// there itself (Linux). Null where the installer or the OS takes care of it.
        /// </summary>
        IMenuEntry? MenuEntry => null;
    }

    public interface IDisplayPlatform
    {
        IList<Displays.Monitor> GetMonitors();

        /// <summary>Drops the monitor cache so the next GetMonitors re-enumerates and re-probes every monitor.</summary>
        void InvalidateMonitors();

        /// <summary>Current VCP 0x60 value per MonitorUniqueId, in a single enumeration pass.</summary>
        Dictionary<string, int> GetCurrentSources();

        /// <summary>Bounds of every screen in the OS's global desktop coordinate space.</summary>
        List<ScreenRect> GetScreenBounds();

        /// <summary>
        /// Something the user should act on for monitor control to work (missing device access,
        /// an ambiguous monitor pairing), or null when there is nothing to say. Shown in the
        /// rule editor and by --list-monitors.
        /// </summary>
        string? StatusMessage => null;

        /// <summary>How the screen layout was determined, where that isn't simply the OS (Linux: the compositor queried).</summary>
        string? LayoutSource => null;
    }

    public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom);

    public interface IHotkeyBackend
    {
        /// <summary>
        /// Registers a system-wide hotkey. Throws when the gesture cannot be mapped or is
        /// already taken. Dispose the returned registration to unregister.
        /// </summary>
        IDisposable Register(HotkeyGesture gesture, Action action);

        /// <summary>
        /// Something the user should know about how hotkeys behave or why they can't be used
        /// here (Linux: the desktop confirms them, or access is missing), or null when there is
        /// nothing to say. Shown where a hotkey is chosen.
        /// </summary>
        string? Note => null;

        /// <summary>Which of several ways of getting hotkeys is in use, where there is more than one (Linux).</summary>
        string? BackendName => null;
    }

    public interface IIdleProvider
    {
        TimeSpan GetIdleTimeSpan();

        /// <summary>
        /// Why idle time can't be determined on this machine (Linux without input-device access
        /// on a desktop that doesn't report it), or null when it can. Known after the first call.
        /// </summary>
        string? StatusMessage => null;

        /// <summary>Where the idle time comes from, where there is more than one possibility (Linux). Known after the first call.</summary>
        string? SourceName => null;
    }

    public interface IStartupManager
    {
        bool IsEnabled();
        void SetEnabled(bool enabled);
    }

    public interface IMenuEntry
    {
        bool IsShown();

        /// <summary>Shows the app in the applications menu, or takes it out of it. Throws when the entry can't be written.</summary>
        void SetShown(bool shown);
    }
}
