using SimpleKVM.Rules.Triggers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SimpleKVM.GUI.Triggers
{
    public partial class UcConfigureNoLongerIdleTrigger : UserControl, IValidate, ITriggerCreator
    {
        public UcConfigureNoLongerIdleTrigger()
        {
            InitializeComponent();
        }

        public Trigger? GetTrigger()
        {
            var result = new NoLongerIdle();
            return result;
        }

        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();
            return result;
        }
    }
}
