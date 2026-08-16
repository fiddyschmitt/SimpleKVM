using System;
#if WINDOWS
using System.Windows.Forms;
#endif

namespace SimpleKVM
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
#if WINDOWS
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
#else
            Console.WriteLine("SimpleKVM does not have a GUI on this platform yet.");
#endif
        }
    }
}
