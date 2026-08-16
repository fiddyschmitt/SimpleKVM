using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace SimpleKVM.Displays.mac
{
    /// <summary>
    /// Pairs CoreGraphics displays with the DCPAVServiceProxy nodes that carry their DDC I2C.
    /// v1 heuristic: with a single external display and external AV services, pair them 1:1 in
    /// enumeration order. Multi-monitor EDID/location matching is a known follow-up.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class AVServiceMatcher
    {
        public static List<IntPtr> GetExternalAvServices()
        {
            var result = new List<IntPtr>();

            if (!IOKitNative.ResolveAvServiceSymbols()) return result;

            var matching = IOKitNative.IOServiceMatching("DCPAVServiceProxy");
            if (IOKitNative.IOServiceGetMatchingServices(0, matching, out uint iterator) != 0) return result;

            uint service;
            while ((service = IOKitNative.IOIteratorNext(iterator)) != 0)
            {
                try
                {
                    var location = IOKitNative.GetRegistryStringProperty(service, "Location");
                    if (location != "External") continue;

                    var avService = IOKitNative.AVServiceCreateWithService(service);
                    if (avService != IntPtr.Zero)
                    {
                        result.Add(avService);
                    }
                }
                finally
                {
                    IOKitNative.IOObjectRelease(service);
                }
            }
            IOKitNative.IOObjectRelease(iterator);

            return result;
        }
    }
}
