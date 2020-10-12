using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SimpleKVM
{
    public class Hotkey
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static Window FakeWindow = new Window();
        readonly int HotkeyId = FakeWindow.GenerateUniqueHotkeyId();

        public ModifierKeys Modifier { get; }
        public Keys Keys { get; }
        public Action? Action { get; }

        public Hotkey(ModifierKeys modifier, Keys key, Action? action)
        {
            Modifier = modifier;
            Keys = key;
            Action = action;

            RegisterHotKey();
        }

        public Hotkey(Keys keys, Action? action)
        {
            (Modifier, Keys) = Split(keys);
            Action = action;

            RegisterHotKey();
        }

        public static (ModifierKeys modifiers, Keys key) Split(Keys keys)
        {
            var mk = new ModifierKeys();
            if (keys.HasFlag(Keys.Control)) mk |= ModifierKeys.Control;
            if (keys.HasFlag(Keys.Alt)) mk |= ModifierKeys.Alt;
            if (keys.HasFlag(Keys.Shift)) mk |= ModifierKeys.Shift;
            if (keys.HasFlag(Keys.LWin)) mk |= ModifierKeys.Win;
            if (keys.HasFlag(Keys.RWin)) mk |= ModifierKeys.Win;

            var pressedKeys = keys & ~Keys.Modifiers;
            return (mk, pressedKeys);
        }

        void RegisterHotKey()
        {
            bool registered = RegisterHotKey(FakeWindow.Handle, HotkeyId, (uint)Modifier, (uint)Keys);
            if (!registered) throw new Exception("Could not register hotkey");

            if (Action != null)
            {
                FakeWindow.HotkeyActions.Add((Modifier, Keys), Action);
            }
        }

        public void UnregisterHotkey()
        {
            if (FakeWindow.HotkeyActions.ContainsKey((Modifier, Keys)))
            {
                FakeWindow.HotkeyActions.Remove((Modifier, Keys));
            }

            UnregisterHotKey(FakeWindow.Handle, HotkeyId);
        }

        public class Window : NativeWindow, IDisposable
        {
            private static int WM_HOTKEY = 0x0312;
            private static int hotkeyId = 0;

            public int GenerateUniqueHotkeyId()
            {
                return hotkeyId++;
            }

            public Window()
            {
                this.CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                // check if we got a hot key pressed.
                if (m.Msg == WM_HOTKEY)
                {
                    // get the keys.
                    Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                    ModifierKeys modifier = (ModifierKeys)((int)m.LParam & 0xFFFF);

                    // invoke the event to notify the parent.
                    var together = (modifier, key);

                    if (HotkeyActions.ContainsKey(together))
                    {
                        var action = HotkeyActions[together];
                        action();
                    }
                }
            }

            public readonly Dictionary<(ModifierKeys, Keys), Action> HotkeyActions = new Dictionary<(ModifierKeys, Keys), Action>();

            public void Dispose()
            {
                this.DestroyHandle();
            }
        }
    }

    public class KeyPressedEventArgs : EventArgs
    {
        internal KeyPressedEventArgs(ModifierKeys modifier, Keys key)
        {
            Modifier = modifier;
            Key = key;
        }

        public ModifierKeys Modifier { get; }

        public Keys Key { get; }
    }

    [Flags]
    public enum ModifierKeys : uint
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }
}
