using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace SimpleKVM.Displays.win
{
    [SupportedOSPlatform("windows6.1")]
    public static class MonitorController
    {
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

        /// <summary>
        /// Calls <paramref name="action"/> for every physical monitor of every screen, with the
        /// screen's geometry-based id. The physical monitor handle is only valid inside the callback.
        /// </summary>
        public static unsafe void EnumMonitors(Action<(IntPtr hMonitor, PHYSICAL_MONITOR PhysicalMonitor, string UniqueId)> action)
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, (RECT*)null, (hMonitor, hdc, rect, lparam) =>
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR((IntPtr)hMonitor.Value, out uint arrSize))
                {
                    return true;
                }

                var arr = new PHYSICAL_MONITOR[arrSize];
                if (!GetPhysicalMonitorsFromHMONITOR((IntPtr)hMonitor.Value, arrSize, arr))
                {
                    return true;
                }

                try
                {
                    var info = new MONITORINFOEXW();
                    info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);

                    if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&info))
                    {
                        var bounds = info.monitorInfo.rcMonitor;
                        var uniqueId = MonitorIdentity.FromBounds(bounds.left, bounds.top, bounds.right, bounds.bottom);

                        foreach (var mon in arr)
                        {
                            action.Invoke(((IntPtr)hMonitor.Value, mon, uniqueId));
                        }
                    }
                }
                finally
                {
                    //Released even when the callback throws, otherwise the handles leak
                    DestroyPhysicalMonitors((uint)arr.Length, arr);
                }

                return true;
            }, default);
        }
    }
}
