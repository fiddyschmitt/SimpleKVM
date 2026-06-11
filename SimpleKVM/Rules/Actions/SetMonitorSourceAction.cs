using SimpleKVM.Displays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Action = SimpleKVM.Rules.Actions.IAction;

namespace SimpleKVM.Rules.Actions
{
    public class SetMonitorSourceAction : IAction
    {
        public Monitor Monitor;
        public int SetMonitorSourceIdTo;

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
