using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;
using System.Linq;

namespace SimpleKVM.Ui
{
    public class ChooseHotkeyWindow : Window
    {
        readonly TextBox hotkeyBox;
        readonly TextBlock availabilityText;
        readonly Button btnOk;
        readonly Rule? ruleBeingEdited;

        public string? HotkeyStringChosenByUser { get; private set; }

        public ChooseHotkeyWindow(string? currentHotkey, Rule? ruleBeingEdited)
        {
            this.ruleBeingEdited = ruleBeingEdited;

            Title = "Choose hotkey";
            Icon = App.LoadIcon();
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            hotkeyBox = new TextBox
            {
                Text = currentHotkey,
                PlaceholderText = "Press a key combination...",
                MinWidth = 260,
                IsReadOnly = true
            };
            hotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            availabilityText = new TextBlock { Text = "" };

            btnOk = new Button { Content = "OK", IsEnabled = false };
            btnOk.Click += (s, e) =>
            {
                HotkeyStringChosenByUser = hotkeyBox.Text;
                Close(true);
            };

            var layout = new StackPanel
            {
                Margin = new Thickness(12),
                Spacing = 8
            };
            layout.Children.Add(new TextBlock { Text = "Press the key combination to use:" });
            layout.Children.Add(hotkeyBox);
            layout.Children.Add(availabilityText);
            layout.Children.Add(btnOk);

            Content = layout;

            Opened += (s, e) =>
            {
                hotkeyBox.Focus();
                CheckIfHotkeyIsAvailable();
            };
        }

        void HotkeyBox_KeyDown(object? sender, KeyEventArgs e)
        {
            e.Handled = true;

            //Ignore presses of the modifier keys themselves
            switch (e.Key)
            {
                case Key.LeftCtrl or Key.RightCtrl:
                case Key.LeftAlt or Key.RightAlt:
                case Key.LeftShift or Key.RightShift:
                case Key.LWin or Key.RWin:
                    return;
            }

            if (e.KeyModifiers == KeyModifiers.None) return;

            var parts = new System.Collections.Generic.List<string>();
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            parts.Add(e.Key.ToString());    //Avalonia's Key names match the vocabulary rules.json stores (D1, NumPad1, OemMinus, ...)

            hotkeyBox.Text = string.Join("+", parts);

            CheckIfHotkeyIsAvailable();
        }

        void CheckIfHotkeyIsAvailable()
        {
            var hotkeyString = hotkeyBox.Text;

            if (string.IsNullOrEmpty(hotkeyString))
            {
                availabilityText.Text = "";
                btnOk.IsEnabled = false;
                return;
            }

            //check if the hotkey is in use by another application
            bool hotkeyAvailable = Input.HotkeySystem.IsAvailable(hotkeyString);

            //check if another rule already uses this hotkey
            bool hotkeyInUseByAnotherRule = RuleStore
                                                .Rules
                                                .Where(rule => rule != ruleBeingEdited)
                                                .Select(rule => rule.Trigger)
                                                .OfType<HotkeyTrigger>()
                                                .Any(trigger => trigger.HotkeyAsString.Equals(hotkeyString));

            if (hotkeyInUseByAnotherRule)
            {
                hotkeyAvailable = false;
            }

            if (hotkeyAvailable)
            {
                availabilityText.Text = "Available";
                availabilityText.Foreground = Brushes.Green;
                btnOk.IsEnabled = true;
            }
            else
            {
                availabilityText.Text = "Unavailable";
                availabilityText.Foreground = Brushes.Red;
                btnOk.IsEnabled = false;
            }
        }
    }
}
