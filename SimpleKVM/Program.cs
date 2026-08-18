using Avalonia;
using System;
using System.Linq;

namespace SimpleKVM
{
    static class Program
    {
        /// <summary>
        /// Passed by the run-at-startup registration so the app starts in the background
        /// (tray / menu bar only) instead of showing its window.
        /// </summary>
        public const string StartMinimizedArg = "--minimized";

        public static bool StartMinimized { get; private set; }

        //STA: the Windows startup-shortcut code talks to the shell through COM (IShellLink)
        [STAThread]
        static int Main(string[] args)
        {
            StartMinimized = args.Contains(StartMinimizedArg);
            args = args.Where(arg => arg != StartMinimizedArg).ToArray();

            if (args.Length > 0)
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                {
                    Platform.win.WindowsConsole.AttachForCli();
                }
                return Cli.DiagnosticCli.Run(args);
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder
                    .Configure<Ui.App>()
                    .UsePlatformDetect()
                    .With(new Win32PlatformOptions
                    {
                        //A settings window doesn't need GPU acceleration; software rendering lets
                        //the ANGLE (OpenGL ES) native library be left out of the build entirely
                        RenderingMode = [Win32RenderingMode.Software]
                    })
                    .With(new MacOSPlatformOptions
                    {
                        //Menu-bar agent: no Dock icon or Cmd+Tab entry. Avalonia otherwise forces
                        //the regular activation policy and overrides LSUIElement in the plist.
                        ShowInDock = false,
                        DisableDefaultApplicationMenuItems = true
                    })
                    .LogToTrace();
        }
    }
}
