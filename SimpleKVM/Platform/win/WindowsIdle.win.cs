using System;
using System.Runtime.InteropServices;

namespace SimpleKVM.Platform.win
{
    public class WindowsIdle : IIdleProvider
    {
        public TimeSpan GetIdleTimeSpan()
        {
            var lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            GetLastInputInfo(ref lastInput);

            var idleMs = (uint)Environment.TickCount - lastInput.dwTime;
            return TimeSpan.FromMilliseconds(idleMs);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    }
}
