using SimpleKVM.Platform;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// The whole keyboard for one window while the hotkey chooser is in front, so combinations
    /// the desktop keeps for itself reach the chooser too. GNOME acts on its own shortcuts (and
    /// its extensions', such as Ubuntu's Tiling Assistant on Win with the numpad keys) before
    /// any app sees the keys. An X client that grabs the keyboard gets them instead once it has
    /// told the window manager that its window may: the _XWAYLAND_MAY_GRAB_KEYBOARD client
    /// message, sent to the root window, which is how remote-desktop and virtual machine viewers
    /// take Win+anything under GNOME on Wayland (a window property of that name is ignored).
    /// On an X11 session the grab alone does it, outranking the desktop's key grabs. The grab is
    /// on a connection of its own (the window is Avalonia's), so the presses arrive here, are
    /// read every few milliseconds and passed on as hotkey strings; Super+Escape, GNOME's way of
    /// taking its shortcuts back from any app, still works.
    /// </summary>
    [SupportedOSPlatform("linux")]
    sealed class X11KeyCapture : IKeyCapture
    {
        const string Xcb = "libxcb.so.1";

        const byte KeyPressEvent = 2, ClientMessageEvent = 33, MappingNotifyEvent = 34, MappingKeyboard = 1;
        const byte GrabModeAsync = 1, GrabSuccess = 0;
        const uint SubstructureNotifyMask = 1 << 19, SubstructureRedirectMask = 1 << 20;

        static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

        //The window manager reads the message on its X connection, while the grab reaches it from
        //XWayland by another way, so the grab waits a few ticks for the message to be read first
        const int TicksBeforeGrab = 8;

        //The window may not be on the screen yet when it is first activated; it is tried again
        //on each tick for this long, after which the chooser's own key events are all there is
        const int GrabAttempts = 100;

        [DllImport(Xcb)] static extern nint xcb_connect(nint displayName, out int preferredScreen);
        [DllImport(Xcb)] static extern int xcb_connection_has_error(nint connection);
        [DllImport(Xcb)] static extern void xcb_disconnect(nint connection);
        [DllImport(Xcb)] static extern nint xcb_get_setup(nint connection);
        [DllImport(Xcb)] static extern ScreenIterator xcb_setup_roots_iterator(nint setup);
        [DllImport(Xcb)] static extern void xcb_screen_next(ref ScreenIterator iterator);
        [DllImport(Xcb)] static extern int xcb_flush(nint connection);
        [DllImport(Xcb)] static extern nint xcb_poll_for_event(nint connection);
        [DllImport(Xcb)] static extern uint xcb_intern_atom(nint connection, byte onlyIfExists, ushort nameLength, byte[] name);
        [DllImport(Xcb)] static extern nint xcb_intern_atom_reply(nint connection, uint cookie, nint error);
        [DllImport(Xcb)] static extern uint xcb_send_event(nint connection, byte propagate, uint destination, uint eventMask, byte[] evt);
        [DllImport(Xcb)] static extern uint xcb_grab_keyboard(nint connection, byte ownerEvents, uint grabWindow, uint time, byte pointerMode, byte keyboardMode);
        [DllImport(Xcb)] static extern nint xcb_grab_keyboard_reply(nint connection, uint cookie, nint error);
        [DllImport(Xcb)] static extern uint xcb_ungrab_keyboard(nint connection, uint time);
        [DllImport(Xcb)] static extern uint xcb_get_keyboard_mapping(nint connection, byte firstKeycode, byte count);
        [DllImport(Xcb)] static extern nint xcb_get_keyboard_mapping_reply(nint connection, uint cookie, nint error);

        [StructLayout(LayoutKind.Sequential)]
        struct ScreenIterator
        {
            public nint Data;
            public int Remaining;
            public int Index;
        }

        readonly nint connection;
        readonly uint window, root, mayGrabAtom;
        readonly byte minKeycode, maxKeycode;
        readonly Action<string> pressed;

        readonly object sync = new();
        Timer? timer;
        uint[]? keysyms;            //the server's keyboard mapping, keysymsPerKeycode entries per key code from minKeycode
        int keysymsPerKeycode;
        bool wanted, grabbed, disposed;
        int attempts, ticksBeforeGrab;

        X11KeyCapture(nint connection, int preferredScreen, uint window, Action<string> pressed)
        {
            this.connection = connection;
            this.window = window;
            this.pressed = pressed;

            //xcb_setup_t: the key code range sits at bytes 34 and 35; the screens follow, each starting with its root window
            var setup = xcb_get_setup(connection);
            minKeycode = Marshal.ReadByte(setup, 34);
            maxKeycode = Marshal.ReadByte(setup, 35);

            var screens = xcb_setup_roots_iterator(setup);
            for (int i = 0; i < preferredScreen && screens.Remaining > 1; i++) xcb_screen_next(ref screens);
            root = unchecked((uint)Marshal.ReadInt32(screens.Data, 0));

            var name = Encoding.ASCII.GetBytes("_XWAYLAND_MAY_GRAB_KEYBOARD");
            var reply = xcb_intern_atom_reply(connection, xcb_intern_atom(connection, 0, (ushort)name.Length, name), 0);
            if (reply != 0)
            {
                //xcb_intern_atom_reply_t: the atom at byte 8
                mayGrabAtom = unchecked((uint)Marshal.ReadInt32(reply, 8));
                Free(reply);
            }
        }

        /// <summary>A capture for an X window. Null without an X server to reach.</summary>
        public static X11KeyCapture? TryCreate(uint window, Action<string> pressed)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) return null;

            nint connection;
            int preferredScreen;
            try
            {
                connection = xcb_connect(0, out preferredScreen);
            }
            catch (DllNotFoundException)
            {
                return null;
            }

            if (connection == 0 || xcb_connection_has_error(connection) != 0)
            {
                if (connection != 0) xcb_disconnect(connection);
                return null;
            }

            return new X11KeyCapture(connection, preferredScreen, window, pressed);
        }

        public void Start()
        {
            lock (sync)
            {
                if (disposed) return;

                AskMayGrab();

                wanted = true;
                attempts = 0;
                ticksBeforeGrab = TicksBeforeGrab;
                timer ??= new Timer(_ => Tick());
                timer.Change(TimeSpan.Zero, PollInterval);
            }
        }

        /// <summary>
        /// Tells the window manager the window may grab the keyboard: a client message about the
        /// window, sent to the root window as EWMH requests are. GNOME (mutter) then stands its
        /// shortcuts down while the grab lasts; other window managers ignore it. The window
        /// has to be one the window manager knows, so it is sent once the window is in front.
        /// </summary>
        //Called with the lock held
        void AskMayGrab()
        {
            if (mayGrabAtom == 0) return;

            //xcb_client_message_event_t: type, format 32, sequence, the window, the message type, then five 32-bit words
            var message = new byte[32];
            message[0] = ClientMessageEvent;
            message[1] = 32;
            BitConverter.TryWriteBytes(message.AsSpan(4), window);
            BitConverter.TryWriteBytes(message.AsSpan(8), mayGrabAtom);
            BitConverter.TryWriteBytes(message.AsSpan(12), 1u);

            xcb_send_event(connection, 0, root, SubstructureNotifyMask | SubstructureRedirectMask, message);
            xcb_flush(connection);
        }

        public void Stop()
        {
            lock (sync)
            {
                if (disposed) return;

                wanted = false;
                timer?.Change(Timeout.Infinite, Timeout.Infinite);
                Ungrab();
            }
        }

        public void Dispose()
        {
            //Every use of the connection is under the lock and checks disposed first, so a tick
            //still on its way finds it gone rather than freed under its feet
            lock (sync)
            {
                if (disposed) return;
                disposed = true;

                timer?.Dispose();
                timer = null;
                Ungrab();
                xcb_disconnect(connection);
            }
        }

        //Called with the lock held
        void Ungrab()
        {
            if (!grabbed) return;

            xcb_ungrab_keyboard(connection, 0);
            xcb_flush(connection);
            grabbed = false;
        }

        void Tick()
        {
            List<string>? combinations = null;

            lock (sync)
            {
                if (disposed || !wanted) return;

                if (xcb_connection_has_error(connection) != 0)
                {
                    //The X server is gone, and with it the session
                    wanted = false;
                    timer?.Change(Timeout.Infinite, Timeout.Infinite);
                    return;
                }

                if (!grabbed && attempts < GrabAttempts)
                {
                    if (ticksBeforeGrab > 0)
                    {
                        ticksBeforeGrab--;
                    }
                    else
                    {
                        attempts++;
                        TryGrab();
                    }
                }

                nint e;
                while ((e = xcb_poll_for_event(connection)) != 0)
                {
                    try
                    {
                        switch (Marshal.ReadByte(e, 0) & 0x7F)
                        {
                            //xcb_key_press_event_t: key code at byte 1, modifier state at 28
                            case KeyPressEvent when grabbed:
                                if (Decode(Marshal.ReadByte(e, 1), unchecked((ushort)Marshal.ReadInt16(e, 28))) is string combination)
                                {
                                    (combinations ??= []).Add(combination);
                                }
                                break;

                            //xcb_mapping_notify_event_t: what changed at byte 4
                            case MappingNotifyEvent:
                                if (Marshal.ReadByte(e, 4) == MappingKeyboard) keysyms = null;
                                break;
                        }
                    }
                    finally
                    {
                        Free(e);
                    }
                }
            }

            //Outside the lock: the chooser may stop the capture from its handler
            if (combinations == null) return;
            foreach (var combination in combinations)
            {
                try { pressed(combination); } catch (Exception ex) { Console.WriteLine($"Key capture: {ex.Message}"); }
            }
        }

        //Called with the lock held
        void TryGrab()
        {
            var reply = xcb_grab_keyboard_reply(connection, xcb_grab_keyboard(connection, 0, window, 0, GrabModeAsync, GrabModeAsync), 0);
            if (reply == 0) return;

            //xcb_grab_keyboard_reply_t: the status at byte 1 (already grabbed, not viewable, ...)
            grabbed = Marshal.ReadByte(reply, 1) == GrabSuccess;
            Free(reply);
        }

        //Called with the lock held
        string? Decode(byte keycode, ushort state)
        {
            LoadKeyboardMapping();
            if (keysyms == null || keycode < minKeycode) return null;

            int row = (keycode - minKeycode) * keysymsPerKeycode;
            if (row + keysymsPerKeycode > keysyms.Length) return null;

            uint unshifted = keysyms[row];
            uint shifted = keysymsPerKeycode > 1 ? keysyms[row + 1] : 0;
            return X11KeyDecoder.Combination(unshifted, shifted, state);
        }

        //Called with the lock held
        void LoadKeyboardMapping()
        {
            if (keysyms != null) return;

            int count = maxKeycode - minKeycode + 1;
            var reply = xcb_get_keyboard_mapping_reply(connection, xcb_get_keyboard_mapping(connection, minKeycode, (byte)count), 0);
            if (reply == 0) return;

            try
            {
                //xcb_get_keyboard_mapping_reply_t: symbols per key code at byte 1, then a 32-byte header, then the symbols
                int perKeycode = Marshal.ReadByte(reply, 1);
                if (perKeycode == 0) return;

                var table = new int[count * perKeycode];
                Marshal.Copy(reply + 32, table, 0, table.Length);

                keysyms = Array.ConvertAll(table, value => unchecked((uint)value));
                keysymsPerKeycode = perKeycode;
            }
            finally
            {
                Free(reply);
            }
        }

        //xcb hands out replies and events as malloc'd blocks the caller frees
        static unsafe void Free(nint block) => NativeMemory.Free((void*)block);
    }
}
