using SimpleKVM.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimpleKVM.Platform.win
{
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

        static readonly MessageWindow messageWindow = new();

        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            var modifiers = ToModifiers(gesture);
            var key = ToKey(gesture.KeyName);

            int hotkeyId = MessageWindow.GenerateUniqueHotkeyId();

            bool registered = RegisterHotKey(messageWindow.Handle, hotkeyId, (uint)modifiers, (uint)key);
            if (!registered) throw new Exception($"Could not register hotkey: {gesture}");

            messageWindow.HotkeyActions[(modifiers, key)] = action;

            return new Registration(hotkeyId, modifiers, key);
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

        sealed class Registration(int hotkeyId, Modifiers modifiers, Keys key) : IDisposable
        {
            bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                messageWindow.HotkeyActions.Remove((modifiers, key));
                UnregisterHotKey(messageWindow.Handle, hotkeyId);
            }
        }

        /// <summary>
        /// A message-only window whose WndProc receives WM_HOTKEY for every registration.
        /// </summary>
        sealed class MessageWindow : NativeWindow
        {
            private static readonly int WM_HOTKEY = 0x0312;
            private static int nextHotkeyId = 0;

            public static int GenerateUniqueHotkeyId()
            {
                return nextHotkeyId++;
            }

            public MessageWindow()
            {
                CreateHandle(new CreateParams());
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
