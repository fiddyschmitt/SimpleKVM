using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimpleKVM.Displays.win
{
    public class ParsedCapabilities
    {
        public string? Model { get; set; }
        public string? MccsVersion { get; set; }
        public Dictionary<byte, List<byte>> VcpFeatures { get; set; } = [];
    }

    public static class CapabilitiesParser
    {
        public static ParsedCapabilities Parse(string capabilities)
        {
            var result = new ParsedCapabilities();

            var s = capabilities.Trim();

            if (s.StartsWith('(') && FindClosingParen(s, 0) == s.Length - 1)
                s = s[1..^1];

            int pos = 0;
            while (pos < s.Length)
            {
                var segment = NextSegment(s, ref pos);
                if (segment == null) break;

                switch (segment.Value.Name.ToLowerInvariant())
                {
                    case "model":
                        result.Model = segment.Value.Value;
                        break;
                    case "mccs_ver":
                        result.MccsVersion = segment.Value.Value;
                        break;
                    case "vcp":
                        result.VcpFeatures = ParseVcpSegment(segment.Value.Value);
                        break;
                }
            }

            return result;
        }

        static (string Name, string Value)? NextSegment(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                pos++;

            if (pos >= s.Length) return null;

            int nameStart = pos;
            while (pos < s.Length && s[pos] != '(' && !char.IsWhiteSpace(s[pos]))
                pos++;

            string name = s[nameStart..pos];
            if (string.IsNullOrEmpty(name)) return null;

            while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                pos++;

            if (pos >= s.Length || s[pos] != '(')
                return null;

            int closeIdx = FindClosingParen(s, pos);
            if (closeIdx >= s.Length) return null;

            string value = s[(pos + 1)..closeIdx];
            pos = closeIdx + 1;

            return (name, value);
        }

        static int FindClosingParen(string s, int start)
        {
            int depth = 1;
            int pos = start + 1;

            while (pos < s.Length && depth > 0)
            {
                if (s[pos] == '(') depth++;
                else if (s[pos] == ')') depth--;
                pos++;
            }

            if (depth == 0) pos--;
            return pos;
        }

        static Dictionary<byte, List<byte>> ParseVcpSegment(string value)
        {
            var features = new Dictionary<byte, List<byte>>();
            int pos = 0;

            while (pos < value.Length)
            {
                while (pos < value.Length && char.IsWhiteSpace(value[pos]))
                    pos++;

                if (pos >= value.Length) break;

                int tokenStart = pos;
                while (pos < value.Length && !char.IsWhiteSpace(value[pos]) && value[pos] != '(')
                    pos++;

                string token = value[tokenStart..pos];
                if (string.IsNullOrEmpty(token)) break;

                // ddcutil: concatenated codes (>2 chars) — take first 2, rewind for the rest
                if (token.Length > 2)
                {
                    pos = tokenStart + 2;
                    token = token[..2];
                }

                if (!byte.TryParse(token, NumberStyles.HexNumber, null, out byte featureId))
                    continue;

                while (pos < value.Length && char.IsWhiteSpace(value[pos]))
                    pos++;

                List<byte>? subValues = null;
                if (pos < value.Length && value[pos] == '(')
                {
                    int closeIdx = FindClosingParen(value, pos);
                    string subValueStr = value[(pos + 1)..closeIdx];
                    subValues = ParseHexValues(subValueStr);
                    pos = closeIdx + 1;
                }

                features[featureId] = subValues ?? [];
            }

            return features;
        }

        static List<byte> ParseHexValues(string value)
        {
            var result = new List<byte>();
            int pos = 0;

            while (pos < value.Length)
            {
                while (pos < value.Length && char.IsWhiteSpace(value[pos]))
                    pos++;

                if (pos >= value.Length) break;

                // Skip nested parenthesized groups
                if (value[pos] == '(')
                {
                    int closeIdx = FindClosingParen(value, pos);
                    pos = closeIdx + 1;
                    continue;
                }

                int tokenStart = pos;
                while (pos < value.Length && !char.IsWhiteSpace(value[pos]) && value[pos] != '(' && value[pos] != ')')
                    pos++;

                string token = value[tokenStart..pos];
                var hex = token.Length > 2 ? token[..2] : token;
                if (byte.TryParse(hex, NumberStyles.HexNumber, null, out byte b))
                    result.Add(b);
            }

            return result;
        }
    }
}
