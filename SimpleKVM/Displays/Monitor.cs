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

        [JsonIgnore]
        public bool UseLgAltMode { get; set; }

        /// <summary>
        /// Raised when the app itself successfully writes a monitor's input source
        /// (MonitorUniqueId, newSourceId). SourceFollowWatcher uses this to tell its own writes
        /// apart from input changes made externally by another PC.
        /// </summary>
        public static event Action<string, int>? SourceSetByApp;

        protected static void RaiseSourceSetByApp(string monitorUniqueId, int sourceId)
        {
            SourceSetByApp?.Invoke(monitorUniqueId, sourceId);
        }

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
