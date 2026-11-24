using Newtonsoft.Json;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Rules.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IAction = SimpleKVM.Rules.Actions.IAction;

namespace SimpleKVM.Rules
{
    public class Rule : Trigger
    {
        public Trigger Trigger;

        public List<IAction> Actions = [];
        public int RunCount { get; set; }
        public DateTime? LastRun { get; set; }
        public EnumRuleStatus Status { get; set; } = EnumRuleStatus.Stopped;
        public string Name { get; set; } = "";
        public int DelaySeconds { get; set; }

        public Rule(string name, Trigger trigger, List<IAction> actions)
        {
            Name = name;
            Trigger = trigger;
            Actions = actions;
        }

        public override void StartMonitoring()
        {
            pausedBy.Clear();   //started outright: no pause holds the rule any more
            Start();
        }

        public override void StopMonitoring()
        {
            pausedBy.Clear();   //stopped outright (deleted, disabled): the end of a pause mustn't start it again
            Stop();
        }

        void Start()
        {
            if (Status == EnumRuleStatus.Running) return;

            try
            {
                Trigger.Triggered -= OnTriggered;   //a tricky manoeuvre to ensure we don't register for the event multiple times
                Trigger.Triggered += OnTriggered;
                Trigger.StartMonitoring();

                Status = EnumRuleStatus.Running;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Status = EnumRuleStatus.Error;
            }
        }

        void Stop()
        {
            Status = EnumRuleStatus.Stopped;
            Trigger.StopMonitoring();
        }

        //The pauses holding the rule stopped. Pausing and editing happen on the UI thread only.
        readonly HashSet<object> pausedBy = [];

        /// <summary>True while a pause holds the rule stopped.</summary>
        [JsonIgnore]
        public bool IsPaused => pausedBy.Count > 0;

        /// <summary>
        /// Stops the rules that are running (or failed to start) until the result is disposed.
        /// For the steps that need them out of the way: the hotkey chooser recording keys, the
        /// USB chooser watching for a device, the rule editor's test. Pauses can overlap; a rule
        /// starts again when the last one holding it ends. A rule stopped, started, disabled or
        /// deleted in the meantime is left as it is.
        /// </summary>
        public static IDisposable Pause(IEnumerable<Rule> rules)
        {
            var pause = new RulePause();

            foreach (var rule in rules)
            {
                if (rule.pausedBy.Count == 0)
                {
                    if (rule.Status != EnumRuleStatus.Running && rule.Status != EnumRuleStatus.Error) continue;
                    rule.Stop();
                }

                rule.pausedBy.Add(pause);
                pause.Held.Add(rule);
            }

            return pause;
        }

        sealed class RulePause : IDisposable
        {
            public List<Rule> Held { get; } = [];

            public void Dispose()
            {
                foreach (var rule in Held)
                {
                    if (rule.pausedBy.Remove(this) && rule.pausedBy.Count == 0)
                    {
                        rule.Start();
                    }
                }

                Held.Clear();
            }
        }

        /// <summary>
        /// Applies an edit made in the rule editor, which replaces the trigger. The rule keeps
        /// running on its old trigger while it's edited, so that one is stopped before the change
        /// and the new one started after it. A disabled rule stays disabled, and a paused one
        /// starts on its new trigger when its pause ends.
        /// </summary>
        public void Edit(Action change)
        {
            bool restart = Status != EnumRuleStatus.Disabled && !IsPaused;
            if (restart) StopMonitoring();

            try
            {
                change();
            }
            finally
            {
                if (restart) StartMonitoring();
            }
        }

        private void OnTriggered(object? sender, EventArgs e)
        {
            if (Status != EnumRuleStatus.Running) return;

            RunInBackground();
        }

        int runInFlight;   //1 while a background run is executing

        /// <summary>
        /// Runs the rule on the thread pool. Triggers fire on threads that must not block (the
        /// hotkey message pump, the WMI and IOKit callback threads, the macOS main thread) and a
        /// run sleeps for the rule's delay and then talks DDC/CI, so it never happens on them. A
        /// trigger that fires again while a run is still in flight is dropped rather than queued.
        /// </summary>
        public void RunInBackground()
        {
            if (Interlocked.CompareExchange(ref runInFlight, 1, 0) != 0) return;

            Task.Run(() =>
            {
                try
                {
                    Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Rule '{Name}' failed: {ex}");
                }
                finally
                {
                    Interlocked.Exchange(ref runInFlight, 0);
                }
            });
        }

        public void Run()
        {
            if (DelaySeconds > 0)
                System.Threading.Thread.Sleep(DelaySeconds * 1000);

            //Actions run concurrently so that a per-action delay (see SetMonitorSourceAction.DelaySeconds)
            //is an offset from this moment rather than being added onto the actions before it: a monitor
            //set to wait 5 s switches 5 s after the rule fires, whatever the other monitors are doing.
            var results = System.Threading.Tasks.Task.WhenAll(
                Actions.Select(action => System.Threading.Tasks.Task.Run(action.Run)))
                .GetAwaiter().GetResult();

            bool wasRun = results.Any(ran => ran);

            if (wasRun)
            {
                RunCount++;
                LastRun = DateTime.Now;
            }

            RaiseTriggered();
        }

        public void Enable()
        {
            StartMonitoring();
        }

        public void Disable()
        {
            StopMonitoring();
            Status = EnumRuleStatus.Disabled;
        }

        public override string GetTriggerAsFriendlyString()
        {
            string result = "None";
            if (Trigger != null)
            {
                result = Trigger.GetTriggerAsFriendlyString();
            }

            return result;
        }

        public string GetLastRunAsFriendlyString()
        {
            var timeSinceLastRun = DateTime.Now - LastRun;

            var result = timeSinceLastRun?.ToPrettyFormat();
            if (result == null)
            {
                result = "Never";
            }
            else
            {
                result += " ago";
            }

            return result;
        }
    }

    public enum EnumTriggerType
    {
        Usb,
        Hotkey,
        NoLongerIdle
    }

    public enum EnumActionType
    {
        SelectMonitorSource
    }

    public enum EnumRuleStatus
    {
        Running,
        Stopped,
        Error,
        Disabled
    }
}
