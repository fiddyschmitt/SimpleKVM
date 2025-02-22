using DDCKVMService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace SimpleKVM.Displays.win
{
    public static class DisplaySystem_2_experiments
    {
        [DllImport("user32.dll")]
        static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX
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

        delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        static bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var mi = new MONITORINFOEX
            {
                Size = Marshal.SizeOf(typeof(MONITORINFOEX))
            };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                var miStr = mi.SerializeToJson();
                Debug.WriteLine(miStr);
            }

            Debug.WriteLine("");
            return true;
        }

        public const int ERROR_SUCCESS = 0;

        public enum QUERY_DEVICE_CONFIG_FLAGS : uint
        {
            QDC_ALL_PATHS = 0x00000001,
            QDC_ONLY_ACTIVE_PATHS = 0x00000002,
            QDC_DATABASE_CURRENT = 0x00000004
        }

        public enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
        {
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_OTHER = 0xFFFFFFFF,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HD15 = 0,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SVIDEO = 1,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPOSITE_VIDEO = 2,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPONENT_VIDEO = 3,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DVI = 4,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HDMI = 5,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS = 6,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_D_JPN = 8,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDI = 9,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EXTERNAL = 10,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 11,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EXTERNAL = 12,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED = 13,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDTVDONGLE = 14,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_MIRACAST = 15,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000
        }

        public enum DISPLAYCONFIG_SCANLINE_ORDERING : uint
        {
            DISPLAYCONFIG_SCANLINE_ORDERING_UNSPECIFIED = 0,
            DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE = 1,
            DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED = 2,
            DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_UPPERFIELDFIRST = DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED,
            DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_LOWERFIELDFIRST = 3,
            DISPLAYCONFIG_SCANLINE_ORDERING_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_ROTATION : uint
        {
            DISPLAYCONFIG_ROTATION_IDENTITY = 1,
            DISPLAYCONFIG_ROTATION_ROTATE90 = 2,
            DISPLAYCONFIG_ROTATION_ROTATE180 = 3,
            DISPLAYCONFIG_ROTATION_ROTATE270 = 4,
            DISPLAYCONFIG_ROTATION_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_SCALING : uint
        {
            DISPLAYCONFIG_SCALING_IDENTITY = 1,
            DISPLAYCONFIG_SCALING_CENTERED = 2,
            DISPLAYCONFIG_SCALING_STRETCHED = 3,
            DISPLAYCONFIG_SCALING_ASPECTRATIOCENTEREDMAX = 4,
            DISPLAYCONFIG_SCALING_CUSTOM = 5,
            DISPLAYCONFIG_SCALING_PREFERRED = 128,
            DISPLAYCONFIG_SCALING_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_PIXELFORMAT : uint
        {
            DISPLAYCONFIG_PIXELFORMAT_8BPP = 1,
            DISPLAYCONFIG_PIXELFORMAT_16BPP = 2,
            DISPLAYCONFIG_PIXELFORMAT_24BPP = 3,
            DISPLAYCONFIG_PIXELFORMAT_32BPP = 4,
            DISPLAYCONFIG_PIXELFORMAT_NONGDI = 5,
            DISPLAYCONFIG_PIXELFORMAT_FORCE_UINT32 = 0xffffffff
        }

        public enum DISPLAYCONFIG_MODE_INFO_TYPE : uint
        {
            DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1,
            DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2,
            DISPLAYCONFIG_MODE_INFO_TYPE_FORCE_UINT32 = 0xFFFFFFFF
        }

        public enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
        {
            DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1,
            DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2,
            DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE = 3,
            DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4,
            DISPLAYCONFIG_DEVICE_INFO_SET_TARGET_PERSISTENCE = 5,
            DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_BASE_TYPE = 6,
            DISPLAYCONFIG_DEVICE_INFO_FORCE_UINT32 = 0xFFFFFFFF
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            readonly DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
            readonly DISPLAYCONFIG_ROTATION rotation;
            readonly DISPLAYCONFIG_SCALING scaling;
            DISPLAYCONFIG_RATIONAL refreshRate;
            readonly DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public DISPLAYCONFIG_2DREGION activeSize;
            public DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct POINTL
        {
            readonly int x;
            readonly int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public DISPLAYCONFIG_PIXELFORMAT pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
        {
            public uint value;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS flags;
            public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        [DllImport("user32.dll")]
        static extern int GetDisplayConfigBufferSizes(
            QUERY_DEVICE_CONFIG_FLAGS Flags,
            out uint NumPathArrayElements,
            out uint NumModeInfoArrayElements
        );

        [DllImport("user32.dll")]
        static extern int QueryDisplayConfig(
            QUERY_DEVICE_CONFIG_FLAGS Flags,
            ref uint NumPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] PathInfoArray,
            ref uint NumModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] ModeInfoArray,
            IntPtr CurrentTopologyId
        );

        [DllImport("user32.dll")]
        static extern int DisplayConfigGetDeviceInfo(
            ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName
        );

        public static string MonitorFriendlyName(LUID adapterId, uint targetId)
        {
            var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            deviceName.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
            deviceName.header.adapterId = adapterId;
            deviceName.header.id = targetId;
            deviceName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            int error = DisplayConfigGetDeviceInfo(ref deviceName);
            if (error != ERROR_SUCCESS)
                throw new Win32Exception(error);
            return deviceName.monitorFriendlyDeviceName;
        }

        public static IList<Monitor> GetMonitors()
        {
            int error = GetDisplayConfigBufferSizes(QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                out uint PathCount, out uint ModeCount);
            if (error != ERROR_SUCCESS)
                throw new Win32Exception(error);

            DISPLAYCONFIG_PATH_INFO[] DisplayPaths = new DISPLAYCONFIG_PATH_INFO[PathCount];
            DISPLAYCONFIG_MODE_INFO[] DisplayModes = new DISPLAYCONFIG_MODE_INFO[ModeCount];
            error = QueryDisplayConfig(QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                ref PathCount, DisplayPaths, ref ModeCount, DisplayModes, IntPtr.Zero);
            if (error != ERROR_SUCCESS)
                throw new Win32Exception(error);

            Debug.WriteLine("------------------ QueryDisplayConfig (\"------------------");
            for (int i = 0; i < ModeCount; i++)
            {
                if (DisplayModes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                {
                    Debug.WriteLine(MonitorFriendlyName(DisplayModes[i].adapterId, DisplayModes[i].id));
                    Debug.WriteLine("");
                }
            }








            Debug.WriteLine("------------------ EnumDisplayMonitors (\"------------------");
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumProc, IntPtr.Zero);



            Debug.WriteLine("------------------ WmiMonitorBasicDisplayParams ------------------");
            var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBasicDisplayParams");
            foreach (ManagementObject queryObj in searcher.Get().Cast<ManagementObject>())
            {
                foreach (var property in queryObj.Properties)
                {
                    Debug.WriteLine($"{property.Name}: {property.Value}");
                }

                Debug.WriteLine("");
            }


            Debug.WriteLine("------------------ Win32_DesktopMonitor ------------------");
            searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_DesktopMonitor");
            foreach (ManagementObject queryObj in searcher.Get().Cast<ManagementObject>())
            {
                foreach (var property in queryObj.Properties)
                {
                    Debug.WriteLine($"{property.Name}: {property.Value}");
                }

                Debug.WriteLine("");
            }


            Debug.WriteLine("------------------ WmiMonitorID ------------------");
            searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorID");

            foreach (ManagementObject queryObj in searcher.Get().Cast<ManagementObject>())
            {
                foreach (var property in queryObj.Properties)
                {
                    if (property.Value is ushort[] unicodeArray)
                    {
                        var str = unicodeArray
                                        .Select(u => char.ConvertFromUtf32((int)u))
                                        .ToString("")
                                        .RemoveNonPrintable();

                        Debug.WriteLine($"{property.Name}: {str}");
                    }
                    else
                    {
                        Debug.WriteLine($"{property.Name}: {property.Value}");
                    }
                }

                Debug.WriteLine("");
            }



            Debug.WriteLine("------------------ WmiMonitorConnectionParams ------------------");
            searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorConnectionParams");

            foreach (ManagementObject queryObj in searcher.Get().Cast<ManagementObject>())
            {
                foreach (var property in queryObj.Properties)
                {
                    if (property.Value is ushort[] unicodeArray)
                    {
                        var str = unicodeArray
                                        .Select(u => char.ConvertFromUtf32((int)u))
                                        .ToString("")
                                        .RemoveNonPrintable();

                        Debug.WriteLine($"{property.Name}: {str}");
                    }
                    else
                    {
                        Debug.WriteLine($"{property.Name}: {property.Value}");
                    }
                }
                Debug.WriteLine("");
            }



            var displayInfo = new List<DISPLAY_DEVICE>();


            var result = new List<Monitor>();
            Debug.WriteLine("------------------ Screen.AllScreens ------------------");

            var screens = System.Windows.Forms.Screen.AllScreens.ToList();
            screens
                .ForEach(screen =>
                {
                    var screenJson = screen.SerializeToJson();
                    Debug.WriteLine(screenJson);

                    var d = new DISPLAY_DEVICE();
                    d.cb = Marshal.SizeOf(d);

                    for (uint id = 0; EnumDisplayDevices(screen.DeviceName, id, ref d, 0x00000001); id++)
                    {
                        Debug.WriteLine("\t------------------ EnumDisplayDevices ------------------");
                        Debug.WriteLine("\tDeviceName: " + d.DeviceName);
                        Debug.WriteLine("\tDeviceString: " + d.DeviceString);
                        Debug.WriteLine("\tStateFlags: " + d.StateFlags);
                        Debug.WriteLine("\tDeviceID: " + d.DeviceID);
                        Debug.WriteLine("\tDeviceKey: " + d.DeviceKey);
                        //When dwFlags = 0
                        //  MONITOR\VSC5B34\{4d36e96e-e325-11ce-bfc1-08002be10318}\0004

                        //When dwFlags = 0x00000001
                        //  \\?\DISPLAY#VSC5B34#5&1068e3a9&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}         //marry this up with WmiMonitorID.InstanceName. That way we can use DeviceName for cmm.exe, and WmiMonitorID.UserFriendlyName for displaying. Or just use physicalMonitor.model and assume it'll always be in the same order


                        //Second time
                        //\\?\DISPLAY#VSC5B34#5&1068e3a9&0&UID4356#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}

                        displayInfo.Add(d);
                    }

                    Debug.WriteLine("");
                });

            Debug.WriteLine("------------------ physicalMonitors ------------------");
            MonitorController.EnumMonitors(mon =>
            {
                Debug.WriteLine(mon.PhysicalMonitor.hPhysicalMonitor);
                Debug.WriteLine(mon.PhysicalMonitor.szPhysicalMonitorDescription);
                Debug.WriteLine(mon.PhysicalMonitor.GetVCPCapabilities());

                mon.PhysicalMonitor.GetVCPRegister(0x60, out uint currentSource);
                Debug.WriteLine(currentSource);
            });



            Debug.WriteLine("------------------ QueryDisplayDevices() ------------------");
            QueryDisplayDevices();

            Debug.WriteLine("------------------ InfoCombined() ------------------");
            InfoCombined();

            return [];
        }

        private static void InfoCombined()
        {
            var screens = System.Windows.Forms.Screen.AllScreens
                            .Select(screen =>
                            {
                                var scr = new Screen
                                {
                                    ScreenName = screen.DeviceName,
                                    Bounds = screen.Bounds
                                };

                                var d = new DISPLAY_DEVICE();
                                d.cb = Marshal.SizeOf(d);

                                for (uint id = 0; EnumDisplayDevices(screen.DeviceName, id, ref d, 0x00000001); id++)
                                {
                                    var dd = new DisplayDevice()
                                    {
                                        DisplayDeviceName = d.DeviceName
                                    };
                                    scr.DisplayDevices.Add(dd);
                                }

                                return scr;
                            })
            .ToList();

            bool monDel(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
            {
                var mi = new MONITORINFOEX
                {
                    Size = Marshal.SizeOf(typeof(MONITORINFOEX))
                };
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    var screen = screens
                                    .FirstOrDefault(screen => screen.ScreenName == mi.DeviceName);
                }

                Debug.WriteLine("");
                return true;
            }

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monDel, IntPtr.Zero);
        }

        public class Screen
        {
            public required string ScreenName { get; set; }
            public Rectangle Bounds { get; set; }
            public List<DisplayDevice> DisplayDevices = [];

            public override string ToString()
            {
                return ScreenName;
            }
        }

        public class DisplayDevice
        {
            public required string DisplayDeviceName { get; set; }

            public override string ToString()
            {
                return DisplayDeviceName;
            }
        }

        private static void QueryDisplayDevices()
        {
            var device = new DISPLAY_DEVICE();
            device.cb = Marshal.SizeOf(device);
            uint DispNum = 0;
            while (EnumDisplayDevices(null, DispNum, ref device, 0))
            {
                var dev = new DISPLAY_DEVICE();
                dev.cb = Marshal.SizeOf(dev);
                if (EnumDisplayDevices(device.DeviceName, 0, ref dev, 0))
                {
                    Debug.WriteLine("\tDeviceName: " + dev.DeviceName);
                    Debug.WriteLine("\tDeviceString: " + dev.DeviceString);
                    Debug.WriteLine("\tStateFlags: " + dev.StateFlags);
                    Debug.WriteLine("\tDeviceID: " + dev.DeviceID);
                    Debug.WriteLine("\tDeviceKey: " + dev.DeviceKey);

                    Debug.WriteLine("");
                }
                DispNum++;
                dev.cb = Marshal.SizeOf(dev);
            }
        }

        public static IList<DISPLAY_DEVICE> GetAdapters()
        {
            var result = new List<DISPLAY_DEVICE>();
            var d = new DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);
            try
            {
                for (uint id = 0; EnumDisplayDevices(null, id, ref d, 0); id++)
                {
                    Debug.WriteLine($"{id}, {d.DeviceName}, {d.DeviceString}, {d.StateFlags}, {d.DeviceID}, {d.DeviceKey}");
                    d.cb = Marshal.SizeOf(d);
                    result.Add(d);
                }
            }
            catch (Exception)
            {

            }

            return result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        [MarshalAs(UnmanagedType.U4)]
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [Flags]
    public enum DisplayDeviceStateFlags : int
    {
        /// <summary>The device is part of the desktop.</summary>
        AttachedToDesktop = 0x1,
        MultiDriver = 0x2,
        /// <summary>The device is part of the desktop.</summary>
        PrimaryDevice = 0x4,
        /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
        MirroringDriver = 0x8,
        /// <summary>The device is VGA compatible.</summary>
        VGACompatible = 0x10,
        /// <summary>The device is removable; it cannot be the primary display.</summary>
        Removable = 0x20,
        /// <summary>The device has more display modes than its output devices support.</summary>
        ModesPruned = 0x8000000,
        Remote = 0x4000000,
        Disconnect = 0x2000000
    }

}
