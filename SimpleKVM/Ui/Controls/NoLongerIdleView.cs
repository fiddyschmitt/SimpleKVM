using Avalonia.Controls;
using SimpleKVM.Rules.Triggers;
using System.Collections.Generic;

namespace SimpleKVM.Ui.Controls
{
    public class NoLongerIdleView : UserControl, IValidate, ITriggerCreator
    {
        public NoLongerIdleView()
        {
            Content = new TextBlock { Text = "Whenever the user is no longer idle, set the monitor sources to:" };
        }

        public List<ValidationResult> ValidateData()
        {
            return [];
        }

        public Trigger? GetTrigger()
        {
            return new NoLongerIdle();
        }
    }
}
