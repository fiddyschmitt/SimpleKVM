using SimpleKVM.Configuration;
using SimpleKVM.Displays.Ddc;
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
            var probe = DdcMonitorBuilder.Run(display.MonitorNumber, edid: null, display.Transport);

            return new Monitor(display.UniqueId, probe.Model, probe.Sources)
            {
                UseLgAltMode = probe.UseLgAltMode,
                Transport = display.Transport
            };
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
