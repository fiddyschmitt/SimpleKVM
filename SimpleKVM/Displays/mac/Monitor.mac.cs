using SimpleKVM.Displays.Ddc;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace SimpleKVM.Displays.mac
{
    /// <summary>The concrete type rules.json names on macOS; the behaviour lives in <see cref="DdcMonitor"/>.</summary>
    [SupportedOSPlatform("macos")]
    public class Monitor : DdcMonitor
    {
        public Monitor(string uniqueId, string model, List<(int SourceId, string SourceName)> validSources) : base(uniqueId, model, validSources)
        {
        }
    }
}
