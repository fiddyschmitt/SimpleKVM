using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Input
{
    /// <summary>
    /// The parsed form of a stored hotkey string such as "Ctrl+Alt+F1" or "Win+D".
    /// The string format is the cross-platform contract: it is what rules.json stores,
    /// what Windows' KeysConverter historically emitted, and what every platform backend
    /// receives. Win means the Windows key on Windows and Command on macOS.
    /// </summary>
    public class HotkeyGesture
    {
        public bool Ctrl { get; private set; }
        public bool Alt { get; private set; }
        public bool Shift { get; private set; }
        public bool Win { get; private set; }

        /// <summary>The non-modifier key, named as in the WinForms Keys enum (F1, A, D1, NumPad1, OemMinus, ...).</summary>
        public string KeyName { get; private set; } = "";

        public static HotkeyGesture Parse(string hotkeyAsString)
        {
            if (string.IsNullOrWhiteSpace(hotkeyAsString))
                throw new ArgumentException("Hotkey string is empty");

            var result = new HotkeyGesture();

            var tokens = hotkeyAsString
                            .Split('+')
                            .Select(token => token.Trim())
                            .Where(token => token.Length > 0)
                            .ToList();

            if (tokens.Count == 0) throw new ArgumentException($"Could not parse hotkey: {hotkeyAsString}");

            //Every token except the last must be a modifier
            foreach (var token in tokens.Take(tokens.Count - 1))
            {
                switch (token.ToLowerInvariant())
                {
                    case "ctrl" or "control": result.Ctrl = true; break;
                    case "alt" or "option": result.Alt = true; break;
                    case "shift": result.Shift = true; break;
                    case "win" or "windows" or "cmd" or "command" or "meta": result.Win = true; break;
                    default: throw new ArgumentException($"Unknown modifier '{token}' in hotkey: {hotkeyAsString}");
                }
            }

            result.KeyName = tokens[^1];
            return result;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Win) parts.Add("Win");
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(KeyName);
            return string.Join("+", parts);
        }
    }
}
