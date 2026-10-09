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
    /// its bounds in physical pixels, and whatever identity the compositor exposes for pairing
    /// it with a DRM connector (see <see cref="LayoutJoin"/>). Wayland compositors name outputs
    /// by their DRM connector; X drivers use their own names, so X11 pairs by EDID instead.
    /// Physical pixels because monitor ids are made from these bounds: a Wayland desktop works
    /// in a layout divided by its scale, so ids taken from that would change whenever the scale
    /// does, and would differ from the ids Windows gives the same monitors.
    /// </summary>
    public sealed record OutputGeometry(
        string Name, int X, int Y, int Width, int Height,
        string? Vendor = null, string? Product = null, string? Serial = null, byte[]? Edid = null)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
    }

    /// <summary>
    /// An output as a Wayland compositor reports it, its logical position and size beside its
    /// size in pixels, on its way to an <see cref="OutputGeometry"/> in physical pixels.
    /// </summary>
    sealed record ScaledOutput(string Name, PhysicalLayout.Output Geometry, string? Vendor, string? Product, string? Serial);

    static class ScaledLayout
    {
        /// <summary>The outputs in physical pixels, in the order given (see <see cref="PhysicalLayout"/>); null when there are none.</summary>
        public static List<OutputGeometry>? ToPhysical(List<ScaledOutput> outputs)
        {
            if (outputs.Count == 0) return null;

            var physical = PhysicalLayout.ToPhysical(outputs.ToDictionary(o => o.Name, o => o.Geometry, StringComparer.Ordinal));
            return outputs.Select(o =>
            {
                var r = physical[o.Name];
                return new OutputGeometry(o.Name, r.X, r.Y, r.Width, r.Height, o.Vendor, o.Product, o.Serial);
            }).ToList();
        }
    }

    /// <summary>
    /// KDE Plasma: the JSON printed by <c>kscreen-doctor -j</c>. Positions are logical, sizes
    /// are the mode in pixels with the output's own scale beside them.
    /// </summary>
    public static class KScreenLayout
    {
        public static List<OutputGeometry>? Parse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var outputs = new List<ScaledOutput>();

                foreach (var output in doc.RootElement.GetProperty("outputs").EnumerateArray())
                {
                    if (output.TryGetProperty("enabled", out var enabled) && !enabled.GetBoolean()) continue;
                    if (output.TryGetProperty("connected", out var connected) && !connected.GetBoolean()) continue;

                    var name = output.GetProperty("name").GetString();
                    if (string.IsNullOrEmpty(name) || outputs.Any(o => o.Name == name)) continue;

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

                    outputs.Add(new ScaledOutput(
                        name,
                        new PhysicalLayout.Output(
                            pos.GetProperty("x").GetInt32(),
                            pos.GetProperty("y").GetInt32(),
                            (int)Math.Round(w / scale),
                            (int)Math.Round(h / scale),
                            w, h),
                        vendor, product, serial));
                }

                return ScaledLayout.ToPhysical(outputs);
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

    /// <summary>One monitor as Mutter describes it: its connector, the identity read from its EDID, and the size of its current mode (null while it has none).</summary>
    public sealed record MutterMonitor(string Connector, string? Vendor, string? Product, string? Serial, (int Width, int Height)? CurrentMode);

    /// <summary>One logical monitor: where it sits on the desktop, and the connectors showing it (several when mirrored).</summary>
    public sealed record MutterLogicalMonitor(int X, int Y, double Scale, uint Transform, IReadOnlyList<string> Connectors);

    /// <summary>
    /// GNOME: the layout in the reply to <c>org.gnome.Mutter.DisplayConfig.GetCurrentState</c>
    /// (read off the bus by MutterDisplayConfig). In the default logical layout mode positions
    /// are logical pixels and sizes are mode size over scale; in physical mode (X11) both are
    /// device pixels already. Either way the result is in physical pixels.
    /// </summary>
    public static class MutterLayout
    {
        /// <summary>The value of the state's <c>layout-mode</c> property that means physical pixels.</summary>
        public const uint PhysicalLayoutMode = 2;

        public static List<OutputGeometry>? Build(IReadOnlyList<MutterMonitor> monitors, IReadOnlyList<MutterLogicalMonitor> logicalMonitors, bool physicalLayout)
        {
            var current = new Dictionary<string, MutterMonitor>(StringComparer.Ordinal);
            foreach (var monitor in monitors)
            {
                if (monitor.CurrentMode != null) current[monitor.Connector] = monitor;
            }

            var outputs = new List<ScaledOutput>();
            foreach (var logical in logicalMonitors)
            {
                double scale = logical.Scale > 0 ? logical.Scale : 1.0;

                foreach (var connector in logical.Connectors)
                {
                    if (!current.TryGetValue(connector, out var monitor) || outputs.Any(o => o.Name == connector)) continue;

                    var (w, h) = monitor.CurrentMode!.Value;
                    if (logical.Transform % 2 == 1) (w, h) = (h, w);    //1, 3, 5, 7: rotated 90 or 270

                    int logicalWidth = physicalLayout ? w : (int)Math.Round(w / scale);
                    int logicalHeight = physicalLayout ? h : (int)Math.Round(h / scale);

                    outputs.Add(new ScaledOutput(
                        connector,
                        new PhysicalLayout.Output(logical.X, logical.Y, logicalWidth, logicalHeight, w, h),
                        monitor.Vendor, monitor.Product, monitor.Serial));
                }
            }

            return ScaledLayout.ToPhysical(outputs);
        }

        /// <summary>Mutter fills the identity fields it couldn't read from the EDID with "unknown".</summary>
        public static string? Known(string? value)
        {
            return !string.IsNullOrEmpty(value) && !value.Equals("unknown", StringComparison.OrdinalIgnoreCase) ? value : null;
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
