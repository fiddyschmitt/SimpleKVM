using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SimpleKVM.Rules.Triggers
{
    public abstract class Trigger
    {
        public abstract string GetTriggerAsFriendlyString();
        public abstract void StartMonitoring();
        public abstract void StopMonitoring();

        public event EventHandler Triggered = delegate { };
        protected virtual void RaiseTriggered()
        {
            Triggered?.Invoke(this, new EventArgs());
        }
    }
}
