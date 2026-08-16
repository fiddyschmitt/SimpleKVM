using Avalonia.Controls;
using SimpleKVM.Rules.Triggers;
using System.Collections.Generic;
using IAction = SimpleKVM.Rules.Actions.IAction;

namespace SimpleKVM.Ui
{
    public class ValidationResult(Control control, string errorMessage)
    {
        public Control Control { get; } = control;
        public string ErrorMessage { get; } = errorMessage;
    }

    public interface IValidate
    {
        List<ValidationResult> ValidateData();
    }

    public interface IValueChangedListener
    {
        void ValueChanged();
    }

    public interface ITriggerCreator
    {
        Trigger? GetTrigger();
    }

    public interface IActionCreator
    {
        List<IAction> GetAction();
    }
}
