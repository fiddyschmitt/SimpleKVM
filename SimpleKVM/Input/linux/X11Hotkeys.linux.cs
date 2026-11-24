using SimpleKVM.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// Global hotkeys in an X11 session: passive key grabs on the root window, the way every X
    /// desktop's own shortcuts work. The grab is exclusive (the combination reaches nobody
    /// else, and one another program already holds is refused), follows the keyboard layout
    /// (the key is found by what it types, not where it sits), and needs no permission.
    /// Spoken through libxcb on a connection of its own: xcb reports a refused grab for the
    /// one request that caused it, where Xlib would route it to the process-wide error
    /// handler that belongs to Avalonia.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed class X11Hotkeys : IHotkeyBackend
    {
        const string Xcb = "libxcb.so.1";

        const ushort ShiftMask = 1, LockMask = 2, ControlMask = 4, Mod1Mask = 8, Mod2Mask = 16, Mod4Mask = 64;
        const ushort GestureModifiers = ShiftMask | ControlMask | Mod1Mask | Mod4Mask;

        //A grab names its modifiers exactly, so each hotkey is grabbed once per state of Caps Lock and Num Lock (Mod2)
        static readonly ushort[] LockStates = [0, LockMask, Mod2Mask, LockMask | Mod2Mask];

        const byte KeyPressEvent = 2, KeyReleaseEvent = 3, MappingNotifyEvent = 34;
        const byte MappingKeyboard = 1;
        const byte BadAccess = 10;
        const byte GrabModeAsync = 1;

        [DllImport(Xcb)] static extern nint xcb_connect(nint displayName, out int preferredScreen);
        [DllImport(Xcb)] static extern int xcb_connection_has_error(nint connection);
        [DllImport(Xcb)] static extern void xcb_disconnect(nint connection);
        [DllImport(Xcb)] static extern nint xcb_get_setup(nint connection);
        [DllImport(Xcb)] static extern ScreenIterator xcb_setup_roots_iterator(nint setup);
        [DllImport(Xcb)] static extern void xcb_screen_next(ref ScreenIterator iterator);
        [DllImport(Xcb)] static extern int xcb_flush(nint connection);
        [DllImport(Xcb)] static extern nint xcb_wait_for_event(nint connection);
        [DllImport(Xcb)] static extern uint xcb_grab_key_checked(nint connection, byte ownerEvents, uint grabWindow, ushort modifiers, byte key, byte pointerMode, byte keyboardMode);
        [DllImport(Xcb)] static extern uint xcb_ungrab_key(nint connection, byte key, uint grabWindow, ushort modifiers);
        [DllImport(Xcb)] static extern nint xcb_request_check(nint connection, uint cookie);
        [DllImport(Xcb)] static extern uint xcb_get_keyboard_mapping(nint connection, byte firstKeycode, byte count);
        [DllImport(Xcb)] static extern nint xcb_get_keyboard_mapping_reply(nint connection, uint cookie, nint error);

        [StructLayout(LayoutKind.Sequential)]
        struct ScreenIterator
        {
            public nint Data;
            public int Remaining;
            public int Index;
        }

        sealed class Binding(HotkeyGesture gesture, ushort modifiers, Action action)
        {
            public HotkeyGesture Gesture { get; } = gesture;
            public ushort Modifiers { get; } = modifiers;
            public Action Action { get; } = action;
            public byte Keycode { get; set; }
        }

        readonly nint connection;
        readonly uint root;
        readonly byte minKeycode, maxKeycode;

        readonly object sync = new();
        readonly List<Binding> bindings = [];
        readonly Dictionary<byte, uint> lastRelease = [];
        uint[]? keysyms;            //the server's keyboard mapping, keysymsPerKeycode entries per key code from minKeycode
        int keysymsPerKeycode;

        /// <summary>Connects to the X server named by DISPLAY. Throws when there is none to connect to.</summary>
        public X11Hotkeys()
        {
            connection = xcb_connect(0, out int preferredScreen);
            if (connection == 0 || xcb_connection_has_error(connection) != 0)
            {
                if (connection != 0) xcb_disconnect(connection);
                throw new InvalidOperationException("Cannot connect to the X server.");
            }

            //xcb_setup_t: the key code range sits at bytes 34 and 35; the screens follow, each starting with its root window
            var setup = xcb_get_setup(connection);
            minKeycode = Marshal.ReadByte(setup, 34);
            maxKeycode = Marshal.ReadByte(setup, 35);

            var screens = xcb_setup_roots_iterator(setup);
            for (int i = 0; i < preferredScreen && screens.Remaining > 1; i++) xcb_screen_next(ref screens);
            root = (uint)Marshal.ReadInt32(screens.Data, 0);

            new Thread(EventLoop)
            {
                IsBackground = true,
                Name = "X11 hotkeys"
            }.Start();
        }

        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            lock (sync)
            {
                var binding = new Binding(gesture, ModifierMask(gesture), action) { Keycode = FindKeycode(gesture.KeyName) };

                if (!bindings.Any(b => b.Keycode == binding.Keycode && b.Modifiers == binding.Modifiers))
                {
                    Grab(binding.Keycode, binding.Modifiers);
                }

                bindings.Add(binding);

                return new Registration(() =>
                {
                    lock (sync)
                    {
                        if (!bindings.Remove(binding)) return;

                        if (!bindings.Any(b => b.Keycode == binding.Keycode && b.Modifiers == binding.Modifiers))
                        {
                            Ungrab(binding.Keycode, binding.Modifiers);
                        }
                    }
                });
            }
        }

        static ushort ModifierMask(HotkeyGesture gesture)
        {
            ushort mask = 0;
            if (gesture.Shift) mask |= ShiftMask;
            if (gesture.Ctrl) mask |= ControlMask;
            if (gesture.Alt) mask |= Mod1Mask;
            if (gesture.Win) mask |= Mod4Mask;
            return mask;
        }

        /// <summary>
        /// The key code of the key that types the gesture's key in the current layout. Keys the
        /// layout doesn't have by that symbol (and the few without a symbol of their own) fall
        /// back to their position on a US keyboard, which is how the kernel numbers them.
        /// </summary>
        byte FindKeycode(string keyName)
        {
            if (LinuxKeyCodes.TryGetKeysym(keyName, out uint keysym, out _))
            {
                LoadKeyboardMapping();
                if (keysyms != null)
                {
                    int keys = keysyms.Length / keysymsPerKeycode;

                    //The unshifted symbol first: a key that merely has the symbol on a higher level is second choice
                    for (int column = 0; column < keysymsPerKeycode; column++)
                    {
                        for (int key = 0; key < keys; key++)
                        {
                            if (keysyms[key * keysymsPerKeycode + column] == keysym) return (byte)(minKeycode + key);
                        }
                    }
                }
            }

            if (LinuxKeyCodes.TryGet(keyName, out ushort evdevCode) && evdevCode + LinuxKeyCodes.X11KeycodeOffset <= maxKeycode)
            {
                return (byte)(evdevCode + LinuxKeyCodes.X11KeycodeOffset);
            }

            throw new ArgumentException($"Key '{keyName}' is not supported for hotkeys on Linux");
        }

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

        void Grab(byte keycode, ushort modifiers)
        {
            for (int i = 0; i < LockStates.Length; i++)
            {
                var cookie = xcb_grab_key_checked(connection, 0, root, (ushort)(modifiers | LockStates[i]), keycode, GrabModeAsync, GrabModeAsync);
                var error = xcb_request_check(connection, cookie);
                if (error == 0) continue;

                byte code = Marshal.ReadByte(error, 1);
                Free(error);

                //Give back the lock states already taken
                for (int j = 0; j < i; j++) xcb_ungrab_key(connection, keycode, root, (ushort)(modifiers | LockStates[j]));
                xcb_flush(connection);

                throw new InvalidOperationException(code == BadAccess
                    ? "The key combination is already taken by another application or the desktop."
                    : $"The X server refused the key grab (error {code}).");
            }
        }

        void Ungrab(byte keycode, ushort modifiers)
        {
            foreach (var lockState in LockStates) xcb_ungrab_key(connection, keycode, root, (ushort)(modifiers | lockState));
            xcb_flush(connection);
        }

        void EventLoop()
        {
            while (true)
            {
                var e = xcb_wait_for_event(connection);
                if (e == 0) return;     //the X server is gone, and with it the session

                try
                {
                    switch (Marshal.ReadByte(e, 0) & 0x7F)
                    {
                        //xcb_key_press_event_t: key code at byte 1, timestamp at 4, modifier state at 28
                        case KeyPressEvent:
                            OnKeyPress(Marshal.ReadByte(e, 1), unchecked((uint)Marshal.ReadInt32(e, 4)), unchecked((ushort)Marshal.ReadInt16(e, 28)));
                            break;

                        case KeyReleaseEvent:
                            lock (sync) lastRelease[Marshal.ReadByte(e, 1)] = unchecked((uint)Marshal.ReadInt32(e, 4));
                            break;

                        //xcb_mapping_notify_event_t: what changed at byte 4 (modifiers, keyboard, pointer)
                        case MappingNotifyEvent:
                            if (Marshal.ReadByte(e, 4) == MappingKeyboard) OnKeyboardMappingChanged();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"X11 hotkeys: {ex.Message}");
                }
                finally
                {
                    Free(e);
                }
            }
        }

        void OnKeyPress(byte keycode, uint time, ushort state)
        {
            List<Action> toFire;
            lock (sync)
            {
                //A held key repeats as a release and a press carrying the same timestamp; only the first press counts
                if (lastRelease.Remove(keycode, out uint releasedAt) && releasedAt == time) return;

                ushort modifiers = (ushort)(state & GestureModifiers);
                toFire = bindings.Where(b => b.Keycode == keycode && b.Modifiers == modifiers).Select(b => b.Action).Distinct().ToList();
            }

            //Off this thread: a monitor switch takes a few hundred ms of DDC traffic
            foreach (var action in toFire)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { action(); } catch (Exception ex) { Console.WriteLine($"Hotkey action failed: {ex.Message}"); }
                });
            }
        }

        /// <summary>
        /// The keyboard mapping changed, so the keys may have moved: find them again, and move
        /// the grabs of those that did. The server announces this far more often than a layout
        /// really changes (every time a different keyboard is typed on, so after every flip of
        /// a USB switch), and then nothing has moved. The grabs that still fit are therefore
        /// left untouched: releasing and retaking them would open a moment in which the
        /// combination belongs to nobody, and the first keys typed on the newly arrived
        /// keyboard, which is what set the announcement off, could fall into it.
        /// </summary>
        void OnKeyboardMappingChanged()
        {
            lock (sync)
            {
                keysyms = null;

                var before = bindings.Select(b => (b.Keycode, b.Modifiers)).ToHashSet();

                foreach (var binding in bindings)
                {
                    try
                    {
                        binding.Keycode = FindKeycode(binding.Gesture.KeyName);
                    }
                    catch (Exception ex)
                    {
                        //The new layout has no such key; the old grab stays for what it is worth
                        Console.WriteLine($"Hotkey {binding.Gesture}: {ex.Message}");
                    }
                }

                var after = bindings.Select(b => (b.Keycode, b.Modifiers)).ToHashSet();

                foreach (var (keycode, modifiers) in after.Except(before))
                {
                    try
                    {
                        Grab(keycode, modifiers);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Hotkey on key code {keycode}: {ex.Message}");
                    }
                }

                foreach (var (keycode, modifiers) in before.Except(after))
                {
                    Ungrab(keycode, modifiers);
                }
            }
        }

        //xcb hands out replies, errors and events as malloc'd blocks the caller frees
        static unsafe void Free(nint block) => NativeMemory.Free((void*)block);

        sealed class Registration(Action onDispose) : IDisposable
        {
            int disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0) onDispose();
            }
        }
    }
}
