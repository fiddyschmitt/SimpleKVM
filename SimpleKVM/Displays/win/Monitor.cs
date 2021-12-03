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
            var monitorToSet = int.Parse(MonitorUniqueId) - 1;
            MonitorController.GetDevices(physicalMonitors =>
            {
                var physicalMonitor = physicalMonitors[monitorToSet];
                physicalMonitor.GetVCPRegister(0x60, out currentSource);
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

            var monitorToSet = int.Parse(MonitorUniqueId) - 1;

            bool result = false;
            MonitorController.GetDevices(physicalMonitors =>
            {
                var physicalMonitor = physicalMonitors[monitorToSet];
                physicalMonitor.GetVCPRegister(0x60, out uint currentSource);

                if (newSourceId != currentSource)
                {
                    physicalMonitor.SetVCPRegister(0x60, (uint)newSourceId);
                    result = true;
                }
            });

            return result;
        }
    }
}
