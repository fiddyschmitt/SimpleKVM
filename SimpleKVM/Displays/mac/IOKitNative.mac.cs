using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SimpleKVM.Displays.mac
{
    /// <summary>
    /// Bindings for the public CoreFoundation/IOKit C APIs plus the private IOAVService
    /// I2C functions used for DDC/CI on Apple Silicon (the m1ddc/MonitorControl approach).
    /// The private symbols are resolved dynamically because their home framework is not
    /// documented; <see cref="ResolveAvServiceSymbols"/> reports where they were found.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class IOKitNative
    {
        const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";

        const uint kCFStringEncodingUTF8 = 0x08000100;

        #region CoreFoundation

        [DllImport(CoreFoundation)]
        public static extern void CFRelease(IntPtr cf);

        [DllImport(CoreFoundation)]
        static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string str, uint encoding);

        [DllImport(CoreFoundation)]
        static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, long bufferSize, uint encoding);

        [DllImport(CoreFoundation)]
        static extern IntPtr CFDataGetBytePtr(IntPtr theData);

        [DllImport(CoreFoundation)]
        static extern long CFDataGetLength(IntPtr theData);

        public static IntPtr CreateCFString(string str)
        {
            return CFStringCreateWithCString(IntPtr.Zero, str, kCFStringEncodingUTF8);
        }

        public static string? CFStringToString(IntPtr cfString)
        {
            if (cfString == IntPtr.Zero) return null;

            var buffer = new byte[1024];
            if (!CFStringGetCString(cfString, buffer, buffer.Length, kCFStringEncodingUTF8)) return null;

            int len = Array.IndexOf(buffer, (byte)0);
            return Encoding.UTF8.GetString(buffer, 0, len < 0 ? buffer.Length : len);
        }

        public static byte[]? CFDataToBytes(IntPtr cfData)
        {
            if (cfData == IntPtr.Zero) return null;

            var length = (int)CFDataGetLength(cfData);
            var bytes = new byte[length];
            Marshal.Copy(CFDataGetBytePtr(cfData), bytes, 0, length);
            return bytes;
        }

        #endregion

        #region IOKit registry

        [DllImport(IOKit)]
        public static extern IntPtr IOServiceMatching(string name);

        [DllImport(IOKit)]
        public static extern int IOServiceGetMatchingServices(uint mainPort, IntPtr matching, out uint iterator);

        [DllImport(IOKit)]
        public static extern uint IOIteratorNext(uint iterator);

        [DllImport(IOKit)]
        public static extern int IOObjectRelease(uint obj);

        [DllImport(IOKit)]
        public static extern IntPtr IORegistryEntryCreateCFProperty(uint entry, IntPtr key, IntPtr allocator, uint options);

        [DllImport(IOKit)]
        public static extern int IORegistryEntryGetRegistryEntryID(uint entry, out ulong entryId);

        public static string? GetRegistryStringProperty(uint entry, string propertyName)
        {
            var key = CreateCFString(propertyName);
            try
            {
                var value = IORegistryEntryCreateCFProperty(entry, key, IntPtr.Zero, 0);
                if (value == IntPtr.Zero) return null;

                try
                {
                    return CFStringToString(value);
                }
                finally
                {
                    CFRelease(value);
                }
            }
            finally
            {
                CFRelease(key);
            }
        }

        #endregion

        #region IOAVService (private)

        delegate IntPtr IOAVServiceCreateWithService_Delegate(IntPtr allocator, uint service);
        delegate int IOAVServiceCopyEDID_Delegate(IntPtr avService, out IntPtr cfData);
        delegate int IOAVServiceReadI2C_Delegate(IntPtr avService, uint chipAddress, uint offset, byte[] outputBuffer, uint outputBufferSize);
        delegate int IOAVServiceWriteI2C_Delegate(IntPtr avService, uint chipAddress, uint dataAddress, byte[] inputBuffer, uint inputBufferSize);

        static IOAVServiceCreateWithService_Delegate? _IOAVServiceCreateWithService;
        static IOAVServiceCopyEDID_Delegate? _IOAVServiceCopyEDID;
        static IOAVServiceReadI2C_Delegate? _IOAVServiceReadI2C;
        static IOAVServiceWriteI2C_Delegate? _IOAVServiceWriteI2C;

        public static string? AvServiceSymbolSource { get; private set; }

        static readonly string[] AvServiceLibraryCandidates =
        [
            "/System/Library/Frameworks/CoreDisplay.framework/CoreDisplay",
            "/System/Library/PrivateFrameworks/CoreDisplay.framework/CoreDisplay",
            IOKit,
        ];

        /// <summary>
        /// Locates the private IOAVService* symbols. Returns true when all were found;
        /// <see cref="AvServiceSymbolSource"/> names the library that exported them.
        /// </summary>
        public static bool ResolveAvServiceSymbols()
        {
            if (_IOAVServiceCreateWithService != null) return true;

            foreach (var candidate in AvServiceLibraryCandidates)
            {
                if (!NativeLibrary.TryLoad(candidate, out var lib)) continue;

                if (NativeLibrary.TryGetExport(lib, "IOAVServiceCreateWithService", out var create) &&
                    NativeLibrary.TryGetExport(lib, "IOAVServiceReadI2C", out var read) &&
                    NativeLibrary.TryGetExport(lib, "IOAVServiceWriteI2C", out var write))
                {
                    _IOAVServiceCreateWithService = Marshal.GetDelegateForFunctionPointer<IOAVServiceCreateWithService_Delegate>(create);
                    _IOAVServiceReadI2C = Marshal.GetDelegateForFunctionPointer<IOAVServiceReadI2C_Delegate>(read);
                    _IOAVServiceWriteI2C = Marshal.GetDelegateForFunctionPointer<IOAVServiceWriteI2C_Delegate>(write);

                    if (NativeLibrary.TryGetExport(lib, "IOAVServiceCopyEDID", out var copyEdid))
                    {
                        _IOAVServiceCopyEDID = Marshal.GetDelegateForFunctionPointer<IOAVServiceCopyEDID_Delegate>(copyEdid);
                    }

                    AvServiceSymbolSource = candidate;
                    return true;
                }
            }

            return false;
        }

        public static IntPtr AVServiceCreateWithService(uint service)
        {
            return _IOAVServiceCreateWithService!(IntPtr.Zero, service);
        }

        public static byte[]? AVServiceCopyEDID(IntPtr avService)
        {
            if (_IOAVServiceCopyEDID == null) return null;

            int status = _IOAVServiceCopyEDID(avService, out IntPtr cfData);
            if (status != 0 || cfData == IntPtr.Zero) return null;

            try
            {
                return CFDataToBytes(cfData);
            }
            finally
            {
                CFRelease(cfData);
            }
        }

        public static int AVServiceReadI2C(IntPtr avService, uint chipAddress, uint offset, byte[] buffer)
        {
            return _IOAVServiceReadI2C!(avService, chipAddress, offset, buffer, (uint)buffer.Length);
        }

        public static int AVServiceWriteI2C(IntPtr avService, uint chipAddress, uint dataAddress, byte[] buffer)
        {
            return _IOAVServiceWriteI2C!(avService, chipAddress, dataAddress, buffer, (uint)buffer.Length);
        }

        #endregion
    }
}
