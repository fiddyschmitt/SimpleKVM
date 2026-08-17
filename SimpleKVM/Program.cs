using Avalonia;
using System;
using System.Linq;
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
        [DllImport("kernel32.dll")]
        static extern IntPtr GetStdHandle(int nStdHandle);
        const int ATTACH_PARENT_PROCESS = -1;
        const int STD_OUTPUT_HANDLE = -11;
#endif

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        /// <summary>
        /// Passed by the run-at-startup registration so the app starts in the background
        /// (tray / menu bar only) instead of showing its window.
        /// </summary>
        public const string StartMinimizedArg = "--minimized";

        public static bool StartMinimized { get; private set; }

        [STAThread]
        static int Main(string[] args)
        {
            StartMinimized = args.Contains(StartMinimizedArg);
            args = args.Where(arg => arg != StartMinimizedArg).ToArray();

            if (args.Length > 0)
            {
#if WINDOWS
                //WinExe apps have no console. When stdout is redirected to a file or pipe the
                //inherited handle already works, so leave it alone; otherwise borrow the parent
                //shell's console so diagnostics print interactively.
                if (GetStdHandle(STD_OUTPUT_HANDLE) == IntPtr.Zero)
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                    Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
                }
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
