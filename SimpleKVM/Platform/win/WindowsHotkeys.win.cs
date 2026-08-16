using SimpleKVM.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SimpleKVM.Platform.win
{
    /// <summary>
    /// System-wide hotkeys via RegisterHotKey. WM_HOTKEY is posted to the thread that owns the
    /// registration window, so a dedicated background thread runs a classic message loop for a
    /// hidden form — independent of whatever framework (WinForms, Avalonia) drives the UI thread.
    /// </summary>
    public class WindowsHotkeys : IHotkeyBackend
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        //Win32 MOD_* values
        [Flags]
        enum Modifiers : uint
        {
            Alt = 1,
            Control = 2,
            Shift = 4,
            Win = 8
        }

        static readonly object sinkLock = new();
        static HotkeySink? sink;

        static HotkeySink Sink
        {
            get
            {
                lock (sinkLock)
                {
                    if (sink != null) return sink;

                    using var ready = new ManualResetEventSlim();
                    HotkeySink? created = null;

                    var pumpThread = new Thread(() =>
                    {
                        created = new HotkeySink();
                        _ = created.Handle;     //force handle creation on this thread, so it owns the message queue
                        ready.Set();
                        Application.Run();
                    })
                    {
                        IsBackground = true,
                        Name = "Hotkey message pump"
                    };
                    pumpThread.SetApartmentState(ApartmentState.STA);
                    pumpThread.Start();

                    ready.Wait();
                    sink = created!;
                    return sink;
                }
            }
        }

        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            var modifiers = ToModifiers(gesture);
            var key = ToKey(gesture.KeyName);
            var sink = Sink;

            int hotkeyId = HotkeySink.GenerateUniqueHotkeyId();

            bool registered = (bool)sink.Invoke(() =>
            {
                if (!RegisterHotKey(sink.Handle, hotkeyId, (uint)modifiers, (uint)key)) return false;
                sink.HotkeyActions[(modifiers, key)] = action;
                return true;
            });

            if (!registered) throw new Exception($"Could not register hotkey: {gesture}");

            return new Registration(sink, hotkeyId, modifiers, key);
        }

        static Modifiers ToModifiers(HotkeyGesture gesture)
        {
            var result = new Modifiers();
            if (gesture.Alt) result |= Modifiers.Alt;
            if (gesture.Ctrl) result |= Modifiers.Control;
            if (gesture.Shift) result |= Modifiers.Shift;
            if (gesture.Win) result |= Modifiers.Win;
            return result;
        }

        static Keys ToKey(string keyName)
        {
            if (Enum.TryParse<Keys>(keyName, ignoreCase: true, out var key)) return key;

            //KeysConverter historically produced the stored strings, so it can parse anything
            //it emitted that isn't a plain enum name
            var converted = new KeysConverter().ConvertFromString(keyName) as Keys?;
            if (converted != null) return converted.Value & ~Keys.Modifiers;

            throw new Exception($"Could not parse hotkey key: {keyName}");
        }

        sealed class Registration(HotkeySink sink, int hotkeyId, Modifiers modifiers, Keys key) : IDisposable
        {
            bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                sink.BeginInvoke(() =>
                {
                    sink.HotkeyActions.Remove((modifiers, key));
                    UnregisterHotKey(sink.Handle, hotkeyId);
                });
            }
        }

        /// <summary>
        /// An invisible form whose WndProc receives WM_HOTKEY for every registration. A form
        /// rather than a bare NativeWindow so registrations can be marshalled onto the pump
        /// thread with Invoke.
        /// </summary>
        sealed class HotkeySink : Form
        {
            private static readonly int WM_HOTKEY = 0x0312;
            private static int nextHotkeyId = 0;

            public static int GenerateUniqueHotkeyId()
            {
                return Interlocked.Increment(ref nextHotkeyId);
            }

            public HotkeySink()
            {
                ShowInTaskbar = false;
                //The form is never shown; only its handle and message queue are used
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_HOTKEY)
                {
                    var key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                    var modifiers = (Modifiers)((int)m.LParam & 0xFFFF);

                    if (HotkeyActions.TryGetValue((modifiers, key), out Action? action))
                    {
                        action();
                    }
                }
            }

            public readonly Dictionary<(Modifiers, Keys), Action> HotkeyActions = [];
        }
    }
}
