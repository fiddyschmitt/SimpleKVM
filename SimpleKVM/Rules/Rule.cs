using Newtonsoft.Json;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Rules.Triggers;
using System;
using System.Collections.Generic;
using System.Text;
using IAction = SimpleKVM.Rules.Actions.IAction;

namespace SimpleKVM.Rules
{
    public class Rule : Trigger
    {
        public Trigger Trigger;

        public List<IAction> Actions = new List<IAction>();
        public int RunCount { get; set; }
        public DateTime? LastRun { get; set; }
        public EnumRuleStatus Status { get; set; } = EnumRuleStatus.Stopped;
        public string Name { get; set; } = "";

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
            } catch(Exception ex)
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

            bool wasRun = false;
            Actions
                .ForEach(action =>
                {
                    if (action.Run())
                    {
                        wasRun = true;
                    }
                    
                });

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
