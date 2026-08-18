using System;
using System.Collections.Generic;

namespace SimpleKVM.Platform.win
{
    /// <summary>
    /// Maps the key names stored in hotkey strings to Win32 virtual-key codes. The names are
    /// the .NET Keys enum names (F1, D1, NumPad1, OemMinus, ...), which is what
    /// the app has always written to rules.json — and Keys' numeric values are the VK codes, so
    /// this table is that enum minus the dependency.
    /// </summary>
    public static class WindowsKeyNames
    {
        public static uint? ToVirtualKey(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName)) return null;

            keyName = keyName.Trim();

            if (KeyCodes.TryGetValue(keyName, out uint code)) return code;

            //Single letters and digits typed as-is ("A", "1")
            if (keyName.Length == 1)
            {
                char c = char.ToUpperInvariant(keyName[0]);
                if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') return c;
            }

            return null;
        }

        static readonly Dictionary<string, uint> KeyCodes = BuildTable();

        static Dictionary<string, uint> BuildTable()
        {
            var table = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

            for (char c = 'A'; c <= 'Z'; c++) table[c.ToString()] = c;
            for (int i = 0; i <= 9; i++) table[$"D{i}"] = (uint)('0' + i);
            for (int i = 0; i <= 9; i++) table[$"NumPad{i}"] = (uint)(0x60 + i);
            for (int i = 1; i <= 24; i++) table[$"F{i}"] = (uint)(0x6F + i);

            void Add(uint code, params string[] names)
            {
                foreach (var name in names) table[name] = code;
            }

            Add(0x08, "Back", "Backspace");
            Add(0x09, "Tab");
            Add(0x0C, "Clear");
            Add(0x0D, "Return", "Enter");
            Add(0x13, "Pause");
            Add(0x14, "CapsLock", "Capital");
            Add(0x1B, "Escape", "Esc");
            Add(0x20, "Space");
            Add(0x21, "PageUp", "Prior");
            Add(0x22, "PageDown", "Next");
            Add(0x23, "End");
            Add(0x24, "Home");
            Add(0x25, "Left");
            Add(0x26, "Up");
            Add(0x27, "Right");
            Add(0x28, "Down");
            Add(0x29, "Select");
            Add(0x2A, "Print");
            Add(0x2B, "Execute");
            Add(0x2C, "PrintScreen", "Snapshot");
            Add(0x2D, "Insert");
            Add(0x2E, "Delete");
            Add(0x2F, "Help");
            Add(0x5D, "Apps");
            Add(0x5F, "Sleep");
            Add(0x6A, "Multiply");
            Add(0x6B, "Add");
            Add(0x6C, "Separator");
            Add(0x6D, "Subtract");
            Add(0x6E, "Decimal");
            Add(0x6F, "Divide");
            Add(0x90, "NumLock");
            Add(0x91, "Scroll");
            Add(0xA6, "BrowserBack");
            Add(0xA7, "BrowserForward");
            Add(0xA8, "BrowserRefresh");
            Add(0xA9, "BrowserStop");
            Add(0xAA, "BrowserSearch");
            Add(0xAB, "BrowserFavorites");
            Add(0xAC, "BrowserHome");
            Add(0xAD, "VolumeMute");
            Add(0xAE, "VolumeDown");
            Add(0xAF, "VolumeUp");
            Add(0xB0, "MediaNextTrack");
            Add(0xB1, "MediaPreviousTrack");
            Add(0xB2, "MediaStop");
            Add(0xB3, "MediaPlayPause");
            Add(0xB4, "LaunchMail");
            Add(0xB5, "SelectMedia");
            Add(0xB6, "LaunchApplication1");
            Add(0xB7, "LaunchApplication2");
            Add(0xBA, "Oem1", "OemSemicolon");
            Add(0xBB, "Oemplus");
            Add(0xBC, "Oemcomma");
            Add(0xBD, "OemMinus");
            Add(0xBE, "OemPeriod");
            Add(0xBF, "Oem2", "OemQuestion");
            Add(0xC0, "Oem3", "Oemtilde");
            Add(0xDB, "Oem4", "OemOpenBrackets");
            Add(0xDC, "Oem5", "OemPipe");
            Add(0xDD, "Oem6", "OemCloseBrackets");
            Add(0xDE, "Oem7", "OemQuotes");
            Add(0xDF, "Oem8");
            Add(0xE2, "Oem102", "OemBackslash");

            return table;
        }
    }
}
