using Avalonia;
using System;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace SimpleKVM
{
    static class Program
    {
#if WINDOWS
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        const int ATTACH_PARENT_PROCESS = -1;
#endif

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0)
            {
#if WINDOWS
                //WinExe apps have no console; borrow the parent shell's so diagnostics print
                AttachConsole(ATTACH_PARENT_PROCESS);
#endif
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
