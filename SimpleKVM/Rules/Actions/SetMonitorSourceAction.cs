using SimpleKVM.Displays;
using System.Linq;

namespace SimpleKVM.Rules.Actions
{
    public class SetMonitorSourceAction : IAction
    {
        public Monitor Monitor;
        public int SetMonitorSourceIdTo;

        /// <summary>
        /// Extra wait before this monitor is switched, on top of the rule's own delay. Lets a
        /// slow-to-wake monitor be switched later than the others without holding them up.
        /// Older rules.json files have no value here and deserialize to 0.
        /// </summary>
        public int DelaySeconds { get; set; }

        public SetMonitorSourceAction(Monitor monitor, int sourceId)
        {
            Monitor = monitor;
            SetMonitorSourceIdTo = sourceId;
        }

        public static bool IsValid()
        {
            //check that the monitor exists
            return true;
        }

        public bool Run()
        {
            if (SetMonitorSourceIdTo == -1) return false;

            if (DelaySeconds > 0)
                System.Threading.Thread.Sleep(DelaySeconds * 1000);

            //The Monitor deserialized from rules.json lacks the [JsonIgnore] state (UseLgAltMode, I2C transport),
            //so resolve the live monitor by id and only fall back to the deserialized one if it's not found
            var monitor = DisplaySystem
                            .GetMonitors()
                            .FirstOrDefault(mon => mon.MonitorUniqueId == Monitor.MonitorUniqueId)
                            ?? Monitor;

            var result = monitor.SetSource(SetMonitorSourceIdTo);
            return result;
        }
    }
}
