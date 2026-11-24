using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// GNOME's keyboard shortcuts as its settings hold them, the pure part: a setting as
    /// "gsettings list-recursively" prints it, whether one of its accelerators ("&lt;Super&gt;KP_4",
    /// "&lt;Primary&gt;&lt;Alt&gt;Left") is a hotkey's key combination, and the value that leaves the
    /// combination out. GnomeShortcuts finds the settings and runs gsettings.
    /// </summary>
    public static partial class GnomeKeybindings
    {
        /// <summary>One line of "gsettings list-recursively": the schema, the key and its value in GVariant text.</summary>
        public sealed record Setting(string Schema, string Key, string Value)
        {
            /// <summary>The strings in the value: each item of a string array, or the string itself. Empty for any other type.</summary>
            public IReadOnlyList<string> Strings => GnomeKeybindings.Strings(Value);

            /// <summary>An array of accelerators ("as"), as most shortcuts are, rather than a single one ("s").</summary>
            public bool IsList => Value.StartsWith('[') || Value.StartsWith("@as", StringComparison.Ordinal);
        }

        public static Setting? ParseLine(string line)
        {
            int first = line.IndexOf(' ');
            if (first <= 0) return null;

            int second = line.IndexOf(' ', first + 1);
            if (second <= first + 1) return null;

            return new Setting(line[..first], line[(first + 1)..second], line[(second + 1)..].Trim());
        }

        /// <summary>The quoted strings in a GVariant text value, with their escapes undone.</summary>
        public static List<string> Strings(string value)
        {
            var result = new List<string>();
            var text = value.Trim();
            if (text.StartsWith("@as", StringComparison.Ordinal)) text = text[3..].TrimStart();
            if (text.Length == 0 || (text[0] != '[' && text[0] != '\'' && text[0] != '"')) return result;

            for (int i = 0; i < text.Length; i++)
            {
                char quote = text[i];
                if (quote != '\'' && quote != '"') continue;

                var item = new StringBuilder();
                for (i++; i < text.Length && text[i] != quote; i++)
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i++;
                    item.Append(text[i]);
                }
                result.Add(item.ToString());
            }

            return result;
        }

        /// <summary>
        /// Whether an accelerator is the gesture's key combination: the same modifiers, no more
        /// and no fewer, and the same key by its keysym name. A modifier a hotkey can't have
        /// (Hyper, Meta) makes it another combination.
        /// </summary>
        public static bool Matches(string accelerator, HotkeyGesture gesture)
        {
            if (!LinuxKeyCodes.TryGetKeysym(gesture.KeyName, out _, out var keysymName)) return false;

            var parsed = Accelerator().Match(accelerator.Trim());
            if (!parsed.Success) return false;

            bool ctrl = false, alt = false, shift = false, win = false;
            foreach (Match modifier in Modifier().Matches(parsed.Groups[1].Value))
            {
                switch (modifier.Groups[1].Value.ToLowerInvariant())
                {
                    case "shift": shift = true; break;
                    case "control" or "ctrl" or "ctl" or "primary": ctrl = true; break;
                    case "alt" or "mod1": alt = true; break;
                    case "super" or "mod4": win = true; break;
                    case "release": break;     //acts when the keys come up, on the same keys
                    default: return false;
                }
            }

            //Shift+Tab types ISO_Left_Tab, so a shortcut may name that and leave Shift out
            var key = parsed.Groups[2].Value;
            if (key.Equals("ISO_Left_Tab", StringComparison.OrdinalIgnoreCase))
            {
                key = "Tab";
                shift = true;
            }

            return (ctrl, alt, shift, win) == (gesture.Ctrl, gesture.Alt, gesture.Shift, gesture.Win)
                && Canonical(key) == Canonical(keysymName);
        }

        /// <summary>The value that leaves the gesture's accelerators out of the setting, in the GVariant text gsettings takes.</summary>
        public static string Without(Setting setting, HotkeyGesture gesture)
        {
            if (!setting.IsList) return "''";

            var kept = setting.Strings.Where(accelerator => !Matches(accelerator, gesture)).ToList();
            return kept.Count == 0 ? "@as []" : "[" + string.Join(", ", kept.Select(Quote)) + "]";
        }

        /// <summary>A setting's key as words for the user: "tile-left-half" is "Tile left half".</summary>
        public static string Humanize(string key)
        {
            var words = key.Replace('-', ' ').Replace('_', ' ').Trim();
            return words.Length == 0 ? key : char.ToUpperInvariant(words[0]) + words[1..];
        }

        static string Quote(string text) => "'" + text.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        /// <summary>Keysym names compared without case, and with the names some keys have twice.</summary>
        static string Canonical(string keysymName) => keysymName.ToLowerInvariant() switch
        {
            "page_up" => "prior",
            "page_down" => "next",
            "kp_page_up" => "kp_prior",
            "kp_page_down" => "kp_next",
            var name => name,
        };

        [GeneratedRegex(@"^((?:<[^<>]+>)*)([^<>]+)$")]
        private static partial Regex Accelerator();

        [GeneratedRegex(@"<([^<>]+)>")]
        private static partial Regex Modifier();
    }
}
