using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleKVM.Displays
{
    public abstract class Monitor(string uniqueId, string model, List<int> validSources)
    {
        public string MonitorUniqueId = uniqueId;

        [JsonIgnore]
        public string Model = model;

        public abstract int GetCurrentSource();

        [JsonIgnore]
        public readonly List<int> ValidSources = validSources;

        public abstract bool SetSource(int newSourceId);
    }
}
