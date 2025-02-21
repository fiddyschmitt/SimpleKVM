using SimpleKVM;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DDCKVMService
{
    public static class MonitorController
    {
        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
            MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("Dxva2.dll")]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            out uint pdwNumberOfPhysicalMonitors
        );

        [DllImport("Dxva2.dll")]
        private static extern bool DestroyPhysicalMonitors(
            uint dwPhysicalMonitorArraySize,
            PHYSICAL_MONITOR[] pPhysicalMonitorArray
        );

        [DllImport("Dxva2.dll")]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray
        );

        [DllImport("Dxva2.dll")]
        private static extern bool SetVCPFeature(
            IntPtr hMonitor,
            byte bVCPCode,
            uint dwNewValue
        );

        [DllImport("Dxva2.dll")]
        static extern bool GetCapabilitiesStringLength(
            IntPtr hMonitor,
            ref uint pdwCapabilitiesStringLengthInCharacters
        );

        [DllImport("Dxva2.dll")]
        static extern bool CapabilitiesRequestAndCapabilitiesReply(
            IntPtr hMonitor,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder pszASCIICapabilitiesString,
            uint dwCapabilitiesStringLengthInCharacters
        );

        [DllImport("Dxva2.dll")]
        static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hMonitor,
            byte bVCPCode,
            IntPtr pvct,
            out uint pdwCurrentValue,
            out uint pdwMaximumValue
        );

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szPhysicalMonitorDescription;

            public readonly bool SetVCPRegister(byte register, uint value)
            {
                var result = SetVCPFeature(hPhysicalMonitor, register, value);
                return result;
            }

            public readonly bool GetVCPRegister(byte register, out uint value)
            {
                var supported = GetVCPFeatureAndVCPFeatureReply(hPhysicalMonitor, register, IntPtr.Zero, out value, out _);
                return supported;
            }

            public readonly string? GetVCPCapabilities()
            {
                uint length = 0;
                var supported = GetCapabilitiesStringLength(hPhysicalMonitor, ref length);
                if (!supported)
                {
                    return null;
                }

                var retval = new StringBuilder((int)length);
                supported = CapabilitiesRequestAndCapabilitiesReply(hPhysicalMonitor, retval, length);
                if (!supported)
                {
                    return null;
                }

                return retval.ToString();
            }
        }

        public static void EnumMonitors(Action<(IntPtr hMonitor, PHYSICAL_MONITOR PhysicalMonitor, string UniqueId)> action)
        {
            // Iterate monitors and retrieve their physical monitor instances
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
                {
                    var supported = GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint arrSize);
                    if (!supported)
                    {
                        return true;
                    }

                    var arr = new PHYSICAL_MONITOR[arrSize];
                    supported = GetPhysicalMonitorsFromHMONITOR(hMonitor, arrSize, arr);
                    if (!supported)
                    {
                        return true;
                    }

                    foreach (var mon in arr)
                    {
                        var mi = new MONITORINFOEX
                        {
                            Size = Marshal.SizeOf(typeof(MONITORINFOEX))
                        };

                        if (GetMonitorInfo(hMonitor, ref mi))
                        {
                            action.Invoke((hMonitor, mon, mi.GetUniqueId()));
                        }
                    }

                    DestroyPhysicalMonitors((uint)arr.Length, arr);

                    return true;
                }, IntPtr.Zero);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    }
}