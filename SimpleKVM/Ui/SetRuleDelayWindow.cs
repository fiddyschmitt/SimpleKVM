using Avalonia;
using Avalonia.Controls;

namespace SimpleKVM.Ui
{
    public class SetRuleDelayWindow : Window
    {
        readonly NumericUpDown nudDelay;
        readonly TextBlock promptText;

        public int DelaySeconds
        {
            get => (int)(nudDelay.Value ?? 0);
            set => nudDelay.Value = value;
        }

        /// <summary>The explanatory text above the field; defaults to the per-rule wording.</summary>
        public string Prompt
        {
            get => promptText.Text ?? "";
            set => promptText.Text = value;
        }

        public SetRuleDelayWindow()
        {
            Title = "Set delay";
            Icon = App.LoadIcon();
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            nudDelay = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 3600,
                Increment = 1,
                FormatString = "0",
                Value = 0,
                MinWidth = 120
            };

            var btnOk = new Button { Content = "OK" };
            btnOk.Click += (s, e) => Close(true);

            var btnCancel = new Button { Content = "Cancel" };
            btnCancel.Click += (s, e) => Close(false);

            var buttonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            buttonRow.Children.Add(btnOk);
            buttonRow.Children.Add(btnCancel);

            var layout = new StackPanel
            {
                Margin = new Thickness(12),
                Spacing = 10
            };
            promptText = new TextBlock
            {
                Text = "Wait this many seconds after the trigger fires\nbefore running the actions:"
            };
            layout.Children.Add(promptText);
            layout.Children.Add(nudDelay);
            layout.Children.Add(buttonRow);

            Content = layout;
        }
    }
}
