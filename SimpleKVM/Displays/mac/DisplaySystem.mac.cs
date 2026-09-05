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
        /// <summary>
        /// A monitor that answered no DDC/CI query when it was probed (off, showing another PC's
        /// input, or simply without DDC/CI) is probed again so it picks up its source list once it
        /// is back - but no more often than this, so a display that never answers doesn't turn
        /// every GetMonitors call into a multi-second capabilities read.
        /// </summary>
        static readonly TimeSpan ProbeRetryInterval = TimeSpan.FromSeconds(30);

        static readonly object cacheLock = new();
        static List<DisplayInfo>? cachedDisplays;
        static List<Monitor>? cachedMonitorList;
        static DateTime lastProbe = DateTime.MinValue;

        public static IList<Monitor> GetMonitors()
        {
            lock (cacheLock)
            {
                return GetMonitorsUnsynchronized();
            }
        }

        /// <summary>Drops the cache so the next GetMonitors re-enumerates and re-probes every monitor.</summary>
        public static void InvalidateMonitors()
        {
            lock (cacheLock)
            {
                cachedDisplays = null;
                cachedMonitorList = null;
            }
        }

        static List<Monitor> GetMonitorsUnsynchronized()
        {
            //CoreGraphics only, so cheap enough to run on every call
            var displays = EnumerateExternalDisplays();

            if (cachedDisplays == null || cachedMonitorList == null || LayoutChanged(displays))
            {
                //The set of displays changed (a monitor was turned on or off, unplugged, or moved):
                //everything is re-enumerated. Pairing displays with AV services creates IOAVService
                //objects, so it only happens here. The previous cache's services are deliberately
                //not released: a rule that is mid-run may still be talking through one of them, and
                //layout changes are rare enough for the leak not to matter.
                cachedDisplays = AttachTransports(displays);
                cachedMonitorList = cachedDisplays.Select(BuildMonitor).ToList();
                lastProbe = DateTime.Now;
            }
            else if (cachedMonitorList.Any(mon => mon.ValidSources.Count == 0) && DateTime.Now - lastProbe >= ProbeRetryInterval)
            {
                //Probe only the monitors that had no sources last time; the rest keep their cached state
                var known = cachedDisplays;
                cachedMonitorList = cachedMonitorList
                                        .Select(mon => mon.ValidSources.Count > 0
                                                        ? mon
                                                        : BuildMonitor(known.First(d => d.UniqueId == mon.MonitorUniqueId)))
                                        .ToList();
                lastProbe = DateTime.Now;
            }

            return cachedMonitorList;
        }

        static bool LayoutChanged(List<DisplayInfo> displays)
        {
            var current = displays.Select(d => d.UniqueId);
            var cached = cachedDisplays!.Select(d => d.UniqueId);

            return current.Except(cached).Any() || cached.Except(current).Any();
        }

        record DisplayInfo(uint DisplayId, int Left, int Top, int Right, int Bottom, string UniqueId, int MonitorNumber, DdcTransport? Transport);

        /// <summary>
        /// The external displays as CoreGraphics lays them out, without DDC transports. Monitor
        /// numbers count every screen, the built-in one included, so they match the numbers in
        /// the rule editor's layout and the MonitorOverrides in config.json (as on Windows).
        /// </summary>
        static List<DisplayInfo> EnumerateExternalDisplays()
        {
            return CoreGraphicsNative
                    .GetActiveDisplays()
                    .Select(id =>
                    {
                        var bounds = CoreGraphicsNative.CGDisplayBounds(id);
                        int left = (int)Math.Round(bounds.X);
                        int top = (int)Math.Round(bounds.Y);
                        int right = (int)Math.Round(bounds.X + bounds.Width);
                        int bottom = (int)Math.Round(bounds.Y + bounds.Height);
                        bool builtin = CoreGraphicsNative.CGDisplayIsBuiltin(id);
                        return (id, left, top, right, bottom, builtin);
                    })
                    .OrderBy(d => d.left)
                    .ThenBy(d => d.top)
                    .Select((d, index) => (Display: d, Number: index + 1))
                    .Where(entry => !entry.Display.builtin)
                    .Select(entry => new DisplayInfo(
                        entry.Display.id, entry.Display.left, entry.Display.top, entry.Display.right, entry.Display.bottom,
                        MonitorIdentity.FromBounds(entry.Display.left, entry.Display.top, entry.Display.right, entry.Display.bottom),
                        entry.Number,
                        Transport: null))
                    .ToList();
        }

        /// <summary>
        /// v1 pairing: external displays and external AV services in enumeration order.
        /// Correct for a single display; multi-monitor matching is a known follow-up.
        /// </summary>
        static List<DisplayInfo> AttachTransports(List<DisplayInfo> displays)
        {
            var avServices = AVServiceMatcher.GetExternalAvServices();

            return displays
                    .Select((d, index) => d with { Transport = index < avServices.Count ? new DdcTransport(avServices[index]) : null })
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

            Action<string>? ddcDebug = Environment.GetEnvironmentVariable("SIMPLEKVM_DDC_DEBUG") == "1"
                                        ? msg => Console.Error.WriteLine($"[caps] {msg}")
                                        : null;
            var caps = display.Transport?.ReadCapabilitiesString(ddcDebug);
            if (caps != null)
            {
                var parsed = CapabilitiesParser.Parse(caps);

                //The EDID display name (e.g. "S240HL") is usually more specific than the
                //capabilities model (often just the brand), so only fill a gap here
                if (!string.IsNullOrEmpty(parsed.Model) && model == "Unknown")
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
