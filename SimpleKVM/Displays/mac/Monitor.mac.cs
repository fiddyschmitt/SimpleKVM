using Newtonsoft.Json;
using SimpleKVM.Configuration;
using SimpleKVM.Displays.I2C;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;

namespace SimpleKVM.Displays.mac
{
    [SupportedOSPlatform("macos")]
    public class Monitor : Displays.Monitor
    {
        [JsonIgnore]
        internal DdcTransport? Transport;

        public Monitor(string uniqueId, string model, List<(int SourceId, string SourceName)> validSources) : base(uniqueId, model, validSources)
        {
        }

        public override int GetCurrentSource()
        {
            if (UseLgAltMode) return -1;

            if (Transport != null && Transport.GetVcp(0x60, out uint currentSource))
            {
                return (int)currentSource;
            }

            return 0;
        }

        public override bool SetSource(int newSourceId)
        {
            //-1 means "Leave unchanged"; 0 is what a failed read returns. Neither is a valid source.
            if (newSourceId <= 0) return false;
            if (Transport == null) return false;

            if (UseLgAltMode)
            {
                bool ok = Transport.SetVcp(LgInputSources.SourceAddress, LgInputSources.VcpCode, (uint)newSourceId);
                if (ok)
                {
                    Thread.Sleep(30);
                    RaiseSourceSetByApp(MonitorUniqueId, newSourceId);
                }
                return ok;
            }

            Transport.GetVcp(0x60, out uint currentSource);

            bool shouldSwitch = AppSettingsManager.Current.ForceInputChange || newSourceId != currentSource;
            if (!shouldSwitch) return false;

            bool result = Transport.SetVcp(0x51, 0x60, (uint)newSourceId);
            if (result)
            {
                RaiseSourceSetByApp(MonitorUniqueId, newSourceId);
            }

            return result;
        }
    }
}
