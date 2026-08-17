using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Styling;
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

            //The rule list selects whole rows; hide the DataGrid's per-cell "current" outline and
            //focus border so a click highlights only the row, like the old ListView
            Styles.Add(new Style(x => x.OfType<DataGridCell>().Class(":current").Template().OfType<Rectangle>().Name("CurrencyVisual"))
            {
                Setters = { new Setter(Visual.IsVisibleProperty, false) }
            });
            Styles.Add(new Style(x => x.OfType<DataGridCell>().Class(":focus").Template().OfType<Grid>().Name("FocusVisual"))
            {
                Setters = { new Setter(Visual.IsVisibleProperty, false) }
            });
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();

                //Refreshes an existing run-at-startup registration to the current format
                //(e.g. adds the start-minimized argument to a shortcut made by an older version)
                try { Platform.PlatformServices.Current.Startup?.IsEnabled(); } catch { }

                //When launched at login the app should sit in the tray / menu bar without
                //showing its window. Not assigning MainWindow keeps the lifetime from showing
                //it; the tray icon and (on Windows) ShutdownMode keep the app alive.
                if (Program.StartMinimized)
                {
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                }
                else
                {
                    desktop.MainWindow = mainWindow;
                }

                trayIcon = new TrayIcon
                {
                    Icon = LoadIcon(),
                    ToolTipText = mainWindow.Title
                };

                trayIcon.Clicked += (s, e) => RestoreMainWindow(mainWindow);

                var openItem = new NativeMenuItem(OperatingSystem.IsMacOS() ? "Open Simple KVM" : "Open");
                openItem.Click += (s, e) => RestoreMainWindow(mainWindow);

                var exitItem = new NativeMenuItem(OperatingSystem.IsMacOS() ? "Quit Simple KVM" : "Exit");
                exitItem.Click += (s, e) => mainWindow.Quit();

                trayIcon.Menu = [openItem, new NativeMenuItemSeparator(), exitItem];

                TrayIcon.SetIcons(this, [trayIcon]);

                //Ensure the tray icon doesn't linger until hovered after exit
                desktop.Exit += (s, e) =>
                {
                    trayIcon.IsVisible = false;
                    trayIcon.Dispose();
                };

                //As a menu-bar agent the app has no Dock icon and the window can't be reached
                //once closed except through the menu-bar icon, so keep running in the background
                if (OperatingSystem.IsMacOS())
                {
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                }

                //Launching the app again while it's running (Finder, Spotlight, a Desktop
                //shortcut) arrives as a "reopen" activation; show the window in response, as
                //macOS users expect. Nothing else has to be done for the first launch.
                if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
                {
                    activatable.Activated += (s, e) =>
                    {
                        if (e.Kind == ActivationKind.Reopen)
                        {
                            RestoreMainWindow(mainWindow);
                        }
                    };
                }
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
