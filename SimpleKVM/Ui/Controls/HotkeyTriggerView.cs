using Avalonia.Controls;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Ui.Controls
{
    public class HotkeyTriggerView : UserControl, IValidate, ITriggerCreator
    {
        readonly HyperlinkButton hotkeyLink;
        readonly IValueChangedListener? valueChangedListener;
        readonly Rule? ruleToEdit;

        string? hotkeyStringChosenByUser;

        public HotkeyTriggerView(IValueChangedListener? valueChangedListener, Rule? ruleToEdit)
        {
            this.valueChangedListener = valueChangedListener;
            this.ruleToEdit = ruleToEdit;

            if (ruleToEdit?.Trigger is HotkeyTrigger trigger)
            {
                hotkeyStringChosenByUser = trigger.HotkeyAsString;
            }

            hotkeyLink = new HyperlinkButton
            {
                Padding = new Avalonia.Thickness(0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            hotkeyLink.Click += async (s, e) => await ShowHotkeyChooser();

            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4
            };
            row.Children.Add(new TextBlock { Text = "Whenever", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            row.Children.Add(hotkeyLink);
            row.Children.Add(new TextBlock { Text = "is pressed, set the monitor sources to:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });

            Content = row;

            UpdateLinkText();
        }

        void UpdateLinkText()
        {
            hotkeyLink.Content = hotkeyStringChosenByUser ?? "this hotkey";
        }

        async System.Threading.Tasks.Task ShowHotkeyChooser()
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var chooser = new ChooseHotkeyWindow(hotkeyStringChosenByUser, ruleToEdit);

            //The hotkey rules are paused while the chooser records keys: one on the same keys
            //would fire, or take them before the chooser sees them, and the availability check
            //would find this app's own registrations
            bool ok;
            using (Rule.Pause(RuleStore.Rules.Where(rule => rule.Trigger is HotkeyTrigger)))
            {
                ok = await chooser.ShowDialog<bool>(owner);
            }

            if (ok)
            {
                hotkeyStringChosenByUser = chooser.HotkeyStringChosenByUser;
                UpdateLinkText();

                valueChangedListener?.ValueChanged();
            }
        }

        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();

            if (hotkeyStringChosenByUser == null) result.Add(new ValidationResult(hotkeyLink, "Please choose a hotkey"));

            return result;
        }

        public Trigger? GetTrigger()
        {
            if (hotkeyStringChosenByUser == null) return null;

            return new HotkeyTrigger(hotkeyStringChosenByUser);
        }
    }
}
