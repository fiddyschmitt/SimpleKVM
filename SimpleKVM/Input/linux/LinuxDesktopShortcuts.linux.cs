using SimpleKVM.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// The desktop's own shortcuts on Linux: the keyboard taken for the hotkey chooser on any X
    /// display (XWayland included), and GNOME's shortcut settings read and freed. Other desktops
    /// have no settings to read here yet, so for them only the capture applies.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed class LinuxDesktopShortcuts : IDesktopShortcuts
    {
        public IKeyCapture? CreateCapture(nint windowHandle, string? handleKind, Action<string> pressed)
        {
            //Avalonia's windows are X windows ("XID") on Linux, under XWayland in a Wayland session
            return handleKind == "XID" && windowHandle != 0 ? X11KeyCapture.TryCreate(unchecked((uint)windowHandle), pressed) : null;
        }

        public IReadOnlyList<string> Uses(string hotkey)
        {
            try
            {
                return GnomeShortcuts.Find(HotkeyGesture.Parse(hotkey)).Select(use => use.Description).Distinct().ToList();
            }
            catch (ArgumentException)
            {
                return [];
            }
        }

        public void Free(string hotkey) => GnomeShortcuts.Free(HotkeyGesture.Parse(hotkey));
    }
}
