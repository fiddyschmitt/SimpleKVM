using SimpleKVM.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        /// <summary>
        /// The desktop's own keyboard shortcuts, where the desktop acts on them before any app
        /// sees the keys (Linux: GNOME), so a hotkey on the same keys could be neither chosen
        /// nor used. Null where hotkeys don't have to share the keyboard with the desktop that way.
        /// </summary>
        IDesktopShortcuts? DesktopShortcuts => null;
    }

    public interface IDesktopShortcuts
    {
        /// <summary>
        /// For the hotkey chooser: lets the window take the whole keyboard while it is in front,
        /// so a combination the desktop would act on itself is passed to pressed instead, as a
        /// hotkey string ("Win+NumPad4"), on another thread. Made before the window is shown.
        /// Null when the keyboard can't be taken for this kind of window.
        /// </summary>
        IKeyCapture? CreateCapture(nint windowHandle, string? handleKind, Action<string> pressed);

        /// <summary>What the desktop does on the hotkey itself, a phrase per shortcut for the user; empty when nothing.</summary>
        IReadOnlyList<string> Uses(string hotkey);

        /// <summary>Takes the hotkey away from the desktop's shortcuts that have it, which keep their other keys. Throws when that fails.</summary>
        void Free(string hotkey);
    }

    /// <summary>The keyboard, taken between Start and Stop. Dispose gives it back for good.</summary>
    public interface IKeyCapture : IDisposable
    {
        void Start();

        void Stop();
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
        /// Ids an earlier version of the app gave monitors that are connected now, mapped to the
        /// ids the same monitors have now, wherever the two differ. Rules saved by that version
        /// are moved over at startup (Rules.MonitorIdMigration). Empty where ids never changed.
        /// </summary>
        IReadOnlyDictionary<string, string> EarlierMonitorIds() => new Dictionary<string, string>();

        /// <summary>
        /// Something the user should act on for monitor control to work (missing device access,
        /// an ambiguous monitor pairing), or null when there is nothing to say. Shown in the
        /// rule editor and by --list-monitors.
        /// </summary>
        string? StatusMessage => null;

        /// <summary>
        /// What the app can do about StatusMessage itself once the user allows it (Linux: give the
        /// user access to the monitors' buses), or null when there is nothing it can do. The rule
        /// editor offers it as a Fix button next to the message.
        /// </summary>
        IStatusFix? StatusFix => null;

        /// <summary>How the screen layout was determined, where that isn't simply the OS (Linux: the compositor queried).</summary>
        string? LayoutSource => null;
    }

    public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom);

    /// <summary>A problem the app can put right itself, given the user's permission.</summary>
    public interface IStatusFix
    {
        /// <summary>What the fix does, for the user to read before choosing it.</summary>
        string Description { get; }

        /// <summary>
        /// Puts it right, asking for an administrator's password the OS's own way first. Doesn't
        /// throw: the result says how it went.
        /// </summary>
        Task<StatusFixResult> RunAsync();
    }

    public enum StatusFixOutcome { Fixed, Cancelled, Failed }

    /// <summary>How a fix went, and when it failed, why, in a sentence for the user.</summary>
    public readonly record struct StatusFixResult(StatusFixOutcome Outcome, string? Message = null);

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
