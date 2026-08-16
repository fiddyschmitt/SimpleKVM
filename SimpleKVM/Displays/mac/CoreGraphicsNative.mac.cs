using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimpleKVM.Displays.mac
{
    [SupportedOSPlatform("macos")]
    public static class CoreGraphicsNative
    {
        const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        [StructLayout(LayoutKind.Sequential)]
        public struct CGRect
        {
            public double X;
            public double Y;
            public double Width;
            public double Height;
        }

        [DllImport(CoreGraphics)]
        public static extern int CGGetActiveDisplayList(uint maxDisplays, uint[] activeDisplays, out uint displayCount);

        [DllImport(CoreGraphics)]
        public static extern CGRect CGDisplayBounds(uint display);

        [DllImport(CoreGraphics)]
        public static extern bool CGDisplayIsBuiltin(uint display);

        public static uint[] GetActiveDisplays()
        {
            var displays = new uint[16];
            if (CGGetActiveDisplayList((uint)displays.Length, displays, out uint count) != 0) return [];
            return displays[..(int)count];
        }
    }
}
