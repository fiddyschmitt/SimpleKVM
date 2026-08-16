using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.mac
{
    [SupportedOSPlatform("macos")]
    public static class CoreFoundationRunLoop
    {
        const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport(CoreFoundation, EntryPoint = "CFRunLoopRun")]
        static extern void CFRunLoopRun();

        /// <summary>Runs the current thread's CFRunLoop; never returns until the loop is stopped.</summary>
        public static void RunForever()
        {
            CFRunLoopRun();
        }
    }
}
