using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SimpleKVM.Configuration;
using SimpleKVM.Platform;
using System;

namespace SimpleKVM.Ui
{
    public class SettingsWindow : Window
    {
        readonly CheckBox chkRunAtStartup;
        readonly CheckBox chkForceInputChange;
        readonly CheckBox chkFollowSourceChanges;
        readonly TextBlock errorText;

        public SettingsWindow()
        {
            Title = "Settings";
            Icon = App.LoadIcon();
            SizeToContent = SizeToContent.WidthAndHeight;
            MaxWidth = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            var startup = PlatformServices.Current.Startup;

            chkRunAtStartup = new CheckBox
            {
                Content = "Run at startup",
                IsVisible = startup != null,
                IsChecked = startup?.IsEnabled() ?? false
            };

            chkForceInputChange = new CheckBox
            {
                Content = "Always set monitor source, even if it appears correct",
                IsChecked = AppSettingsManager.Current.ForceInputChange
            };

            chkFollowSourceChanges = new CheckBox
            {
                Content = "Follow external source changes",
                IsChecked = AppSettingsManager.Current.FollowSourceChanges
            };

            var followHint = new TextBlock
            {
                Text = "When another PC pulls some monitors to itself, move the ones left behind to match.",
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(28, 0, 0, 0)
            };

            errorText = new TextBlock
            {
                Foreground = Brushes.Red,
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap
            };

            var btnOk = new Button { Content = "OK" };
            btnOk.Click += (s, e) => Save(startup);

            var btnCancel = new Button { Content = "Cancel" };
            btnCancel.Click += (s, e) => Close(false);

            var buttonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };
            buttonRow.Children.Add(btnOk);
            buttonRow.Children.Add(btnCancel);

            var layout = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 10
            };
            layout.Children.Add(chkRunAtStartup);
            layout.Children.Add(chkForceInputChange);
            layout.Children.Add(chkFollowSourceChanges);
            layout.Children.Add(followHint);
            layout.Children.Add(errorText);
            layout.Children.Add(buttonRow);

            Content = layout;
        }

        void Save(IStartupManager? startup)
        {
            if (startup != null)
            {
                try
                {
                    startup.SetEnabled(chkRunAtStartup.IsChecked == true);
                }
                catch (Exception ex)
                {
                    errorText.Text = $"Failed to update the startup setting: {ex.Message}";
                    errorText.IsVisible = true;
                    return;
                }
            }

            AppSettingsManager.Current.ForceInputChange = chkForceInputChange.IsChecked == true;
            AppSettingsManager.Current.FollowSourceChanges = chkFollowSourceChanges.IsChecked == true;
            AppSettingsManager.Save();

            Close(true);
        }
    }
}
