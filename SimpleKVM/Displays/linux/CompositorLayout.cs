using SimpleKVM.Platform.linux;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// One enabled output as the desktop lays it out: its name as the compositor knows it,
    /// its logical bounds, and whatever identity the compositor exposes for pairing it with
    /// a DRM connector (see <see cref="LayoutJoin"/>). Wayland compositors name outputs by
    /// their DRM connector; X drivers use their own names, so X11 pairs by EDID instead.
    /// </summary>
    public sealed record OutputGeometry(
        string Name, int X, int Y, int Width, int Height,
        string? Vendor = null, string? Product = null, string? Serial = null, byte[]? Edid = null)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
    }

    /// <summary>KDE Plasma: the JSON printed by <c>kscreen-doctor -j</c>.</summary>
    public static class KScreenLayout
    {
        public static List<OutputGeometry>? Parse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var result = new List<OutputGeometry>();

                foreach (var output in doc.RootElement.GetProperty("outputs").EnumerateArray())
                {
                    if (output.TryGetProperty("enabled", out var enabled) && !enabled.GetBoolean()) continue;
                    if (output.TryGetProperty("connected", out var connected) && !connected.GetBoolean()) continue;

                    var name = output.GetProperty("name").GetString();
                    if (string.IsNullOrEmpty(name)) continue;

                    var pos = output.GetProperty("pos");
                    var size = output.GetProperty("size");
                    double scale = output.TryGetProperty("scale", out var s) ? s.GetDouble() : 1.0;
                    if (scale <= 0) scale = 1.0;

                    int w = size.GetProperty("width").GetInt32();
                    int h = size.GetProperty("height").GetInt32();

                    //rotation: 1 none, 2 left, 4 inverted, 8 right
                    int rotation = output.TryGetProperty("rotation", out var r) ? r.GetInt32() : 1;
                    if (rotation == 2 || rotation == 8) (w, h) = (h, w);

                    string? vendor = null, product = null, serial = null;
                    if (output.TryGetProperty("edid", out var edid) && edid.ValueKind == JsonValueKind.Object)
                    {
                        vendor = OptionalString(edid, "vendor");
                        product = OptionalString(edid, "name");
                        serial = OptionalString(edid, "serial");
                    }

                    result.Add(new OutputGeometry(
                        name,
                        pos.GetProperty("x").GetInt32(),
                        pos.GetProperty("y").GetInt32(),
                        (int)Math.Round(w / scale),
                        (int)Math.Round(h / scale),
                        vendor, product, serial));
                }

                return result.Count > 0 ? result : null;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return null;
            }
        }

        static string? OptionalString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }

    /// <summary>
    /// GNOME: the reply to <c>org.gnome.Mutter.DisplayConfig.GetCurrentState</c> as gdbus prints
    /// it. Shape: (serial, monitors, logical_monitors, properties) where each monitor is
    /// ((connector, vendor, product, serial), modes, props), each mode is (id, width, height,
    /// refresh, preferred_scale, supported_scales, props) with 'is-current' in props, and each
    /// logical monitor is (x, y, scale, transform, primary, [monitor specs], props). In the
    /// default logical layout mode positions are logical pixels and sizes are mode size over
    /// scale; in physical mode both are device pixels.
    /// </summary>
    public static class MutterLayout
    {
        public static List<OutputGeometry>? Parse(string gvariantText)
        {
            try
            {
                if (GVariantText.Parse(gvariantText) is not List<object?> { Count: >= 3 } state) return null;
                if (state[1] is not List<object?> monitors || state[2] is not List<object?> logicalMonitors) return null;

                bool physicalLayout = state.Count >= 4
                                      && state[3] is Dictionary<string, object?> props
                                      && props.TryGetValue("layout-mode", out var mode)
                                      && mode is long and 2;

                //connector -> (current mode size, spec)
                var current = new Dictionary<string, (int W, int H, string? Vendor, string? Product, string? Serial)>(StringComparer.Ordinal);
                foreach (var monitor in monitors.OfType<List<object?>>())
                {
                    if (monitor.Count < 2 || monitor[0] is not List<object?> { Count: >= 4 } spec || spec[0] is not string connector) continue;

                    foreach (var m in (monitor[1] as List<object?>)?.OfType<List<object?>>() ?? [])
                    {
                        if (m.Count < 7 || m[1] is not long w || m[2] is not long h) continue;
                        if (m[6] is Dictionary<string, object?> modeProps && modeProps.TryGetValue("is-current", out var isCurrent) && isCurrent is true)
                        {
                            current[connector] = ((int)w, (int)h, Unknown(spec[1]), Unknown(spec[2]), Unknown(spec[3]));
                            break;
                        }
                    }
                }

                var result = new List<OutputGeometry>();
                foreach (var logical in logicalMonitors.OfType<List<object?>>())
                {
                    if (logical.Count < 6 || logical[0] is not long x || logical[1] is not long y) continue;
                    double scale = logical[2] switch { double d => d, long l => l, _ => 1.0 };
                    long transform = logical[3] as long? ?? 0;
                    if (scale <= 0) scale = 1.0;

                    foreach (var spec in (logical[5] as List<object?>)?.OfType<List<object?>>() ?? [])
                    {
                        if (spec.Count < 1 || spec[0] is not string connector || !current.TryGetValue(connector, out var cur)) continue;

                        var (w, h) = (cur.W, cur.H);
                        if (transform % 2 == 1) (w, h) = (h, w);    //1, 3, 5, 7: rotated 90 or 270
                        if (!physicalLayout)
                        {
                            w = (int)Math.Round(w / scale);
                            h = (int)Math.Round(h / scale);
                        }

                        result.Add(new OutputGeometry(connector, (int)x, (int)y, w, h, cur.Vendor, cur.Product, cur.Serial));
                    }
                }

                return result.Count > 0 ? result : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        //Mutter fills fields it couldn't read from the EDID with "unknown"
        static string? Unknown(object? value)
        {
            return value is string s && s.Length > 0 && !s.Equals("unknown", StringComparison.OrdinalIgnoreCase) ? s : null;
        }
    }

    /// <summary>
    /// X11 on any desktop: the output of <c>xrandr --verbose</c>, which lists each output's
    /// geometry (already rotated) and its EDID. X output names are the X driver's own, so the
    /// EDID is what pairs them with DRM connectors.
    /// </summary>
    public static partial class XrandrLayout
    {
        [GeneratedRegex(@"^(?<name>\S+) connected(?: primary)? (?<w>\d+)x(?<h>\d+)\+(?<x>-?\d+)\+(?<y>-?\d+)", RegexOptions.Multiline)]
        private static partial Regex OutputLine();

        public static List<OutputGeometry>? Parse(string text)
        {
            var lines = text.Split('\n');
            var result = new List<OutputGeometry>();

            for (int i = 0; i < lines.Length; i++)
            {
                var match = OutputLine().Match(lines[i]);
                if (!match.Success) continue;

                //The EDID block is indented under the output, one hex line per row
                byte[]? edid = null;
                for (int j = i + 1; j < lines.Length && (lines[j].Length == 0 || char.IsWhiteSpace(lines[j][0])); j++)
                {
                    if (lines[j].Trim() != "EDID:") continue;

                    var hex = new System.Text.StringBuilder();
                    for (int k = j + 1; k < lines.Length; k++)
                    {
                        var row = lines[k].Trim();
                        if (row.Length == 0 || !row.All(Uri.IsHexDigit)) break;
                        hex.Append(row);
                    }

                    if (hex.Length >= 2 * Edid.BaseBlockLength) edid = Convert.FromHexString(hex.ToString());
                    break;
                }

                result.Add(new OutputGeometry(
                    match.Groups["name"].Value,
                    int.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["w"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture),
                    Edid: edid));
            }

            return result.Count > 0 ? result : null;
        }
    }
}
