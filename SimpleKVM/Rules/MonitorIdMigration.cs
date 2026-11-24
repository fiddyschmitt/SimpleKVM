using SimpleKVM.Rules.Actions;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Rules
{
    /// <summary>
    /// Moves rules saved under a monitor id an earlier version used over to the id the same
    /// monitor has now. Monitor ids are made from where a monitor sits and how big it is; when the
    /// way that is measured changes (macOS before 2.5.0 measured in points, which follow the
    /// display's scaling), a rule must not lose its monitor because of the upgrade. The platform
    /// supplies the earlier and current ids of the monitors connected at the time, so this runs
    /// at every start and catches a monitor whenever it is next connected.
    /// </summary>
    public static class MonitorIdMigration
    {
        /// <summary>Points every action that targets a monitor by an earlier id at its current id. Returns each change made.</summary>
        public static List<(string Earlier, string Current)> Apply(IEnumerable<Rule> rules, IReadOnlyDictionary<string, string> earlierToCurrent)
        {
            var changes = new List<(string Earlier, string Current)>();
            if (earlierToCurrent.Count == 0) return changes;

            foreach (var action in rules.SelectMany(rule => rule.Actions).OfType<SetMonitorSourceAction>())
            {
                var monitor = action.Monitor;
                if (monitor?.MonitorUniqueId is string id && earlierToCurrent.TryGetValue(id, out var current))
                {
                    monitor.MonitorUniqueId = current;
                    changes.Add((id, current));
                }
            }

            return changes;
        }
    }
}
