using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Displays.linux
{
    /// <summary>A connected DRM connector from sysfs: "DP-1", "HDMI-A-1", ... with its EDID.</summary>
    public sealed record ConnectorInfo(string Name, string SysfsPath, byte[] Edid);

    /// <summary>
    /// Pairs the desktop's outputs with the kernel's connectors. The connector is what the
    /// DDC bus hangs off; the output is where the monitor sits on the desktop. Rules, per
    /// output: the same name (Wayland compositors use DRM names), then the same EDID (xrandr
    /// prints it), then the same vendor/product/serial (Mutter reports them). When nothing
    /// pairs at all the caller must not trust the layout: on X11 the names never match, so a
    /// name-only join would silently drop every monitor.
    /// </summary>
    public static class LayoutJoin
    {
        public sealed record Pair(ConnectorInfo Connector, OutputGeometry Output);

        /// <summary>Null when no output could be paired with any connector.</summary>
        public static List<Pair>? Match(IReadOnlyList<ConnectorInfo> connectors, IReadOnlyList<OutputGeometry> outputs)
        {
            var unmatched = connectors.ToList();
            var pairs = new List<Pair>();

            foreach (var output in outputs)
            {
                var connector = unmatched.FirstOrDefault(c => string.Equals(c.Name, output.Name, StringComparison.OrdinalIgnoreCase))
                                ?? (output.Edid != null ? unmatched.FirstOrDefault(c => SameEdid(c.Edid, output.Edid)) : null)
                                ?? (output.Vendor != null || output.Product != null || output.Serial != null
                                        ? unmatched.FirstOrDefault(c => SameIdentity(c.Edid, output))
                                        : null);
                if (connector == null) continue;

                unmatched.Remove(connector);
                pairs.Add(new Pair(connector, output));
            }

            return pairs.Count > 0 ? pairs : null;
        }

        static bool SameEdid(byte[] a, byte[] b)
        {
            return Edid.IsValid(a) && Edid.IsValid(b) && a.AsSpan(0, Edid.BaseBlockLength).SequenceEqual(b.AsSpan(0, Edid.BaseBlockLength));
        }

        /// <summary>
        /// Mutter's spec: vendor is the PNP id, product the EDID model name, serial the EDID serial
        /// string (or the number when there's no string). A field Mutter reports as unknown matches anything.
        /// </summary>
        static bool SameIdentity(byte[] edid, OutputGeometry output)
        {
            if (!Edid.IsValid(edid)) return false;

            if (output.Vendor != null && !output.Vendor.Equals(Edid.PnpId(edid), StringComparison.OrdinalIgnoreCase)) return false;
            if (output.Product != null && !output.Product.Equals(Edid.ModelName(edid), StringComparison.OrdinalIgnoreCase)) return false;

            if (output.Serial != null)
            {
                var serialString = Edid.SerialString(edid);
                var serialNumber = Edid.SerialNumber(edid).ToString();
                if (!output.Serial.Equals(serialString, StringComparison.OrdinalIgnoreCase) && output.Serial != serialNumber) return false;
            }

            return output.Vendor != null || output.Product != null || output.Serial != null;
        }
    }
}
