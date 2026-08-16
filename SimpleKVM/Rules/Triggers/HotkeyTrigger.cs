using SimpleKVM.Input;
using System;

namespace SimpleKVM.Rules.Triggers
{
    public class HotkeyTrigger : Trigger
    {
        public HotkeyTrigger(string hotkeyAsString)
        {
            HotkeyAsString = hotkeyAsString;
        }

        public string HotkeyAsString { get; }

        public override string GetTriggerAsFriendlyString()
        {
            var result = $"whenever {HotkeyAsString} is pressed";
            return result;
        }

        IDisposable? registration;

        public override void StartMonitoring()
        {
            registration = HotkeySystem.Register(HotkeyAsString, RaiseTriggered);
        }

        public override void StopMonitoring()
        {
            registration?.Dispose();
            registration = null;
        }
    }
}
