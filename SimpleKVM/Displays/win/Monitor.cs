using DDCKVMService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SimpleKVM.Displays.win
{
    public class Monitor : Displays.Monitor
    {
        public override int GetCurrentSource()
        {
            uint currentSource = 0;
            MonitorController.EnumMonitors(physicalMonitor =>
            {
                if (physicalMonitor.UniqueId == MonitorUniqueId)
                {
                    physicalMonitor.PhysicalMonitor.GetVCPRegister(0x60, out currentSource);
                }
            });

            return (int)currentSource;
        }

        //public static string ControlMyMonitorExe => Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", @"ext\win\controlmymonitor\ControlMyMonitor.exe");

        public override bool SetSource(int newSourceId)
        {
            /*
            var monitorListCommand = new ProcessStartInfo
            {
                FileName = ControlMyMonitorExe,
                Arguments = $"/SetValueIfNeeded \"{ MonitorDeviceName }\" 60 {inputId}"
            };

            monitorListCommand.StartAndReadStdout();
            */

            bool result = false;
            MonitorController.EnumMonitors(physicalMonitor =>
            {
                if (physicalMonitor.UniqueId == MonitorUniqueId)
                {
                    physicalMonitor.PhysicalMonitor.GetVCPRegister(0x60, out uint currentSource);

                    if (newSourceId != currentSource)
                    {
                        physicalMonitor.PhysicalMonitor.SetVCPRegister(0x60, (uint)newSourceId);
                        result = true;
                    }
                }
            });

            return result;
        }
    }
}
