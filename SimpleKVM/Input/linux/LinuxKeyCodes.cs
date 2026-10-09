using System;
using System.Collections.Generic;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// The .NET Keys names that rules.json stores, in the three vocabularies Linux needs them
    /// in: the kernel's key code (input-event-codes.h, a physical key position, used when
    /// reading /dev/input), the X keysym (what the key produces in the user's layout, used to
    /// find the key to grab on X11), and that keysym's name (how the XDG shortcuts
    /// specification spells a key for the desktop portals).
    /// </summary>
    public static class LinuxKeyCodes
    {
        public const ushort LeftCtrl = 29, RightCtrl = 97, LeftShift = 42, RightShift = 54,
                            LeftAlt = 56, RightAlt = 100, LeftMeta = 125, RightMeta = 126;

        /// <summary>X key codes are the kernel's plus eight on every X server that takes its keyboards from evdev or libinput, XWayland included.</summary>
        public const int X11KeycodeOffset = 8;

        public static bool IsModifier(ushort code) =>
            code is LeftCtrl or RightCtrl or LeftShift or RightShift or LeftAlt or RightAlt or LeftMeta or RightMeta;

        public static bool TryGet(string keyName, out ushort code) => evdev.TryGetValue(keyName, out code);

        /// <summary>The keysym the key produces and its name, for the keys that have one the same on every layout; false for the rest.</summary>
        public static bool TryGetKeysym(string keyName, out uint keysym, out string name)
        {
            if (keysyms.TryGetValue(keyName, out var found))
            {
                (keysym, name) = found;
                return true;
            }

            (keysym, name) = (0, "");
            return false;
        }

        /// <summary>
        /// A gesture as the XDG shortcuts specification writes a trigger, e.g. "CTRL+ALT+F1" or
        /// "LOGO+SHIFT+KP_1"; null when the key has no keysym name to put in it.
        /// </summary>
        public static string? ToShortcutTrigger(HotkeyGesture gesture)
        {
            if (!TryGetKeysym(gesture.KeyName, out _, out var name)) return null;

            var parts = new List<string>();
            if (gesture.Ctrl) parts.Add("CTRL");
            if (gesture.Alt) parts.Add("ALT");
            if (gesture.Shift) parts.Add("SHIFT");
            if (gesture.Win) parts.Add("LOGO");
            parts.Add(name);
            return string.Join("+", parts);
        }

        static readonly Dictionary<string, ushort> evdev = BuildEvdev();
        static readonly Dictionary<string, (uint Keysym, string Name)> keysyms = BuildKeysyms();

        static Dictionary<string, ushort> BuildEvdev()
        {
            var m = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["A"] = 30, ["B"] = 48, ["C"] = 46, ["D"] = 32, ["E"] = 18, ["F"] = 33, ["G"] = 34, ["H"] = 35,
                ["I"] = 23, ["J"] = 36, ["K"] = 37, ["L"] = 38, ["M"] = 50, ["N"] = 49, ["O"] = 24, ["P"] = 25,
                ["Q"] = 16, ["R"] = 19, ["S"] = 31, ["T"] = 20, ["U"] = 22, ["V"] = 47, ["W"] = 17, ["X"] = 45,
                ["Y"] = 21, ["Z"] = 44,

                ["D1"] = 2, ["D2"] = 3, ["D3"] = 4, ["D4"] = 5, ["D5"] = 6, ["D6"] = 7, ["D7"] = 8,
                ["D8"] = 9, ["D9"] = 10, ["D0"] = 11,

                ["F11"] = 87, ["F12"] = 88,

                ["NumPad0"] = 82, ["NumPad1"] = 79, ["NumPad2"] = 80, ["NumPad3"] = 81, ["NumPad4"] = 75,
                ["NumPad5"] = 76, ["NumPad6"] = 77, ["NumPad7"] = 71, ["NumPad8"] = 72, ["NumPad9"] = 73,
                ["Multiply"] = 55, ["Add"] = 78, ["Subtract"] = 74, ["Divide"] = 98, ["Decimal"] = 83,

                ["Left"] = 105, ["Right"] = 106, ["Down"] = 108, ["Up"] = 103,
                ["Home"] = 102, ["End"] = 107, ["PageUp"] = 104, ["PageDown"] = 109, ["Prior"] = 104, ["Next"] = 109,
                ["Insert"] = 110, ["Delete"] = 111, ["Pause"] = 119, ["Scroll"] = 70,
                ["Space"] = 57, ["Tab"] = 15, ["Return"] = 28, ["Enter"] = 28, ["Escape"] = 1, ["Back"] = 14,
                ["PrintScreen"] = 99, ["Snapshot"] = 99, ["CapsLock"] = 58, ["Capital"] = 58, ["NumLock"] = 69,
                ["Apps"] = 127, ["Help"] = 138, ["Sleep"] = 142,

                ["VolumeMute"] = 113, ["VolumeDown"] = 114, ["VolumeUp"] = 115,
                ["MediaNextTrack"] = 163, ["MediaPreviousTrack"] = 165, ["MediaStop"] = 166, ["MediaPlayPause"] = 164,
                ["LaunchMail"] = 155, ["BrowserHome"] = 172, ["BrowserBack"] = 158, ["BrowserForward"] = 159,
                ["BrowserRefresh"] = 173, ["BrowserStop"] = 128, ["BrowserSearch"] = 217, ["BrowserFavorites"] = 156,

                ["OemMinus"] = 12, ["Oemplus"] = 13, ["Oemcomma"] = 51, ["OemPeriod"] = 52,
                ["OemQuestion"] = 53, ["Oem2"] = 53, ["OemSemicolon"] = 39, ["Oem1"] = 39, ["OemQuotes"] = 40, ["Oem7"] = 40,
                ["OemOpenBrackets"] = 26, ["Oem4"] = 26, ["OemCloseBrackets"] = 27, ["Oem6"] = 27, ["Oem5"] = 43, ["OemPipe"] = 43,
                ["Oemtilde"] = 41, ["Oem3"] = 41, ["OemBackslash"] = 86, ["Oem102"] = 86,
            };

            for (int i = 1; i <= 10; i++) m[$"F{i}"] = (ushort)(58 + i);        //F1=59 .. F10=68
            for (int i = 13; i <= 24; i++) m[$"F{i}"] = (ushort)(183 + i - 13); //F13=183 .. F24=194

            return m;
        }

        /// <summary>Values and names from X11's keysymdef.h and XF86keysym.h.</summary>
        static Dictionary<string, (uint, string)> BuildKeysyms()
        {
            var m = new Dictionary<string, (uint, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Multiply"] = (0xffaa, "KP_Multiply"), ["Add"] = (0xffab, "KP_Add"), ["Subtract"] = (0xffad, "KP_Subtract"),
                ["Decimal"] = (0xffae, "KP_Decimal"), ["Divide"] = (0xffaf, "KP_Divide"),

                ["Home"] = (0xff50, "Home"), ["Left"] = (0xff51, "Left"), ["Up"] = (0xff52, "Up"), ["Right"] = (0xff53, "Right"),
                ["Down"] = (0xff54, "Down"), ["PageUp"] = (0xff55, "Prior"), ["Prior"] = (0xff55, "Prior"),
                ["PageDown"] = (0xff56, "Next"), ["Next"] = (0xff56, "Next"), ["End"] = (0xff57, "End"),
                ["Insert"] = (0xff63, "Insert"), ["Delete"] = (0xffff, "Delete"), ["Pause"] = (0xff13, "Pause"), ["Scroll"] = (0xff14, "Scroll_Lock"),
                ["Space"] = (0x20, "space"), ["Tab"] = (0xff09, "Tab"), ["Return"] = (0xff0d, "Return"), ["Enter"] = (0xff0d, "Return"),
                ["Escape"] = (0xff1b, "Escape"), ["Back"] = (0xff08, "BackSpace"),
                ["PrintScreen"] = (0xff61, "Print"), ["Snapshot"] = (0xff61, "Print"),
                ["CapsLock"] = (0xffe5, "Caps_Lock"), ["Capital"] = (0xffe5, "Caps_Lock"), ["NumLock"] = (0xff7f, "Num_Lock"),
                ["Apps"] = (0xff67, "Menu"), ["Help"] = (0xff6a, "Help"),

                ["VolumeMute"] = (0x1008ff12, "XF86AudioMute"), ["VolumeDown"] = (0x1008ff11, "XF86AudioLowerVolume"), ["VolumeUp"] = (0x1008ff13, "XF86AudioRaiseVolume"),
                ["MediaNextTrack"] = (0x1008ff17, "XF86AudioNext"), ["MediaPreviousTrack"] = (0x1008ff16, "XF86AudioPrev"),
                ["MediaStop"] = (0x1008ff15, "XF86AudioStop"), ["MediaPlayPause"] = (0x1008ff14, "XF86AudioPlay"),

                ["OemMinus"] = (0x2d, "minus"), ["Oemplus"] = (0x3d, "equal"), ["Oemcomma"] = (0x2c, "comma"), ["OemPeriod"] = (0x2e, "period"),
                ["OemQuestion"] = (0x2f, "slash"), ["Oem2"] = (0x2f, "slash"), ["OemSemicolon"] = (0x3b, "semicolon"), ["Oem1"] = (0x3b, "semicolon"),
                ["OemQuotes"] = (0x27, "apostrophe"), ["Oem7"] = (0x27, "apostrophe"),
                ["OemOpenBrackets"] = (0x5b, "bracketleft"), ["Oem4"] = (0x5b, "bracketleft"),
                ["OemCloseBrackets"] = (0x5d, "bracketright"), ["Oem6"] = (0x5d, "bracketright"),
                ["Oem5"] = (0x5c, "backslash"), ["OemPipe"] = (0x5c, "backslash"),
                ["Oemtilde"] = (0x60, "grave"), ["Oem3"] = (0x60, "grave"),
            };

            for (char c = 'A'; c <= 'Z'; c++) m[c.ToString()] = ((uint)char.ToLowerInvariant(c), char.ToLowerInvariant(c).ToString());
            for (int i = 0; i <= 9; i++)
            {
                m[$"D{i}"] = ((uint)('0' + i), i.ToString());
                m[$"NumPad{i}"] = ((uint)(0xffb0 + i), $"KP_{i}");
            }
            for (int i = 1; i <= 24; i++) m[$"F{i}"] = ((uint)(0xffbe + i - 1), $"F{i}");

            return m;
        }
    }
}
