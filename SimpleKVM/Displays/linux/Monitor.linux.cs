using SimpleKVM.Displays.Ddc;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace SimpleKVM.Displays.linux
{
    /// <summary>The concrete type rules.json names on Linux; the behaviour lives in <see cref="DdcMonitor"/>.</summary>
    [SupportedOSPlatform("linux")]
    public class Monitor : DdcMonitor
    {
        public Monitor(string uniqueId, string model, List<(int SourceId, string SourceName)> validSources) : base(uniqueId, model, validSources)
        {
        }
    }
}
