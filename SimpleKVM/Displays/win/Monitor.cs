using DDCKVMService;
using Newtonsoft.Json;
using SimpleKVM.Configuration;
using SimpleKVM.Displays.win.I2C;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SimpleKVM.Displays.win
{
    public class Monitor : Displays.Monitor
    {
        [JsonIgnore]
        internal II2CTransport? I2CTransport;

        [JsonIgnore]
        internal object? I2CDisplayHandle;

        public Monitor(string uniqueId, string model, List<(int SourceId, string SourceName)> validSources) : base(uniqueId, model, validSources)
        {
        }

        public override int GetCurrentSource()
        {
            if (UseLgAltMode)
                return -1;

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

        public override bool SetSource(int newSourceId)
        {
            if (UseLgAltMode && I2CTransport != null && I2CDisplayHandle != null)
            {
                bool ok = I2CTransport.SetVcp(I2CDisplayHandle, LgInputSources.I2CAddress, LgInputSources.VcpCode, (uint)newSourceId);
                if (ok) Thread.Sleep(30);
                return ok;
            }

            bool result = false;
            MonitorController.EnumMonitors(physicalMonitor =>
            {
                if (physicalMonitor.UniqueId == MonitorUniqueId)
                {
                    physicalMonitor.PhysicalMonitor.GetVCPRegister(0x60, out uint currentSource);

                    bool shouldSwitch = AppSettingsManager.Current.ForceInputChange || newSourceId != currentSource;

                    if (shouldSwitch)
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
