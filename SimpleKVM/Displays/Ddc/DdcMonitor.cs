using Newtonsoft.Json;
using SimpleKVM.Configuration;
using SimpleKVM.Displays.I2C;
using System.Collections.Generic;
using System.Threading;

namespace SimpleKVM.Displays.Ddc
{
    /// <summary>
    /// A monitor driven purely over a DDC/CI transport: what the macOS and Linux backends have
    /// in common. Each keeps its own thin subclass because rules.json names the concrete type
    /// and the serialization binder maps those names between platforms.
    /// </summary>
    public abstract class DdcMonitor : Monitor
    {
        [JsonIgnore]
        internal IDdcTransport? Transport;

        protected DdcMonitor(string uniqueId, string model, List<(int SourceId, string SourceName)> validSources) : base(uniqueId, model, validSources)
        {
        }

        public override int GetCurrentSource()
        {
            if (UseLgAltMode) return -1;     //the sidechannel input can't be read back

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
