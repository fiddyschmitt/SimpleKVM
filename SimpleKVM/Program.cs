using System;
#if WINDOWS
using System.Runtime.InteropServices;
using System.Windows.Forms;
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

#if WINDOWS
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            return 0;
#else
            Console.WriteLine("SimpleKVM does not have a GUI on this platform yet. Run with --help style arguments; see --list-monitors.");
            return Cli.DiagnosticCli.Run(["--help"]);
#endif
        }
    }
}
