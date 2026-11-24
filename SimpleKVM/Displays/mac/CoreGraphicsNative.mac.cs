using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimpleKVM.Displays.mac
{
    [SupportedOSPlatform("macos")]
    public static class CoreGraphicsNative
    {
        const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        /// <summary>IOKit's kDisplayModeNativeFlag: the mode is the panel's native resolution (also set on its HiDPI twin, which has the same pixels).</summary>
        const uint DisplayModeNativeFlag = 0x02000000;

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

        [DllImport(CoreGraphics)]
        static extern IntPtr CGDisplayCopyDisplayMode(uint display);

        [DllImport(CoreGraphics)]
        static extern IntPtr CGDisplayCopyAllDisplayModes(uint display, IntPtr options);

        [DllImport(CoreGraphics)]
        static extern nuint CGDisplayModeGetWidth(IntPtr mode);

        [DllImport(CoreGraphics)]
        static extern nuint CGDisplayModeGetHeight(IntPtr mode);

        [DllImport(CoreGraphics)]
        static extern nuint CGDisplayModeGetPixelWidth(IntPtr mode);

        [DllImport(CoreGraphics)]
        static extern nuint CGDisplayModeGetPixelHeight(IntPtr mode);

        [DllImport(CoreGraphics)]
        static extern uint CGDisplayModeGetIOFlags(IntPtr mode);

        [DllImport(CoreGraphics)]
        static extern void CGDisplayModeRelease(IntPtr mode);

        [DllImport(CoreFoundation)]
        static extern nint CFArrayGetCount(IntPtr array);

        [DllImport(CoreFoundation)]
        static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

        [DllImport(CoreFoundation)]
        static extern IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint count, IntPtr keyCallBacks, IntPtr valueCallBacks);

        [DllImport(CoreFoundation)]
        static extern void CFRelease(IntPtr cf);

        public static uint[] GetActiveDisplays()
        {
            var displays = new uint[16];
            if (CGGetActiveDisplayList((uint)displays.Length, displays, out uint count) != 0) return [];
            return displays[..(int)count];
        }

        /// <summary>
        /// The display's native resolution in pixels: what its panel has, whichever mode macOS runs
        /// it in. A scaled ("looks like") mode reports fewer points, and even its own pixel size
        /// follows the setting (a 4K monitor that looks like 2560x1440 renders at 5120x2880), so the
        /// native mode is looked up in the full mode list. Failing that: the largest mode whose
        /// points are pixels, then the current mode's pixels. (0, 0) when nothing answers.
        /// </summary>
        public static (int Width, int Height) NativePixelSize(uint display)
        {
            (int Width, int Height) native = (0, 0), largestUnscaled = (0, 0);

            var options = DuplicateLowResolutionModesOption();
            var modes = CGDisplayCopyAllDisplayModes(display, options);
            if (options != IntPtr.Zero) CFRelease(options);

            if (modes != IntPtr.Zero)
            {
                try
                {
                    for (nint i = 0; i < CFArrayGetCount(modes); i++)
                    {
                        var mode = CFArrayGetValueAtIndex(modes, i);
                        int pixelWidth = (int)CGDisplayModeGetPixelWidth(mode), pixelHeight = (int)CGDisplayModeGetPixelHeight(mode);

                        if ((CGDisplayModeGetIOFlags(mode) & DisplayModeNativeFlag) != 0 && pixelWidth * pixelHeight > native.Width * native.Height)
                        {
                            native = (pixelWidth, pixelHeight);
                        }

                        bool unscaled = pixelWidth == (int)CGDisplayModeGetWidth(mode) && pixelHeight == (int)CGDisplayModeGetHeight(mode);
                        if (unscaled && pixelWidth * pixelHeight > largestUnscaled.Width * largestUnscaled.Height)
                        {
                            largestUnscaled = (pixelWidth, pixelHeight);
                        }
                    }
                }
                finally
                {
                    CFRelease(modes);
                }
            }

            if (native.Width > 0) return native;
            if (largestUnscaled.Width > 0) return largestUnscaled;

            var current = CGDisplayCopyDisplayMode(display);
            if (current == IntPtr.Zero) return (0, 0);
            try
            {
                return ((int)CGDisplayModeGetPixelWidth(current), (int)CGDisplayModeGetPixelHeight(current));
            }
            finally
            {
                CGDisplayModeRelease(current);
            }
        }

        /// <summary>
        /// { kCGDisplayShowDuplicateLowResolutionModes: true }, which adds the HiDPI modes to the
        /// list: on a Retina screen the native mode may only be listed among them. IntPtr.Zero
        /// (the default list) when the constants can't be found.
        /// </summary>
        static IntPtr DuplicateLowResolutionModesOption()
        {
            try
            {
                var coreGraphics = NativeLibrary.Load(CoreGraphics);
                var coreFoundation = NativeLibrary.Load(CoreFoundation);

                //Exported variables: the strings and booleans are pointers stored at the export; the callback structs are the exports themselves
                var key = Marshal.ReadIntPtr(NativeLibrary.GetExport(coreGraphics, "kCGDisplayShowDuplicateLowResolutionModes"));
                var yes = Marshal.ReadIntPtr(NativeLibrary.GetExport(coreFoundation, "kCFBooleanTrue"));
                var keyCallBacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryKeyCallBacks");
                var valueCallBacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryValueCallBacks");

                return CFDictionaryCreate(IntPtr.Zero, [key], [yes], 1, keyCallBacks, valueCallBacks);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }
}
