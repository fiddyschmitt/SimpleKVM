using SimpleKVM.Configuration;
using SimpleKVM.Displays.I2C;
using SimpleKVM.Displays.win.I2C;
using System;
using System.Collections.Generic;
using System.Linq;
using SimpleKVM.Platform.win;
using System.Runtime.Versioning;

namespace SimpleKVM.Displays.win
{
    [SupportedOSPlatform("windows6.1")]
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
        static List<Monitor>? cachedMonitorList;
        static HashSet<string> cachedScreenIds = [];
        static List<EdidDisplayInfo> cachedEdidDisplays = [];
        static DateTime lastProbe = DateTime.MinValue;

        public static IList<Monitor> GetMonitors()
        {
            //GetMonitors is called from the warm-up task, the UI thread and rule trigger threads,
            //so the cache check and refresh have to happen atomically
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
                cachedMonitorList = null;
            }
        }

        static List<Monitor> GetMonitorsUnsynchronized()
        {
            var screens = WindowsScreens.All();

            if (cachedMonitorList == null || LayoutChanged(screens))
            {
                //The set of screens changed (a monitor was turned on or off, unplugged, or moved):
                //everything is re-enumerated. The cache is only in memory; rules keep referring to
                //their monitors by id and pick them up again when they return.
                RebuildAll(screens);
            }
            else if (ProbeRetryDue(screens))
            {
                ReprobeUnanswered(screens);
            }

            return cachedMonitorList!;   //assigned by both rebuild paths
        }

        static bool LayoutChanged(List<WindowsScreen> screens)
        {
            return !cachedScreenIds.SetEquals(screens.Select(screen => screen.UniqueId));
        }

        static bool ProbeRetryDue(List<WindowsScreen> screens)
        {
            if (DateTime.Now - lastProbe < ProbeRetryInterval) return false;

            var monitors = cachedMonitorList!;
            return monitors.Any(mon => mon.ValidSources.Count == 0)
                    || screens.Any(screen => !monitors.Any(mon => mon.MonitorUniqueId == screen.UniqueId));
        }

        static void RebuildAll(List<WindowsScreen> screens)
        {
            cachedEdidDisplays = [];
            try
            {
                I2CTransportManager.Initialize();
                cachedEdidDisplays = EdidHelper.GetDisplayEdidInfo();
                I2CTransportManager.BuildDisplayMap(cachedEdidDisplays);
            }
            catch
            {
            }

            var monitors = new List<Monitor>();
            MonitorController.EnumMonitors(mon => monitors.Add(BuildMonitor(mon, screens)));

            cachedMonitorList = monitors;
            cachedScreenIds = screens.Select(screen => screen.UniqueId).ToHashSet();
            lastProbe = DateTime.Now;
        }

        /// <summary>
        /// Probes only the monitors that had no sources last time (or no physical monitor at all);
        /// the ones that answered keep their cached state, LG transport handles included.
        /// </summary>
        static void ReprobeUnanswered(List<WindowsScreen> screens)
        {
            var previous = cachedMonitorList!;

            var monitors = new List<Monitor>();
            MonitorController.EnumMonitors(mon =>
            {
                var cached = previous.FirstOrDefault(c => c.MonitorUniqueId == mon.UniqueId);
                monitors.Add(cached != null && cached.ValidSources.Count > 0 ? cached : BuildMonitor(mon, screens));
            });

            cachedMonitorList = monitors;
            lastProbe = DateTime.Now;
        }

        static Monitor BuildMonitor((IntPtr hMonitor, MonitorController.PHYSICAL_MONITOR PhysicalMonitor, string UniqueId) mon, List<WindowsScreen> screens)
        {
            List<(int SourceId, string SourceName)>? sources = null;

            //First we'll check the config file, to see if the user has specified a custom list of sources for this monitor
            var monitorNumber = screens.FirstOrDefault(s => s.UniqueId == mon.UniqueId)?.ScreenNumber(screens);
            MonitorOverride? monitorOverride = null;
            if (monitorNumber.HasValue)
            {
                monitorOverride = ConfigManager
                            .Current?
                            .Overrides?
                            .MonitorOverrides?
                            .FirstOrDefault(ovr => ovr.MonitorNumber == monitorNumber);

                sources = monitorOverride?
                            .Sources
                            .Select(src => (src.SourceId, src.SourceName))
                            .ToList();

                if (sources != null && sources.Count == 0)
                    sources = null;
            }

            bool userSpecifiedSources = sources != null;

            //Second, we'll query the monitor for its capabilities string, which contains the model & valid sources (if still required)
            var caps = mon.PhysicalMonitor.GetVCPCapabilities();

            var model = "Unknown";
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

            if (sources == null || sources.Count == 0)
            {
                if (mon.PhysicalMonitor.GetVCPRegister(0x60, out _))
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
            }

            // Determine LG alt mode. LG monitors answer VCP 0x60 reads and report sources in
            // their capabilities string, but ignore VCP 0x60 writes, so every LG monitor
            // (EDID manufacturer GSM) defaults to the 0xF4 sidechannel unless overridden.
            bool useLgAltMode = false;
            if (monitorOverride?.UseLgAltMode == true)
            {
                useLgAltMode = true;
            }
            else if (monitorOverride?.UseLgAltMode == null)
            {
                var edidInfo = cachedEdidDisplays.FirstOrDefault(e => e.UniqueId == mon.UniqueId);
                useLgAltMode = edidInfo?.EdidManufacturerId == LgInputSources.EdidManufacturerId
                || model.Contains("LG", StringComparison.OrdinalIgnoreCase);
            }

            // The sidechannel has its own source id namespace, so sources derived from the
            // capabilities string don't apply to it
            if (useLgAltMode && !userSpecifiedSources)
            {
                sources = LgInputSources.GetDefaultSources();
            }

            sources ??= [];

            var newMonitor = new Monitor(mon.UniqueId, model, sources);

            if (useLgAltMode)
            {
                newMonitor.UseLgAltMode = true;
                var transport = I2CTransportManager.GetTransportForDisplay(mon.UniqueId);
                if (transport.HasValue)
                {
                    newMonitor.I2CTransport = transport.Value.Transport;
                    newMonitor.I2CDisplayHandle = transport.Value.DisplayHandle;
                }
            }

            return newMonitor;
        }

        public static Dictionary<string, int> GetCurrentSources()
        {
            //Read VCP 0x60 for every physical monitor in a single enumeration pass. Calling
            //Monitor.GetCurrentSource() per monitor would re-enumerate every monitor each time.
            var result = new Dictionary<string, int>();

            MonitorController.EnumMonitors(mon =>
            {
                if (result.ContainsKey(mon.UniqueId)) return;

                if (mon.PhysicalMonitor.GetVCPRegister(0x60, out uint currentSource))
                {
                    result[mon.UniqueId] = (int)currentSource;
                }
            });

            return result;
        }
    }
}
