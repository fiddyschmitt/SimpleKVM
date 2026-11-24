using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SimpleKVM.Input;
using SimpleKVM.Platform;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Ui
{
    public class ChooseHotkeyWindow : Window
    {
        readonly TextBox hotkeyBox;
        readonly TextBlock availabilityText;
        readonly Button btnFree;
        readonly Button btnOk;
        readonly Rule? ruleBeingEdited;
        readonly IDesktopShortcuts? desktopShortcuts = PlatformServices.Current.DesktopShortcuts;
        readonly IKeyCapture? keyCapture;

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

            availabilityText = new TextBlock
            {
                Text = "",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360
            };

            //A combination the desktop keeps for one of its own shortcuts can be taken from it
            btnFree = new Button { Content = "Free it", IsVisible = false };
            ToolTip.SetTip(btnFree, "Takes this key combination away from the desktop's shortcut, which keeps any other keys it has. "
                                    + "The desktop's keyboard settings can give it back.");
            btnFree.Click += (s, e) => FreeFromDesktop();

            //How hotkeys behave on this desktop, or what stands in their way, when the platform has something to say
            var platformNote = new TextBlock
            {
                Text = PlatformServices.Current.Hotkeys.Note,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
                IsVisible = PlatformServices.Current.Hotkeys.Note != null
            };

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
            layout.Children.Add(btnFree);
            layout.Children.Add(platformNote);
            layout.Children.Add(btnOk);

            Content = layout;

            //Where the desktop acts on its own shortcuts before any app sees the keys (Linux:
            //GNOME), the keyboard is taken while this window is in front, so that those
            //combinations arrive here too. Set up before the window is shown, so that its first
            //activation already takes it.
            if (desktopShortcuts != null && TryGetPlatformHandle() is { } handle)
            {
                keyCapture = desktopShortcuts.CreateCapture(handle.Handle, handle.HandleDescriptor,
                                                            combination => Dispatcher.UIThread.Post(() => Record(combination)));
            }
            Activated += (s, e) => keyCapture?.Start();
            Deactivated += (s, e) => keyCapture?.Stop();
            Closed += (s, e) => keyCapture?.Dispose();

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

        /// <summary>
        /// A combination the platform took from the keyboard, recorded as the chooser's own key
        /// events would have recorded it: a key without a modifier is no hotkey, and the key is
        /// named as Avalonia names it.
        /// </summary>
        void Record(string combination)
        {
            HotkeyGesture gesture;
            try
            {
                gesture = HotkeyGesture.Parse(combination);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (!(gesture.Win || gesture.Ctrl || gesture.Alt || gesture.Shift)) return;

            var parts = new List<string>();
            if (gesture.Win) parts.Add("Win");
            if (gesture.Ctrl) parts.Add("Ctrl");
            if (gesture.Alt) parts.Add("Alt");
            if (gesture.Shift) parts.Add("Shift");
            parts.Add(Enum.TryParse<Key>(gesture.KeyName, ignoreCase: true, out var key) ? key.ToString() : gesture.KeyName);

            hotkeyBox.Text = string.Join("+", parts);

            CheckIfHotkeyIsAvailable();
        }

        void CheckIfHotkeyIsAvailable()
        {
            var hotkeyString = hotkeyBox.Text;
            btnFree.IsVisible = false;

            if (string.IsNullOrEmpty(hotkeyString))
            {
                availabilityText.Text = "";
                btnOk.IsEnabled = false;
                return;
            }

            //check if another rule already uses this hotkey
            bool hotkeyInUseByAnotherRule = RuleStore
                                                .Rules
                                                .Where(rule => rule != ruleBeingEdited)
                                                .Select(rule => rule.Trigger)
                                                .OfType<HotkeyTrigger>()
                                                .Any(trigger => trigger.HotkeyAsString.Equals(hotkeyString));

            //check if the desktop acts on it itself, before any app could see it (Linux: GNOME)
            IReadOnlyList<string> desktopUses = hotkeyInUseByAnotherRule ? [] : desktopShortcuts?.Uses(hotkeyString) ?? [];
            if (desktopUses.Count > 0)
            {
                availabilityText.Text = $"Your desktop uses this for {string.Join(" and ", desktopUses)}.";
                availabilityText.Foreground = Brushes.Red;
                btnFree.IsVisible = true;
                btnOk.IsEnabled = false;
                return;
            }

            //check if the hotkey is in use by another application
            bool hotkeyAvailable = !hotkeyInUseByAnotherRule && HotkeySystem.IsAvailable(hotkeyString);

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

        /// <summary>
        /// Takes the combination away from the desktop's shortcuts that have it. The desktop lets
        /// go of the keys a moment after its settings change, so the check is made again then: on
        /// an X11 session the keys can't be grabbed until it has.
        /// </summary>
        void FreeFromDesktop()
        {
            var hotkeyString = hotkeyBox.Text;
            if (desktopShortcuts == null || string.IsNullOrEmpty(hotkeyString)) return;

            try
            {
                desktopShortcuts.Free(hotkeyString);
            }
            catch (Exception ex)
            {
                availabilityText.Text = $"Couldn't free it: {ex.Message}";
                availabilityText.Foreground = Brushes.Red;
                return;
            }

            CheckIfHotkeyIsAvailable();
            DispatcherTimer.RunOnce(() =>
            {
                if (hotkeyBox.Text == hotkeyString) CheckIfHotkeyIsAvailable();
            }, TimeSpan.FromMilliseconds(500));
        }
    }
}
