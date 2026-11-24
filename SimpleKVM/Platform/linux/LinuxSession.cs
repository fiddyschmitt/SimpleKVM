using System;

namespace SimpleKVM.Platform.linux
{
    /// <summary>What kind of desktop session the app is running in, read from the environment the session gives its programs.</summary>
    public static class LinuxSession
    {
        /// <summary>
        /// True in an X11 session, where the X server itself knows the screen layout and can
        /// grab keys. In a Wayland session X is only XWayland: a guest that sees neither.
        /// </summary>
        public static bool IsX11 => IsX11Session(Environment.GetEnvironmentVariable);

        public static bool IsX11Session(Func<string, string?> environment)
        {
            switch (environment("XDG_SESSION_TYPE"))
            {
                case "x11": return true;
                case "wayland": return false;
            }

            //Not said (startx from a console, a bare window manager): a display without a compositor socket is X
            return string.IsNullOrEmpty(environment("WAYLAND_DISPLAY")) && !string.IsNullOrEmpty(environment("DISPLAY"));
        }
    }
}
