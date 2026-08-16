using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Rules.Triggers;
using SimpleKVM.Ui.Controls;
using SimpleKVM.USB;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleKVM.Ui
{
    public class ModifyRuleWindow : Window, IValidate, IValueChangedListener
    {
        readonly TextBox txtRuleName;
        readonly TextBlock errorText;
        readonly ITriggerCreator? triggerCreator;
        readonly IActionCreator? actionCreator;

        public Rule? RuleToEdit { get; }

        public ModifyRuleWindow(USBSystem usbSystem, string title, Rule ruleToEdit)
            : this(usbSystem, title, TriggerTypeOf(ruleToEdit), ActionTypeOf(ruleToEdit), ruleToEdit)
        {
        }

        static EnumTriggerType TriggerTypeOf(Rule rule)
        {
            return rule.Trigger switch
            {
                USBTrigger => EnumTriggerType.Usb,
                HotkeyTrigger => EnumTriggerType.Hotkey,
                _ => EnumTriggerType.NoLongerIdle
            };
        }

        static EnumActionType ActionTypeOf(Rule rule)
        {
            return EnumActionType.SelectMonitorSource;
        }

        public ModifyRuleWindow(USBSystem usbSystem, string title, EnumTriggerType triggerType, EnumActionType actionType, Rule? ruleToEdit)
        {
            Title = title;
            Icon = App.LoadIcon();
            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            RuleToEdit = ruleToEdit;

            txtRuleName = new TextBox
            {
                Text = ruleToEdit?.Name ?? "Switch to this computer",
                MinWidth = 320
            };

            var nameRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };
            nameRow.Children.Add(new TextBlock { Text = "Rule Name", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            nameRow.Children.Add(txtRuleName);

            Control? triggerView = triggerType switch
            {
                EnumTriggerType.Usb => new UsbTriggerView(usbSystem, this, ruleToEdit),
                EnumTriggerType.Hotkey => new HotkeyTriggerView(this, ruleToEdit),
                EnumTriggerType.NoLongerIdle => new NoLongerIdleView(),
                _ => null
            };
            triggerCreator = triggerView as ITriggerCreator;

            Control? actionView = actionType switch
            {
                EnumActionType.SelectMonitorSource => new MonitorLayoutView(ruleToEdit),
                _ => null
            };
            actionCreator = actionView as IActionCreator;

            errorText = new TextBlock
            {
                Foreground = Brushes.Red,
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap
            };

            var btnSave = new Button { Content = "Save" };
            btnSave.Click += (s, e) =>
            {
                if (ValidateData().Count == 0)
                {
                    Close(true);
                }
            };

            var btnTest = new Button { Content = "Test" };
            btnTest.Click += (s, e) => RunTest();

            var buttonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };
            buttonRow.Children.Add(btnSave);
            buttonRow.Children.Add(btnTest);
            buttonRow.Children.Add(new TextBlock
            {
                Text = "(will revert back after 10 seconds)",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = Brushes.Gray
            });

            var layout = new StackPanel
            {
                Margin = new Thickness(12),
                Spacing = 12
            };
            layout.Children.Add(nameRow);
            if (triggerView != null) layout.Children.Add(triggerView);
            if (actionView != null) layout.Children.Add(actionView);
            layout.Children.Add(errorText);
            layout.Children.Add(buttonRow);

            Content = new ScrollViewer { Content = layout };
        }

        public System.Collections.Generic.List<ValidationResult> ValidateData()
        {
            var result = new System.Collections.Generic.List<ValidationResult>();

            if (triggerCreator is IValidate triggerValidate)
            {
                result.AddRange(triggerValidate.ValidateData());
            }

            if (actionCreator is IValidate actionValidate)
            {
                result.AddRange(actionValidate.ValidateData());
            }

            errorText.Text = string.Join(Environment.NewLine, result.Select(r => r.ErrorMessage));
            errorText.IsVisible = result.Count > 0;

            return result;
        }

        public void ValueChanged()
        {
            //We have been told by a sub control that they've changed. Let's validate again
            ValidateData();
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
                    result = new Rule(txtRuleName.Text ?? "", trigger, actions);
                }

                return result;
            }
            else
            {
                RuleToEdit.Name = txtRuleName.Text ?? "";
                RuleToEdit.Trigger = triggerCreator?.GetTrigger() ?? RuleToEdit.Trigger;
                RuleToEdit.Actions = actionCreator?.GetAction() ?? RuleToEdit.Actions;

                return RuleToEdit;
            }
        }

        void RunTest()
        {
            var actions = actionCreator?.GetAction();
            if (actions == null || actions.Count == 0) return;

            Task.Factory.StartNew(() =>
            {
                var originalSources = Displays.DisplaySystem
                                        .GetMonitors()
                                        .Select(monitor => new
                                        {
                                            Monitor = monitor,
                                            OriginalSource = monitor.GetCurrentSource()
                                        })
                                        .Where(originalSource => originalSource.OriginalSource > 0)    //the current source can't always be determined (e.g. LG alt mode); don't restore those monitors
                                        .ToList();

                actions.ForEach(action => action.Run());
                Thread.Sleep(TimeSpan.FromSeconds(10));

                originalSources
                    .ForEach(originalSource =>
                    {
                        originalSource.Monitor.SetSource(originalSource.OriginalSource);
                    });
            }, TaskCreationOptions.LongRunning);
        }
    }
}
