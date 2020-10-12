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
        public static extern bool GetCapabilitiesStringLength(
            IntPtr hMonitor,
            ref uint pdwCapabilitiesStringLengthInCharacters
        );

        [DllImport("Dxva2.dll")]
        public static extern bool CapabilitiesRequestAndCapabilitiesReply(
            IntPtr hMonitor,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder pszASCIICapabilitiesString,
            uint dwCapabilitiesStringLengthInCharacters
        );

        [DllImport("Dxva2.dll")]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(
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

            public bool SetVCPRegister(byte register, uint value) => SetVCPFeature(this.hPhysicalMonitor, register, value);

            public bool GetVCPRegister(byte register, out uint value)
            {
                var supported = GetVCPFeatureAndVCPFeatureReply(this.hPhysicalMonitor, register, IntPtr.Zero, out value, out uint maxValue);
                return supported;
            }

            public string? GetVCPCapabilities()
            {
                uint length = 0;
                var supported = GetCapabilitiesStringLength(this.hPhysicalMonitor, ref length);
                if (!supported)
                {
                    return null;
                }

                var retval = new StringBuilder((int)length);
                supported = CapabilitiesRequestAndCapabilitiesReply(this.hPhysicalMonitor, retval, length);
                if (!supported)
                {
                    return null;
                }

                return retval.ToString();
            }
        }

        public static void GetDevices(Action<List<PHYSICAL_MONITOR>> handleCallback)
        {
            var handles = new List<PHYSICAL_MONITOR>();

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

                    handles.AddRange(arr);

                    return true;
                }, IntPtr.Zero);

            // Callback
            handleCallback(handles);

            // Cleanup
            DestroyPhysicalMonitors((uint)handles.Count, handles.ToArray());
        }
    }
}