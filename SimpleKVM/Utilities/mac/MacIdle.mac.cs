using SimpleKVM.Platform;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimpleKVM.Utilities.mac
{
    [SupportedOSPlatform("macos")]
    public class MacIdle : IIdleProvider
    {
        const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        [DllImport(CoreGraphics)]
        static extern double CGEventSourceSecondsSinceLastEventType(int stateID, uint eventType);

        const int kCGEventSourceStateCombinedSessionState = 0;
        const uint kCGAnyInputEventType = 0xFFFFFFFF;

        public TimeSpan GetIdleTimeSpan()
        {
            var seconds = CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateCombinedSessionState, kCGAnyInputEventType);
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
