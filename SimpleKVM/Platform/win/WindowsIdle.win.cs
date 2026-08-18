using System;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.win
{
    [SupportedOSPlatform("windows6.1")]
    public class WindowsIdle : IIdleProvider
    {
        public unsafe TimeSpan GetIdleTimeSpan()
        {
            var lastInput = new LASTINPUTINFO { cbSize = (uint)sizeof(LASTINPUTINFO) };
            PInvoke.GetLastInputInfo(ref lastInput);

            var idleMs = (uint)Environment.TickCount - lastInput.dwTime;
            return TimeSpan.FromMilliseconds(idleMs);
        }
    }
}
