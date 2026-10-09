using SimpleKVM.Platform;
using SimpleKVM.Platform.linux;
using System;
using System.Runtime.Versioning;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// Global hotkeys on Linux, by whichever route the session offers, best first:
    /// <list type="bullet">
    /// <item>an X11 session: key grabs on the X server (exclusive, no permission);</item>
    /// <item>a Wayland session whose desktop portal has GlobalShortcuts (GNOME 48+, KDE
    /// Plasma 6): the portal (exclusive, no permission, confirmed by the user);</item>
    /// <item>anything else: reading /dev/input, which needs the <c>input</c> group and can't
    /// keep the keys from reaching the focused application.</item>
    /// </list>
    /// SIMPLEKVM_HOTKEYS=x11, =portal or =evdev pins one for troubleshooting.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class LinuxHotkeys : IHotkeyBackend
    {
        readonly object sync = new();
        IHotkeyBackend? backend;

        string name = "";

        /// <summary>Which route is in use: "x11", "portal" or "evdev".</summary>
        public string? BackendName
        {
            get
            {
                _ = Backend;
                return name;
            }
        }

        IHotkeyBackend Backend
        {
            get
            {
                lock (sync)
                {
                    if (backend == null) (backend, name) = Choose();
                    return backend;
                }
            }
        }

        public IDisposable Register(HotkeyGesture gesture, Action action) => Backend.Register(gesture, action);

        public string? Note => Backend.Note;

        static (IHotkeyBackend, string) Choose()
        {
            var pinned = Environment.GetEnvironmentVariable("SIMPLEKVM_HOTKEYS")?.ToLowerInvariant();

            if (pinned == "x11" || (pinned == null && LinuxSession.IsX11))
            {
                try
                {
                    return (new X11Hotkeys(), "x11");
                }
                catch (Exception ex)
                {
                    if (pinned != null) return (new NoHotkeys($"X11 hotkeys are unavailable: {ex.Message}"), "none");

                    //No X server to reach after all (or no libxcb): the routes below may still work
                    Console.WriteLine($"X11 hotkeys are unavailable: {ex.Message}");
                }
            }

            if (pinned == "portal" || (pinned == null && !LinuxSession.IsX11))
            {
                if (PortalHotkeys.TryCreate() is PortalHotkeys portal) return (portal, "portal");
                if (pinned == "portal") return (new NoHotkeys("This desktop's portal has no GlobalShortcuts interface."), "none");
            }

            return (new EvdevHotkeys(), "evdev");
        }

        /// <summary>The route that was asked for by name and isn't there.</summary>
        sealed class NoHotkeys(string reason) : IHotkeyBackend
        {
            public IDisposable Register(HotkeyGesture gesture, Action action) => throw new InvalidOperationException(reason);

            public string? Note => reason;
        }
    }
}
