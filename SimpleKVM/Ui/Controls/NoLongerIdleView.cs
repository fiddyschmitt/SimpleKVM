using Avalonia.Controls;
using Avalonia.Media;
using SimpleKVM.Platform;
using SimpleKVM.Rules.Triggers;
using SimpleKVM.Utilities;
using System.Collections.Generic;

namespace SimpleKVM.Ui.Controls
{
    public class NoLongerIdleView : UserControl, IValidate, ITriggerCreator
    {
        public NoLongerIdleView()
        {
            var layout = new StackPanel { Spacing = 4 };
            layout.Children.Add(new TextBlock { Text = "Whenever the user is no longer idle, set the monitor sources to:" });

            //Whether idle time can be read here at all (Linux: needs /dev/input access on a
            //desktop that doesn't report it); the platform knows after one reading
            IdleUtility.GetIdleTimeSpan();
            if (PlatformServices.Current.Idle.StatusMessage is string status)
            {
                layout.Children.Add(new TextBlock
                {
                    Text = status,
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 640
                });
            }

            Content = layout;
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
