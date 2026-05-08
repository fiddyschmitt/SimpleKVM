using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SimpleKVM.Displays.win.I2C;

namespace SimpleKVM.Displays.win
{
    public class EdidDisplayInfo
    {
        public required string UniqueId { get; set; }
        public ushort EdidManufacturerId { get; set; }
        public ushort EdidProductCode { get; set; }
        public ConnectorType ConnectorType { get; set; }
    }

    public static class EdidHelper
    {
        public static List<EdidDisplayInfo> GetDisplayEdidInfo()
        {
            var result = new List<EdidDisplayInfo>();

            try
            {
                int error = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS,
                    out uint pathCount, out uint modeCount);
                if (error != 0) return result;

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                error = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS,
                    ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (error != 0) return result;

                foreach (var path in paths.Take((int)pathCount))
                {
                    var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    deviceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                    deviceName.header.adapterId = path.targetInfo.adapterId;
                    deviceName.header.id = path.targetInfo.id;
                    deviceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;

                    error = DisplayConfigGetDeviceInfo(ref deviceName);
                    if (error != 0) continue;

                    // Find the source mode for this path to get screen position
                    string? uniqueId = null;
                    if (path.sourceInfo.modeInfoIdx < modeCount)
                    {
                        var sourceMode = modes[path.sourceInfo.modeInfoIdx];
                        if (sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                        {
                            var pos = sourceMode.sourceMode.position;
                            var screen = Screen.AllScreens.FirstOrDefault(s =>
                                s.Bounds.Left == pos.x && s.Bounds.Top == pos.y);
                            if (screen != null)
                            {
                                uniqueId = screen.GetUniqueId();
                            }
                        }
                    }

                    if (uniqueId == null) continue;

                    result.Add(new EdidDisplayInfo
                    {
                        UniqueId = uniqueId,
                        EdidManufacturerId = deviceName.edidManufactureId,
                        EdidProductCode = deviceName.edidProductCodeId,
                        ConnectorType = MapOutputTechnology(deviceName.outputTechnology)
                    });
                }
            }
            catch
            {
                // DisplayConfig APIs may fail on some configurations
            }

            return result;
        }

        static ConnectorType MapOutputTechnology(uint outputTechnology)
        {
            return outputTechnology switch
            {
                0 => ConnectorType.VGA,       // HD15
                4 => ConnectorType.DVI,
                5 => ConnectorType.HDMI,
                10 => ConnectorType.DisplayPort, // External
                11 => ConnectorType.DisplayPort, // Embedded
                _ => ConnectorType.Unknown
            };
        }

        #region DisplayConfig P/Invoke

        const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
        const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;

        [DllImport("user32.dll")]
        static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray, ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);

        [StructLayout(LayoutKind.Sequential)]
        struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            DISPLAYCONFIG_RATIONAL hSyncFreq;
            DISPLAYCONFIG_RATIONAL vSyncFreq;
            DISPLAYCONFIG_2DREGION activeSize;
            DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINTL
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;

            public DISPLAYCONFIG_SOURCE_MODE sourceMode => modeInfo.sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags;
            public uint outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        #endregion
    }
}
