using SimpleKVM.GUI.Actions;
using SimpleKVM.GUI.Triggers;
using SimpleKVM.Rules;
using SimpleKVM.USB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using SimpleKVM.Rules.Triggers;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Displays;
using System.Threading.Tasks;
using System.Threading;

namespace SimpleKVM.GUI.Rules
{
    public partial class ModifyRule : Form, IValidate, IRuleCreator, IValueChangedListener
    {
        ITriggerCreator? triggerCreator;
        IActionCreator? actionCreator;

        public Rule? RuleToEdit { get; protected set; }

        public ModifyRule()
        {
            InitializeComponent();
        }

        public ModifyRule(USBSystem usbSystem, string title, Rule ruleToEdit) : this()
        {
            EnumTriggerType? triggerType = null;
            if (ruleToEdit.Trigger is USBTrigger) triggerType = EnumTriggerType.Usb;
            if (ruleToEdit.Trigger is HotkeyTrigger) triggerType = EnumTriggerType.Hotkey;
            if (ruleToEdit.Trigger is NoLongerIdle) triggerType = EnumTriggerType.NoLongerIdle;

            EnumActionType? actionType = null;
            if (ruleToEdit.Actions.All(action => action is SetMonitorSourceAction)) actionType = EnumActionType.SelectMonitorSource;

            if (triggerType == null || actionType == null) return;

            Init(usbSystem, title, triggerType.Value, actionType.Value, ruleToEdit);
        }

        public ModifyRule(USBSystem usbSystem, string title, EnumTriggerType triggerType, EnumActionType actionType) : this()
        {
            Init(usbSystem, title, triggerType, actionType, null);
        }

        void Init(USBSystem usbSystem, string title, EnumTriggerType triggerType, EnumActionType actionType, Rule? ruleToEdit = null)
        {
            Text = title;

            if (ruleToEdit != null)
            {
                txtRuleName.Text = ruleToEdit.Name;
            }

            UserControl? triggerCreatorUc = triggerType switch
            {
                EnumTriggerType.Usb => new UcConfigureUsbTrigger(usbSystem, this, ruleToEdit),
                EnumTriggerType.Hotkey => new UcConfigureHotkeyTrigger(this, ruleToEdit),
                EnumTriggerType.NoLongerIdle => new UcConfigureNoLongerIdleTrigger(),
                _ => null
            };

            if (triggerCreatorUc != null)
            {
                triggerCreatorUc.Left = label1.Left;
                triggerCreatorUc.Top = txtRuleName.Bottom + 8;

                Controls.Add(triggerCreatorUc);

                triggerCreator = (ITriggerCreator)triggerCreatorUc;

                triggerCreatorUc.TabIndex = txtRuleName.TabIndex;
            }

            var actionCreatorUc = actionType switch
            {
                EnumActionType.SelectMonitorSource => new UcSelectMonitorsAndSources(null, ruleToEdit),
                _ => null
            };

            if (actionCreatorUc != null)
            {
                actionCreatorUc.Left = 8;

                if (triggerCreatorUc != null)
                {
                    actionCreatorUc.Top = triggerCreatorUc.Bottom;
                    actionCreatorUc.TabIndex = triggerCreatorUc.TabIndex;

                    Controls.Add(actionCreatorUc);
                }

                actionCreator = actionCreatorUc;
            }


            Width = Controls.Cast<Control>().Max(c => c.Width + 64);
            Height = Controls.Cast<Control>().Max(c => c.Bottom) + 96;

            if (triggerCreatorUc != null) triggerCreatorUc.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            if (actionCreatorUc != null) actionCreatorUc.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;

            RuleToEdit = ruleToEdit;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var validationResults = ValidateData();

            if (validationResults.Count == 0)
            {
                DialogResult = DialogResult.OK;
            }
        }

        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();

            if (triggerCreator is IValidate triggerUc)
            {
                result.AddRange(triggerUc.ValidateData());
            }

            if (actionCreator is IValidate actionUc)
            {
                result.AddRange(actionUc.ValidateData());
            }

            errorProvider1.Clear();

            result
                .ForEach(validationResult =>
                {
                    errorProvider1.SetError(validationResult.Control, validationResult.ErrorMessage);
                    errorProvider1.SetIconAlignment(validationResult.Control, validationResult.Alignment);
                    errorProvider1.SetIconPadding(validationResult.Control, validationResult.IconPadding);
                });

            return result;
        }

        public Rule? GetRule()
        {
            if (RuleToEdit == null)
            {
                Rule? result = null;

                var trigger = triggerCreator?.GetTrigger();
                var actions = actionCreator?.GetAction();

                if (trigger != null && actions != null)
                {
                    result = new Rule(txtRuleName.Text, trigger, actions);
                }

                return result;
            }
            else
            {
                RuleToEdit.Name = txtRuleName.Text;
                RuleToEdit.Trigger = triggerCreator?.GetTrigger() ?? RuleToEdit.Trigger;
                RuleToEdit.Actions = actionCreator?.GetAction() ?? RuleToEdit.Actions;

                return RuleToEdit;
            }
        }

        public void ValueChanged()
        {
            //We have been told by a sub control that they've changed. Let's validate again
            ValidateData();
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            var rule = GetRule();

            if (rule != null)
            {
                Task.Factory.StartNew(() =>
                {
                    var originalSources = DisplaySystem
                                            .GetMonitors(true)
                                            .Select(monitor => new
                                            {
                                                Monitor = monitor,
                                                OriginalSource = monitor.GetCurrentSource()
                                            })
                                            .ToList();

                    rule.Run();
                    Thread.Sleep(TimeSpan.FromSeconds(10));

                    originalSources
                        .ForEach(originalSources =>
                        {
                            originalSources.Monitor.SetSource(originalSources.OriginalSource);
                        });
                });
            }
        }
    }
}
