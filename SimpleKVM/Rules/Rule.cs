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

        public override void StopMonitoring()
        {
            Status = EnumRuleStatus.Stopped;
            Trigger.StopMonitoring();
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
