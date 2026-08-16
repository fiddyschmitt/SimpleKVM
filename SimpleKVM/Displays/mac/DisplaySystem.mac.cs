using SimpleKVM.Configuration;
using SimpleKVM.Displays.I2C;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace SimpleKVM.Displays.mac
{
    [SupportedOSPlatform("macos")]
    public static class DisplaySystem
    {
        static readonly object cacheLock = new();
        static List<Monitor>? cachedMonitorList;

        public static IList<Monitor> GetMonitors()
        {
            lock (cacheLock)
            {
                return GetMonitorsUnsynchronized();
            }
        }

        static List<Monitor> GetMonitorsUnsynchronized()
        {
            var displays = EnumerateDisplays();

            bool refreshRequired;
            if (cachedMonitorList == null)
            {
                refreshRequired = true;
            }
            else
            {
                var allScreens = displays.Select(d => d.UniqueId);
                var allMonitors = cachedMonitorList.Select(mon => mon.MonitorUniqueId);

                refreshRequired = allMonitors.Except(allScreens).Any() || allScreens.Except(allMonitors).Any();
                refreshRequired |= cachedMonitorList.Any(mon => mon.ValidSources.Count == 0);
            }

            if (cachedMonitorList == null || refreshRequired)
            {
                cachedMonitorList = displays.Select(BuildMonitor).ToList();
            }

            return cachedMonitorList;
        }

        record DisplayInfo(uint DisplayId, int Left, int Top, int Right, int Bottom, string UniqueId, int MonitorNumber, DdcTransport? Transport);

        static List<DisplayInfo> EnumerateDisplays()
        {
            var avServices = AVServiceMatcher.GetExternalAvServices();

            var externals = CoreGraphicsNative
                            .GetActiveDisplays()
                            .Where(id => !CoreGraphicsNative.CGDisplayIsBuiltin(id))
                            .Select(id =>
                            {
                                var bounds = CoreGraphicsNative.CGDisplayBounds(id);
                                int left = (int)Math.Round(bounds.X);
                                int top = (int)Math.Round(bounds.Y);
                                int right = (int)Math.Round(bounds.X + bounds.Width);
                                int bottom = (int)Math.Round(bounds.Y + bounds.Height);
                                return (id, left, top, right, bottom);
                            })
                            .OrderBy(d => d.left)
                            .ThenBy(d => d.top)
                            .ToList();

            //v1 pairing: external displays and external AV services in enumeration order.
            //Correct for a single display; multi-monitor matching is a known follow-up.
            return externals
                    .Select((d, index) => new DisplayInfo(
                        d.id, d.left, d.top, d.right, d.bottom,
                        MonitorIdentity.FromBounds(d.left, d.top, d.right, d.bottom),
                        index + 1,
                        index < avServices.Count ? new DdcTransport(avServices[index]) : null))
                    .ToList();
        }

        static Monitor BuildMonitor(DisplayInfo display)
        {
            List<(int SourceId, string SourceName)>? sources = null;

            //First the config file, in case the user specified a custom list of sources for this monitor
            var monitorOverride = ConfigManager
                        .Current?
                        .Overrides?
                        .MonitorOverrides?
                        .FirstOrDefault(ovr => ovr.MonitorNumber == display.MonitorNumber);

            sources = monitorOverride?
                        .Sources
                        .Select(src => (src.SourceId, src.SourceName))
                        .ToList();

            if (sources != null && sources.Count == 0)
                sources = null;

            bool userSpecifiedSources = sources != null;

            //Second, the monitor's capabilities string (model + valid sources)
            var model = "Unknown";
            ushort edidManufacturer = 0;

            var edid = display.Transport?.ReadEdid();
            if (edid != null && edid.Length >= 128)
            {
                edidManufacturer = (ushort)((edid[8] << 8) | edid[9]);
                model = EdidModelName(edid) ?? model;
            }

            var caps = display.Transport?.ReadCapabilitiesString();
            if (caps != null)
            {
                var parsed = CapabilitiesParser.Parse(caps);

                if (!string.IsNullOrEmpty(parsed.Model))
                    model = parsed.Model;

                if (sources == null && parsed.VcpFeatures.TryGetValue(0x60, out var inputSources))
                {
                    sources = inputSources
                                .Select(sourceId => ((int)sourceId, VcpSourceNames.SourceIdToName(sourceId)))
                                .ToList();
                }
            }

            if ((sources == null || sources.Count == 0) &&
                display.Transport != null && display.Transport.GetVcp(0x60, out _))
            {
                sources =
                [
                    (0x11, "HDMI 1"),
                    (0x12, "HDMI 2"),
                    (0x0F, "DisplayPort 1"),
                    (0x10, "DisplayPort 2"),
                    (0x03, "DVI 1"),
                    (0x01, "VGA 1"),
                ];
            }

            //LG monitors ignore VCP 0x60 writes; use the 0xF4 sidechannel unless overridden
            bool useLgAltMode = false;
            if (monitorOverride?.UseLgAltMode == true)
            {
                useLgAltMode = true;
            }
            else if (monitorOverride?.UseLgAltMode == null)
            {
                useLgAltMode = edidManufacturer == LgInputSources.EdidManufacturerId
                || model.Contains("LG", StringComparison.OrdinalIgnoreCase);
            }

            if (useLgAltMode && !userSpecifiedSources)
            {
                sources = LgInputSources.GetDefaultSources();
            }

            sources ??= [];

            var newMonitor = new Monitor(display.UniqueId, model, sources)
            {
                UseLgAltMode = useLgAltMode,
                Transport = display.Transport
            };

            return newMonitor;
        }

        static string? EdidModelName(byte[] edid)
        {
            //Display name lives in an 18-byte descriptor block tagged 0xFC
            foreach (int offset in new[] { 54, 72, 90, 108 })
            {
                if (offset + 18 > edid.Length) break;
                if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 3] != 0xFC) continue;

                var name = Encoding.ASCII.GetString(edid, offset + 5, 13);
                int newline = name.IndexOf('\n');
                if (newline >= 0) name = name[..newline];
                name = name.Trim();

                return name.Length > 0 ? name : null;
            }

            return null;
        }

        public static Dictionary<string, int> GetCurrentSources()
        {
            var result = new Dictionary<string, int>();

            foreach (var mon in GetMonitors())
            {
                if (mon.UseLgAltMode) continue;     //the sidechannel input can't be read back

                var current = mon.GetCurrentSource();
                if (current > 0)
                {
                    result[mon.MonitorUniqueId] = current;
                }
            }

            return result;
        }
    }
}
