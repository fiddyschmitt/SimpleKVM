using DDCKVMService;
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
    public static class DisplaySystem
    {
        static readonly Regex modelRegex = new(@"model\((.*?)\)");
        static readonly Regex sourcesRegex = new(@"(?<=\s)60\((.*?)\)");

        static List<Monitor>? cachedMonitorList;

        public static IList<Monitor> GetMonitors()
        {
            var monitorsChanged = !cachedMonitorList?.All(mon => Screen.AllScreens.Any(scr => scr.DeviceName.Equals(mon.MonitorUniqueId))) ?? false;

            if (cachedMonitorList == null || monitorsChanged)
            {
                cachedMonitorList = [];
                MonitorController.EnumMonitors(mon =>
                {
                    var caps = mon.PhysicalMonitor.GetVCPCapabilities();

                    var model = "Unknown";
                    var sources = Array.Empty<uint>();

                    if (caps != null)
                    {
                        model = modelRegex.Match(caps).Groups[1].Value;

                        sources = sourcesRegex.Match(caps).Groups[1].Value.Split(' ')
                                        .Where(x => !string.IsNullOrWhiteSpace(x))
                                        .Select(x => Convert.ToUInt32(x, 16))
                                        .ToArray();
                    }

                    mon.PhysicalMonitor.GetVCPRegister(0x60, out uint currentSource);

                    var newMonitor = new Monitor()
                    {
                        MonitorUniqueId = mon.UniqueId,
                        Model = model,
                        ValidSources = sources.Cast<int>().ToList()
                    };

                    cachedMonitorList.Add(newMonitor);
                });
            }

            return cachedMonitorList;
        }
    }
}
