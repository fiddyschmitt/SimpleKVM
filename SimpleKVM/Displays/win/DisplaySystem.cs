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

namespace SimpleKVM.Displays.win
{
    public static class DisplaySystem
    {
        static readonly Regex modelRegex = new Regex(@"model\((.*?)\)");
        static readonly Regex sourcesRegex = new Regex(@"(?<=\s)60\((.*?)\)");

        static List<Monitor>? cachedMonitorList;
        public static IList<Monitor> GetMonitors()
        {
            if (cachedMonitorList == null)
            {

                cachedMonitorList = new List<Monitor>();

                int monitorId = 0;
                MonitorController.GetDevices(physicalMonitors =>
                {
                    physicalMonitors
                                .ForEach(physicalMonitor =>
                                {
                                    var caps = physicalMonitor.GetVCPCapabilities();
                                    if (caps != null)
                                    {
                                        var model = modelRegex.Match(caps).Groups[1].Value;
                                        var sources = sourcesRegex.Match(caps).Groups[1].Value.Split(' ').Select(x => Convert.ToUInt32(x, 16)).ToArray();
                                        physicalMonitor.GetVCPRegister(0x60, out uint currentSource);

                                        var newMonitor = new Monitor()
                                        {
                                            MonitorUniqueId = $"{++monitorId}",
                                            Model = model,
                                            ValidSources = sources.Cast<int>().ToList()
                                        };

                                        cachedMonitorList.Add(newMonitor);
                                    }
                                });
                });
            }

            return cachedMonitorList;
        }
    }
}
