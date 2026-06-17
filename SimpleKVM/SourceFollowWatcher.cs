using SimpleKVM.Displays;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleKVM
{
    /// <summary>
    /// Watches the input source of digital (DDC/CI-readable) monitors. When one is changed by
    /// something other than this app - e.g. another PC pulling the shared monitors toward itself -
    /// it runs the active rule(s) that target that monitor+input, which pushes the remaining
    /// (analog VGA/DVI) monitors the same direction so they aren't left behind.
    /// </summary>
    public class SourceFollowWatcher
    {
        static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        //After the app writes a source, ignore that monitor briefly so neither the write nor the
        //DDC apply lag is mistaken for an external change (which could misfire toward the previous PC).
        static readonly TimeSpan SettleWindow = TimeSpan.FromSeconds(3);

        readonly Func<IEnumerable<Rule>> getRules;

        readonly object sync = new();
        readonly Dictionary<string, int> lastSource = [];
        readonly Dictionary<string, DateTime> suppressUntil = [];

        Task? loop;
        CancellationTokenSource? cancellation;

        public SourceFollowWatcher(Func<IEnumerable<Rule>> getRules)
        {
            this.getRules = getRules;
        }

        public void Start()
        {
            Stop();   //guarantee a single loop and a fresh baseline

            lock (sync)
            {
                lastSource.Clear();
                suppressUntil.Clear();
            }

            Displays.Monitor.SourceSetByApp += OnSourceSetByApp;

            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            loop = Task.Factory.StartNew(() => Run(token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            Displays.Monitor.SourceSetByApp -= OnSourceSetByApp;

            //Cancel without waiting: the loop only touches DDC/CI, never the UI thread, so there is
            //nothing to deadlock on, and we don't want to block the caller (the UI) for a poll cycle.
            cancellation?.Cancel();
            cancellation = null;
            loop = null;
        }

        void OnSourceSetByApp(string monitorUniqueId, int sourceId)
        {
            lock (sync)
            {
                lastSource[monitorUniqueId] = sourceId;
                suppressUntil[monitorUniqueId] = DateTime.Now + SettleWindow;
            }
        }

        void Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Poll();
                }
                catch
                {
                    //Never let a transient DDC/CI failure kill the watcher.
                }

                token.WaitHandle.WaitOne(PollInterval);
            }
        }

        void Poll()
        {
            var current = ReadReadableSources();

            var externalChanges = new List<(string MonitorId, int NewSource)>();

            lock (sync)
            {
                var now = DateTime.Now;

                foreach (var (id, source) in current)
                {
                    if (suppressUntil.TryGetValue(id, out var until) && now < until)
                    {
                        //We (or a settling write) caused this; absorb it without following.
                        lastSource[id] = source;
                        continue;
                    }

                    if (lastSource.TryGetValue(id, out var previous))
                    {
                        if (previous != source)
                        {
                            externalChanges.Add((id, source));
                            lastSource[id] = source;
                        }
                    }
                    else
                    {
                        //First time we've seen this monitor; just establish the baseline.
                        lastSource[id] = source;
                    }
                }
            }

            if (externalChanges.Count == 0) return;

            //Run every active rule that targets one of the changed monitors with its new input.
            //Distinct() so a comprehensive rule matched by two monitors at once is only run once.
            var rulesToRun = getRules()
                                .Where(rule => rule.Status == EnumRuleStatus.Running)
                                .Where(rule => rule.Actions
                                                    .OfType<SetMonitorSourceAction>()
                                                    .Any(action => externalChanges.Any(change =>
                                                        action.Monitor.MonitorUniqueId == change.MonitorId &&
                                                        action.SetMonitorSourceIdTo == change.NewSource)))
                                .Distinct()
                                .ToList();

            //Run() re-asserts the changed monitor (a no-op, so no fight with the other PC), pushes
            //the analog monitors, and tracks RunCount/LastRun. Its own writes announce themselves
            //via SourceSetByApp and get suppressed, so the follow can't feed back on itself.
            rulesToRun.ForEach(rule => rule.Run());
        }

        IEnumerable<(string MonitorId, int Source)> ReadReadableSources()
        {
            //LG alt-mode monitors can't have their input read over DDC/CI, so they can only be
            //pushed, never watched. Exclude them from the watch set.
            var lgMonitorIds = DisplaySystem
                                .GetMonitors()
                                .Where(mon => mon.UseLgAltMode)
                                .Select(mon => mon.MonitorUniqueId)
                                .ToHashSet();

            return DisplaySystem
                        .GetCurrentSources()
                        .Where(kvp => kvp.Value > 0 && !lgMonitorIds.Contains(kvp.Key))
                        .Select(kvp => (kvp.Key, kvp.Value));
        }
    }
}
