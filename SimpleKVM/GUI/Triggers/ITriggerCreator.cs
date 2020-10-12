using SimpleKVM.Rules.Triggers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SimpleKVM.GUI.Triggers
{
    public interface ITriggerCreator
    {
        public Trigger? GetTrigger();

        public List<ValidationResult> ValidateData();
    }
}
