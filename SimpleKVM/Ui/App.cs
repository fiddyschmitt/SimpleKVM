using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using System;

namespace SimpleKVM.Ui
{
    public class App : Application
    {
        public const string IconUri = "avares://SimpleKVM/iconfinder_Communication_pc_computer_sharing_6588768_white_bg.ico";

        TrayIcon? trayIcon;

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Styles.Add(new StyleInclude(new Uri("avares://SimpleKVM"))
            {
                Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
            });
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;

                trayIcon = new TrayIcon
                {
                    Icon = LoadIcon(),
                    ToolTipText = mainWindow.Title
                };

                trayIcon.Clicked += (s, e) => RestoreMainWindow(mainWindow);

                var openItem = new NativeMenuItem("Open");
                openItem.Click += (s, e) => RestoreMainWindow(mainWindow);

                var exitItem = new NativeMenuItem("Exit");
                exitItem.Click += (s, e) => mainWindow.Close();

                trayIcon.Menu = [openItem, new NativeMenuItemSeparator(), exitItem];

                TrayIcon.SetIcons(this, [trayIcon]);

                //Ensure the tray icon doesn't linger until hovered after exit
                desktop.Exit += (s, e) =>
                {
                    trayIcon.IsVisible = false;
                    trayIcon.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        static void RestoreMainWindow(Window mainWindow)
        {
            mainWindow.WindowState = WindowState.Normal;    //deminiaturize before showing
            mainWindow.Show();
            mainWindow.Activate();
        }

        public static WindowIcon LoadIcon()
        {
            return new WindowIcon(AssetLoader.Open(new Uri(IconUri)));
        }
    }
}
