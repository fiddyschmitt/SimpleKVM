using SimpleKVM.Rules.Actions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using IAction = SimpleKVM.Rules.Actions.IAction;

namespace SimpleKVM.GUI.Actions
{
    public interface IActionCreator
    {
        public List<IAction> GetAction();

        public List<ValidationResult> ValidateData();
    }
}
