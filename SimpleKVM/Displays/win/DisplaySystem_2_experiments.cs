using DDCKVMService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

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
            MONITORINFOEX mi = new MONITORINFOEX();
            mi.Size = Marshal.SizeOf(typeof(MONITORINFOEX));
            if (GetMonitorInfo(hMonitor, ref mi))
                Debug.WriteLine(mi.DeviceName);

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
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000,
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_FORCE_UINT32 = 0xFFFFFFFF
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
            DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
            DISPLAYCONFIG_ROTATION rotation;
            DISPLAYCONFIG_SCALING scaling;
            DISPLAYCONFIG_RATIONAL refreshRate;
            DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
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
        public struct POINTL
        {
            int x;
            int y;
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
        public static extern int GetDisplayConfigBufferSizes(
            QUERY_DEVICE_CONFIG_FLAGS Flags,
            out uint NumPathArrayElements,
            out uint NumModeInfoArrayElements
        );

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(
            QUERY_DEVICE_CONFIG_FLAGS Flags,
            ref uint NumPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] PathInfoArray,
            ref uint NumModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] ModeInfoArray,
            IntPtr CurrentTopologyId
        );

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(
            ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName
        );

        public static string MonitorFriendlyName(LUID adapterId, uint targetId)
        {
            DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
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
            uint PathCount, ModeCount;
            int error = GetDisplayConfigBufferSizes(QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                out PathCount, out ModeCount);
            if (error != ERROR_SUCCESS)
                throw new Win32Exception(error);

            DISPLAYCONFIG_PATH_INFO[] DisplayPaths = new DISPLAYCONFIG_PATH_INFO[PathCount];
            DISPLAYCONFIG_MODE_INFO[] DisplayModes = new DISPLAYCONFIG_MODE_INFO[ModeCount];
            error = QueryDisplayConfig(QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                ref PathCount, DisplayPaths, ref ModeCount, DisplayModes, IntPtr.Zero);
            if (error != ERROR_SUCCESS)
                throw new Win32Exception(error);

            for (int i = 0; i < ModeCount; i++)
            {
                if (DisplayModes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                {
                    Debug.WriteLine(MonitorFriendlyName(DisplayModes[i].adapterId, DisplayModes[i].id));
                }
            }
            







            Debug.WriteLine("------------------");
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumProc, IntPtr.Zero);



            Debug.WriteLine("------------------");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBasicDisplayParams");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                foreach (var property in queryObj.Properties)
                {
                    Debug.WriteLine($"{property.Name}: {property.Value}");
                }
            }
            /*
            Active: True
            DisplayTransferCharacteristic: 120
            InstanceName: DISPLAY\VSC5B34\5&1068e3a9&0&UID4353_0
            MaxHorizontalImageSize: 94
            MaxVerticalImageSize: 53
            SupportedDisplayFeatures: System.Management.ManagementBaseObject
            VideoInputType: 1

            Active: True
            DisplayTransferCharacteristic: 120
            InstanceName: DISPLAY\VSC5B34\5&1068e3a9&0&UID4356_0
            MaxHorizontalImageSize: 94
            MaxVerticalImageSize: 53
            SupportedDisplayFeatures: System.Management.ManagementBaseObject
            VideoInputType: 1        
            */


            Debug.WriteLine("------------------");
            searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_DesktopMonitor");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                foreach (var property in queryObj.Properties)
                {
                    Debug.WriteLine($"{property.Name}: {property.Value}");
                }

                Debug.WriteLine("");
            }
            /*
            Availability: 8
            Bandwidth: 
            Caption: Generic PnP Monitor
            ConfigManagerErrorCode: 0
            ConfigManagerUserConfig: False
            CreationClassName: Win32_DesktopMonitor
            Description: Generic PnP Monitor
            DeviceID: DesktopMonitor1
            DisplayType: 
            ErrorCleared: 
            ErrorDescription: 
            InstallDate: 
            IsLocked: 
            LastErrorCode: 
            MonitorManufacturer: (Standard monitor types)
            MonitorType: Generic PnP Monitor
            Name: Generic PnP Monitor
            PixelsPerXLogicalInch: 288
            PixelsPerYLogicalInch: 288
            PNPDeviceID: DISPLAY\VSC5B34\5&1068E3A9&0&UID4353
            PowerManagementCapabilities: 
            PowerManagementSupported: 
            ScreenHeight: 
            ScreenWidth: 
            Status: OK
            StatusInfo: 
            SystemCreationClassName: Win32_ComputerSystem
            SystemName: MAXIMUS
                         */


            Debug.WriteLine("------------------");
            searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorID");

            foreach (ManagementObject queryObj in searcher.Get())
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
            }
            /*
Active: True
InstanceName: DISPLAY\VSC5B34\5&1068e3a9&0&UID4353_0
ManufacturerName: VSC
ProductCodeID: 5B34
SerialNumberID: 16843009
UserFriendlyName: VX4380 SERIES
UserFriendlyNameLength: 13
WeekOfManufacture: 14
YearOfManufacture: 2017

Active: True
InstanceName: DISPLAY\VSC5B34\5&1068e3a9&0&UID4356_0
ManufacturerName: VSC
ProductCodeID: 5B34
SerialNumberID: 16843009
UserFriendlyName: VX4380 SERIES
UserFriendlyNameLength: 13
WeekOfManufacture: 14
YearOfManufacture: 2017
            */

            Debug.WriteLine("------------------");
            searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorConnectionParams");

            foreach (ManagementObject queryObj in searcher.Get())
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

            /*
Active: True
InstanceName: DISPLAY\VSC5B34\5&1068e3a9&0&UID4353_0
VideoOutputTechnology: 5

Active: True
InstanceName: DISPLAY\VSC5B34\5&1068e3a9&0&UID4356_0
VideoOutputTechnology: 10
            */


                        var displayInfo = new List<DISPLAY_DEVICE>();
                        DISPLAY_DEVICE d = new DISPLAY_DEVICE();
                        d.cb = Marshal.SizeOf(d);

                        var result = new List<Monitor>();
                        Debug.WriteLine("------------------");

                        var screens = System.Windows.Forms.Screen.AllScreens.ToList();
                        screens
                            .ForEach(screen =>
                            {
                                for (uint id = 0; EnumDisplayDevices(screen.DeviceName, id, ref d, 0x00000001); id++)
                                {
                                    /*
            0
            \\.\DISPLAY1\Monitor0
            Generic PnP Monitor
            AttachedToDesktop, MultiDriver
            MONITOR\VSC5B34\{4d36e96e-e325-11ce-bfc1-08002be10318}\0004
            \Registry\Machine\System\CurrentControlSet\Control\Class\{4d36e96e-e325-11ce-bfc1-08002be10318}\0004

            0
            \\.\DISPLAY2\Monitor0
            Generic PnP Monitor
            AttachedToDesktop, MultiDriver
            MONITOR\VSC5B34\{4d36e96e-e325-11ce-bfc1-08002be10318}\0000
            \Registry\Machine\System\CurrentControlSet\Control\Class\{4d36e96e-e325-11ce-bfc1-08002be10318}\0000
                                    */

            Debug.WriteLine(id);
                        Debug.WriteLine(d.DeviceName);
                        Debug.WriteLine(d.DeviceString);
                        Debug.WriteLine(d.StateFlags);

                        Debug.WriteLine(d.DeviceID);
                                    //When dwFlags = 0
                                    //  MONITOR\VSC5B34\{4d36e96e-e325-11ce-bfc1-08002be10318}\0004

                                    //When dwFlags = 0x00000001
                                    //  \\?\DISPLAY#VSC5B34#5&1068e3a9&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}         //marry this up with WmiMonitorID.InstanceName. That way we can use DeviceName for cmm.exe, and WmiMonitorID.UserFriendlyName for displaying. Or just use physicalMonitor.model and assume it'll always be in the same order


                                    //Second time
                                    //\\?\DISPLAY#VSC5B34#5&1068e3a9&0&UID4356#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}

                                    Debug.WriteLine(d.DeviceKey);

                        d.cb = Marshal.SizeOf(d);
                        displayInfo.Add(d);
                    }
                });

            Debug.WriteLine("------------------");
            MonitorController.GetDevices(physicalMonitors =>
            {
                physicalMonitors
                    .ForEach(physicalMonitor =>
                    {
                        Debug.WriteLine(physicalMonitor.hPhysicalMonitor);
                        Debug.WriteLine(physicalMonitor.szPhysicalMonitorDescription);
                        Debug.WriteLine(physicalMonitor.GetVCPCapabilities());

                        physicalMonitor.GetVCPRegister(0x60, out uint currentSource);
                        Debug.WriteLine(currentSource);
                    });

                /*
                    2
                    Generic PnP Monitor
                    (
                        prot(monitor)
                        type(LCD)
                        model(VX4380)
                        cmds(01 02 03 07 0C E3 F3)
                        vcp(02 04 05 08 0B 0C 10 12 14(01 08 06 05 04 0B) 16 18 1A 52 60(0F 10 11 12) 62 87 8D(01 02) A5 AC AE B2 B6 C6 C8 CA CC(01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 12 14 16 17 1A 1E 24) D6(01 04 05) DC(00 01 02 03 05 08 1F) DF E0(00 01 02 03 14) EC(01 02 03) F6 F7(42 FF) FA(00 01 02) FB FC FD FE(00 01 02 04) FF)
                        mswhql(1)
                        asset_eep(40)
                        mccs_ver(2.2)
                    )
                    18

                    3
                    Generic PnP Monitor
                    (
                        prot(monitor)
                        type(LCD)
                        model(VX4380)
                        cmds(01 02 03 07 0C E3 F3)
                        vcp(02 04 05 08 0B 0C 10 12 14(01 08 06 05 04 0B) 16 18 1A 52 60(0F 10 11 12) 62 87 8D(01 02) A5 AC AE B2 B6 C6 C8 CA CC(01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 12 14 16 17 1A 1E 24) D6(01 04 05) DC(00 01 02 03 05 08 1F) DF E0(00 01 02 03 14) EC(01 02 03) F6 F7(42 FF) FA(00 01 02) FB FC FD FE(00 01 02 04) FF)
                        mswhql(1)
                        asset_eep(40)
                        mccs_ver(2.2)
                    )
                    18
                */

            });

            /*
             * var adapters = GetAdapers();
            var monitors = adapters
                            .SelectMany(adapter =>
                            {

                            })
                            .ToList();
            */

            return new List<Monitor>();
        }

        public static IList<DISPLAY_DEVICE> GetAdapers()
        {
            var result = new List<DISPLAY_DEVICE>();
            DISPLAY_DEVICE d = new DISPLAY_DEVICE();
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
