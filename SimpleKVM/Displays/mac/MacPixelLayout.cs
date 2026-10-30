using SimpleKVM.Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SimpleKVM.Displays.mac
{
    /// <summary>One display as macOS reports it: its bounds in points, whether it is built in, and its panel's native resolution in pixels (0 when unknown).</summary>
    public sealed record MacDisplay(uint Id, bool BuiltIn, double X, double Y, double Width, double Height, int NativeWidth, int NativeHeight);

    /// <summary>A display's bounds in pixels, which monitor ids are made from, and in points, which ids were made from before 2.5.0.</summary>
    public sealed record MacScreen(uint Id, bool BuiltIn, ScreenRect Pixels, ScreenRect Points);

    /// <summary>
    /// macOS arranges displays in points. A Retina screen, or a 4K monitor at a scaled ("looks
    /// like") resolution, is fewer points than it has pixels, so its bounds in points change
    /// whenever its scaling does, and so do the bounds of the monitors arranged beside it. Monitor
    /// ids made from points therefore changed with every scaling change, orphaning the rules that
    /// target those monitors, and differed from the ids Windows gives the same monitors. Here each
    /// display's native resolution and the arrangement in points become a layout in pixels
    /// (see <see cref="PhysicalLayout"/>), which scaling doesn't touch. Pure arithmetic, so it is
    /// tested without a Mac.
    /// </summary>
    public static class MacPixelLayout
    {
        public static List<MacScreen> Build(IReadOnlyList<MacDisplay> displays)
        {
            var outputs = new Dictionary<string, PhysicalLayout.Output>(StringComparer.Ordinal);
            var points = new Dictionary<uint, ScreenRect>();

            foreach (var display in displays)
            {
                int x = (int)Math.Round(display.X), y = (int)Math.Round(display.Y);
                int width = (int)Math.Round(display.Width), height = (int)Math.Round(display.Height);
                points[display.Id] = new ScreenRect(x, y, x + width, y + height);

                //Display modes describe the panel as it is built; a rotated display is laid out turned
                var (pixelWidth, pixelHeight) = (display.NativeWidth, display.NativeHeight);
                if (pixelWidth <= 0 || pixelHeight <= 0)
                {
                    (pixelWidth, pixelHeight) = (width, height);     //native resolution unknown: as before, points
                }
                else if (pixelWidth != pixelHeight && (pixelWidth > pixelHeight) != (width > height))
                {
                    (pixelWidth, pixelHeight) = (pixelHeight, pixelWidth);
                }

                outputs[Key(display.Id)] = new PhysicalLayout.Output(x, y, width, height, pixelWidth, pixelHeight);
            }

            var pixels = PhysicalLayout.ToPhysical(outputs);

            return displays
                    .Select(display =>
                    {
                        var r = pixels[Key(display.Id)];
                        return new MacScreen(display.Id, display.BuiltIn, new ScreenRect(r.X, r.Y, r.X + r.Width, r.Y + r.Height), points[display.Id]);
                    })
                    .ToList();
        }

        /// <summary>
        /// The ids versions before 2.5.0 gave the external displays connected now, mapped to the
        /// ids those displays have now, so rules saved by those versions can be moved over (see
        /// <see cref="Rules.MonitorIdMigration"/>). Left out: displays whose id didn't change (no
        /// scaling anywhere near them), an earlier id that is also someone's current id (a rule
        /// using it matches already), and an earlier id two displays shared (mirrored).
        /// </summary>
        public static Dictionary<string, string> EarlierIds(IReadOnlyList<MacScreen> screens)
        {
            var external = screens.Where(s => !s.BuiltIn).ToList();
            var current = external.Select(s => Id(s.Pixels)).ToHashSet(StringComparer.Ordinal);

            return external
                    .GroupBy(s => Id(s.Points), StringComparer.Ordinal)
                    .Where(g => g.Count() == 1 && !current.Contains(g.Key))
                    .ToDictionary(g => g.Key, g => Id(g.Single().Pixels), StringComparer.Ordinal);
        }

        public static string Id(ScreenRect r) => MonitorIdentity.FromBounds(r.Left, r.Top, r.Right, r.Bottom);

        static string Key(uint displayId) => displayId.ToString(CultureInfo.InvariantCulture);
    }
}
