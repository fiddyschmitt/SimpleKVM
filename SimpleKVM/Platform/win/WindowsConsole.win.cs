using System;
using System.IO;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.System.Console;

namespace SimpleKVM.Platform.win
{
    [SupportedOSPlatform("windows6.1")]
    public static class WindowsConsole
    {
        /// <summary>
        /// A WinExe has no console. When stdout is redirected to a file or pipe the inherited
        /// handle already works, so leave it alone; otherwise borrow the parent shell's console
        /// so the diagnostic CLI prints interactively.
        /// </summary>
        public static void AttachForCli()
        {
            if (!PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE).IsNull) return;

            PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }
}
