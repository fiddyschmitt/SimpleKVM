using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimpleKVM.Platform.linux
{
    /// <summary>
    /// Parses the GVariant text format that <c>gdbus call</c> prints, e.g.
    /// <c>(uint32 1, [('DP-1', 'DEL', 'U2412M', 'ABC')], {'is-current': &lt;true&gt;})</c>.
    /// Tuples and arrays become <see cref="List{T}"/> of object, dictionaries with string keys
    /// become <see cref="Dictionary{TKey,TValue}"/>, variants are unwrapped, integers are
    /// <see cref="long"/>, floating point <see cref="double"/>. Type annotations
    /// (<c>uint64 5</c>, <c>@a{sv} {}</c>, <c>objectpath '/x'</c>) are accepted and dropped:
    /// gdbus annotates the first element of every array and every non-default scalar, which
    /// is exactly what made regex parsing of this output go wrong.
    /// </summary>
    public static class GVariantText
    {
        public static object? Parse(string text)
        {
            var parser = new Parser(text);
            var value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw parser.Error("unexpected trailing text");
            return value;
        }

        /// <summary>Parses and returns the first (or only) value of a top-level tuple, e.g. the <c>5432</c> of <c>(uint64 5432,)</c>.</summary>
        public static object? ParseSingleResult(string text)
        {
            var value = Parse(text);
            return value is List<object?> { Count: > 0 } list ? list[0] : value;
        }

        static readonly HashSet<string> ScalarAnnotations = new(StringComparer.Ordinal)
        {
            "boolean", "byte", "int16", "uint16", "int32", "uint32", "int64", "uint64",
            "handle", "double", "string", "objectpath", "signature",
        };

        sealed class Parser(string text)
        {
            int pos;

            public bool AtEnd => pos >= text.Length;

            public FormatException Error(string message) => new($"GVariant text: {message} at position {pos}");

            public void SkipWhitespace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            }

            char Peek() => pos < text.Length ? text[pos] : '\0';

            bool TryConsume(char c)
            {
                SkipWhitespace();
                if (Peek() != c) return false;
                pos++;
                return true;
            }

            void Expect(char c)
            {
                if (!TryConsume(c)) throw Error($"expected '{c}'");
            }

            public object? ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("unexpected end");

                char c = Peek();
                switch (c)
                {
                    case '(': return ParseTuple();
                    case '[': return ParseArray();
                    case '{': return ParseDictionary();
                    case '<':
                        pos++;
                        var inner = ParseValue();
                        Expect('>');
                        return inner;
                    case '\'':
                    case '"':
                        return ParseString();
                    case '@':
                        //@type prefix, e.g. @a{sv} {} or @ms nothing: the type has no spaces
                        while (pos < text.Length && !char.IsWhiteSpace(text[pos])) pos++;
                        return ParseValue();
                }

                if (c == 'b' && pos + 1 < text.Length && (text[pos + 1] == '\'' || text[pos + 1] == '"'))
                {
                    pos++;    //bytestring b'...'
                    return ParseString();
                }

                if (char.IsLetter(c))
                {
                    var word = ReadWord();
                    switch (word)
                    {
                        case "true": return true;
                        case "false": return false;
                        case "nothing": return null;
                        case "just": return ParseValue();
                        case "inf": return double.PositiveInfinity;
                        case "nan": return double.NaN;
                    }

                    if (ScalarAnnotations.Contains(word))
                    {
                        return ParseValue();    //annotation, then the value it describes
                    }

                    throw Error($"unknown token '{word}'");
                }

                return ParseNumber();
            }

            string ReadWord()
            {
                int start = pos;
                while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_')) pos++;
                return text[start..pos];
            }

            object ParseNumber()
            {
                int start = pos;
                if (Peek() == '-' || Peek() == '+') pos++;

                if (pos + 1 < text.Length && text[pos] == '0' && (text[pos + 1] == 'x' || text[pos + 1] == 'X'))
                {
                    pos += 2;
                    int hexStart = pos;
                    while (pos < text.Length && Uri.IsHexDigit(text[pos])) pos++;
                    var hex = long.Parse(text[hexStart..pos], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return text[start] == '-' ? -hex : hex;
                }

                if (pos + 2 < text.Length && string.CompareOrdinal(text, pos, "inf", 0, 3) == 0)
                {
                    pos += 3;
                    return text[start] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
                }

                bool isDouble = false;
                while (pos < text.Length)
                {
                    char ch = text[pos];
                    if (char.IsDigit(ch)) { pos++; continue; }
                    if (ch == '.' || ch == 'e' || ch == 'E') { isDouble = true; pos++; continue; }
                    if ((ch == '-' || ch == '+') && (text[pos - 1] == 'e' || text[pos - 1] == 'E')) { pos++; continue; }
                    break;
                }

                var token = text[start..pos];
                if (token.Length == 0 || token == "-" || token == "+") throw Error("expected a value");

                if (isDouble) return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)) return l;
                if (ulong.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var ul)) return unchecked((long)ul);
                throw Error($"bad number '{token}'");
            }

            string ParseString()
            {
                char quote = text[pos++];
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = text[pos++];
                    if (c == quote) return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    if (AtEnd) throw Error("unterminated escape");
                    char e = text[pos++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'a': sb.Append('\a'); break;
                        case 'v': sb.Append('\v'); break;
                        case '0': sb.Append('\0'); break;
                        case 'u': sb.Append((char)int.Parse(text.Substring(pos, 4), NumberStyles.HexNumber)); pos += 4; break;
                        case 'U': sb.Append(char.ConvertFromUtf32(int.Parse(text.Substring(pos, 8), NumberStyles.HexNumber))); pos += 8; break;
                        default: sb.Append(e); break;    //\\ \' \"
                    }
                }
            }

            List<object?> ParseTuple()
            {
                Expect('(');
                var items = new List<object?>();
                while (!TryConsume(')'))
                {
                    items.Add(ParseValue());
                    if (!TryConsume(',')) { Expect(')'); break; }
                }
                return items;
            }

            List<object?> ParseArray()
            {
                Expect('[');
                var items = new List<object?>();
                while (!TryConsume(']'))
                {
                    items.Add(ParseValue());
                    if (!TryConsume(',')) { Expect(']'); break; }
                }
                return items;
            }

            object ParseDictionary()
            {
                Expect('{');
                var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                var pairs = new List<object?>();
                bool allStringKeys = true;

                while (!TryConsume('}'))
                {
                    var key = ParseValue();
                    SkipWhitespace();
                    //'k': v is a dictionary; {k, v} is a single dict entry
                    char sep = Peek();
                    if (sep != ':' && sep != ',') throw Error("expected ':' or ',' after a dictionary key");
                    pos++;
                    var value = ParseValue();

                    if (key is string s && allStringKeys) dict[s] = value;
                    else allStringKeys = false;
                    pairs.Add(new List<object?> { key, value });

                    if (!TryConsume(',')) { Expect('}'); break; }
                }

                return allStringKeys ? dict : pairs;
            }
        }
    }
}
