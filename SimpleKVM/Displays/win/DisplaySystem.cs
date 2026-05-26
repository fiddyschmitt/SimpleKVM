using DDCKVMService;
using SimpleKVM.Configuration;
using SimpleKVM.Displays.win.I2C;
using SimpleKVM.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SimpleKVM.Displays.win
{
    public static class DisplaySystem
    {
        static List<Monitor>? cachedMonitorList;

        public static IList<Monitor> GetMonitors()
        {
            bool refreshRequired;
            if (cachedMonitorList == null)
            {
                refreshRequired = true;
            }
            else
            {
                //confirm all screens are in the cache
                var allScreens = Screen.AllScreens.Select(screen => screen.GetUniqueId());
                var allMonitors = cachedMonitorList.Select(mon => mon.MonitorUniqueId);

                refreshRequired = allMonitors.Except(allScreens).Any() || allScreens.Except(allMonitors).Any();

                refreshRequired |= cachedMonitorList.Any(mon => mon.ValidSources.Count == 0);
            }


            if (cachedMonitorList == null || refreshRequired)
            {
                try
                {
                    I2CTransportManager.Initialize();
                    var edidDisplays = EdidHelper.GetDisplayEdidInfo();
                    I2CTransportManager.BuildDisplayMap(edidDisplays);
                }
                catch
                {
                }

                cachedMonitorList = [];
                MonitorController.EnumMonitors(mon =>
                {
                    List<(int SourceId, string SourceName)>? sources = null;

                    //First we'll check the config file, to see if the user has specified a custom list of sources for this monitor
                    var monitorNumber = Screen.AllScreens.FirstOrDefault(s => s.GetUniqueId() == mon.UniqueId)?.ScreenIndex();
                    MonitorOverride? monitorOverride = null;
                    if (monitorNumber.HasValue)
                    {
                        monitorOverride = Form1
                                    .Config?
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
                                        .Select(sourceId => ((int)sourceId, UcSelectMonitorSource.SourceIdToName(sourceId)))
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

                    // Determine LG alt mode
                    bool useLgAltMode = false;
                    if (monitorOverride?.UseLgAltMode == true)
                    {
                        useLgAltMode = true;
                    }
                    else if (monitorOverride?.UseLgAltMode == null)
                    {
                        useLgAltMode = model.Contains("LG", StringComparison.OrdinalIgnoreCase)
                        && (sources == null || sources.Count == 0);
                    }

                    if (useLgAltMode && sources == null)
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

                    cachedMonitorList.Add(newMonitor);
                });
            }

            return cachedMonitorList;
        }
    }
}
