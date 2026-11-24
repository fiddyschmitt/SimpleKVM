using System.Collections.Generic;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// A key press as the X server reports it (a key code and the modifier state), turned into a
    /// hotkey string in the vocabulary rules.json stores: "Win+NumPad4", or "Escape" when no
    /// modifier is held. The pure part of X11KeyCapture.
    /// </summary>
    public static class X11KeyDecoder
    {
        public const ushort ShiftMask = 1, ControlMask = 4, Mod1Mask = 8, Mod2Mask = 16, Mod4Mask = 64;

        /// <summary>
        /// The keypad's other face, for when Num Lock is off: the key names Avalonia gives those
        /// keys, so the hotkey is the one the chooser would have recorded from them itself.
        /// </summary>
        static readonly Dictionary<uint, string> KeypadNavigation = new()
        {
            [0xff95] = "Home", [0xff96] = "Left", [0xff97] = "Up", [0xff98] = "Right", [0xff99] = "Down",
            [0xff9a] = "PageUp", [0xff9b] = "PageDown", [0xff9c] = "End", [0xff9e] = "Insert", [0xff9f] = "Delete",
            [0xff8d] = "Enter",
        };

        /// <summary>
        /// Which of a key's first two symbols the press types, by the core protocol's rule: the
        /// keypad's digits while Num Lock is on (unless Shift is held too, which swaps back), and
        /// otherwise the unshifted symbol. Shift stays a modifier of the hotkey rather than
        /// changing its key, as it does on the other platforms: Shift+1 is "Shift+D1", not "!".
        /// </summary>
        public static uint Keysym(uint unshifted, uint shifted, ushort state)
        {
            bool numLock = (state & Mod2Mask) != 0;
            bool shift = (state & ShiftMask) != 0;

            if (numLock && IsKeypad(shifted)) return shift ? unshifted : shifted;
            return unshifted != 0 ? unshifted : shifted;
        }

        /// <summary>The hotkey string for a press, or null for a key a hotkey can't use (the modifier keys themselves among them).</summary>
        public static string? Combination(uint unshifted, uint shifted, ushort state)
        {
            uint keysym = Keysym(unshifted, shifted, state);

            //Upper-case letters stand for the same key as lower-case ones
            if (keysym is >= 'A' and <= 'Z') keysym += 'a' - 'A';

            if (!LinuxKeyCodes.TryGetKeyName(keysym, out var keyName) && !KeypadNavigation.TryGetValue(keysym, out keyName))
                return null;

            var parts = new List<string>();
            if ((state & Mod4Mask) != 0) parts.Add("Win");
            if ((state & ControlMask) != 0) parts.Add("Ctrl");
            if ((state & Mod1Mask) != 0) parts.Add("Alt");
            if ((state & ShiftMask) != 0) parts.Add("Shift");
            parts.Add(keyName);
            return string.Join("+", parts);
        }

        static bool IsKeypad(uint keysym) => keysym is >= 0xff80 and <= 0xffbd;
    }
}
