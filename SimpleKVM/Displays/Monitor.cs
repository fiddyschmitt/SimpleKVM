using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleKVM.Displays
{
    public abstract class Monitor
    {
        public string MonitorUniqueId = "";

        [JsonIgnoreAttribute]
        public string Model = "";

        [JsonIgnoreAttribute]
        public int CurrentSource;

        [JsonIgnoreAttribute]
        public List<int> ValidSources = new List<int>();

        public abstract bool SetSource(int newSourceId);
    }
}
