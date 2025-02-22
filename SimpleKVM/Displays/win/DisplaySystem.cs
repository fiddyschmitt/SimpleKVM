using DDCKVMService;
using SimpleKVM.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SimpleKVM.Displays.win
{
    public static partial class DisplaySystem
    {
        static readonly Regex modelRegex = ModelRegex();
        static readonly Regex sourcesRegex = SourcesRegex();

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
                cachedMonitorList = [];
                MonitorController.EnumMonitors(mon =>
                {
                    List<(int SourceId, string SourceName)>? sources = null;

                    //Determine all the sources the monitor supports.

                    //First we'll check the config file, to see if the user has specified a custom list of sources for this monitor
                    var monitorNumber = Screen.AllScreens.FirstOrDefault(s => s.GetUniqueId() == mon.UniqueId)?.ScreenIndex();
                    if (monitorNumber.HasValue)
                    {
                        sources = Form1
                                    .Config?
                                    .Overrides?
                                    .MonitorOverrides?
                                    .FirstOrDefault(ovr => ovr.MonitorNumber == monitorNumber)?
                                    .Sources
                                    .Select(src => (src.SourceId, src.SourceName))
                                    .ToList();
                    }

                    //Second, we'll query the monitor for its capabilities string, which contains the model & valid sources (if still required)
                    var caps = mon.PhysicalMonitor.GetVCPCapabilities();

                    var model = "Unknown";
                    if (caps != null)
                    {
                        model = modelRegex.Match(caps).Groups[1].Value;

                        sources ??= sourcesRegex.Match(caps).Groups[1].Value.Split(' ')
                                        .Where(x => !string.IsNullOrWhiteSpace(x))
                                        .Select(x => (int)(Convert.ToUInt32(x, 16)))
                                        .Select(sourceId => (sourceId, UcSelectMonitorSource.SourceIdToName(sourceId)))
                                        .ToList();
                    }

                    sources ??= [];

                    var newMonitor = new Monitor(mon.UniqueId, model, sources);

                    cachedMonitorList.Add(newMonitor);
                });
            }

            return cachedMonitorList;
        }

        [GeneratedRegex(@"model\((.*?)\)")]
        private static partial Regex ModelRegex();
        [GeneratedRegex(@"(?<=\s)60\((.*?)\)")]
        private static partial Regex SourcesRegex();
    }
}
