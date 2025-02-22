using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleKVM.Displays
{
    public abstract class Monitor
    {
        public string MonitorUniqueId;

        [JsonIgnore]
        public string Model;

        public abstract int GetCurrentSource();

        [JsonIgnore]
        public readonly List<(int SourceId, string SourceName)> ValidSources;

        public Monitor(string uniqueId, string model, List<(int SourceId, string SourceName)> validSources)
        {
            MonitorUniqueId = uniqueId;
            Model = model;
            ValidSources = validSources;
        }

        public abstract bool SetSource(int newSourceId);
    }
}
