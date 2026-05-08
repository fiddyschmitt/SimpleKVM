using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SimpleKVM.Displays.win.I2C
{
    public class NvApiTransport : II2CTransport
    {
        bool _initialized;
        IntPtr _dllHandle;

        delegate int NvAPI_Initialize_Delegate();
        delegate int NvAPI_EnumPhysicalGPUs_Delegate([Out] IntPtr[] gpuHandles, out int gpuCount);
        delegate int NvAPI_GPU_GetConnectedDisplayIds_Delegate(IntPtr gpuHandle, [In, Out] NV_GPU_DISPLAYIDS[]? displayIds, ref int displayIdCount, int flags);
        delegate int NvAPI_I2CWrite_Delegate(IntPtr gpuHandle, ref NV_I2C_INFO_V3 i2cInfo);
        delegate int NvAPI_GPU_GetEDID_Delegate(IntPtr gpuHandle, int displayId, ref NV_EDID edid);

        NvAPI_Initialize_Delegate? _NvAPI_Initialize;
        NvAPI_EnumPhysicalGPUs_Delegate? _NvAPI_EnumPhysicalGPUs;
        NvAPI_GPU_GetConnectedDisplayIds_Delegate? _NvAPI_GPU_GetConnectedDisplayIds;
        NvAPI_I2CWrite_Delegate? _NvAPI_I2CWrite;
        NvAPI_GPU_GetEDID_Delegate? _NvAPI_GPU_GetEDID;

        const uint NVAPI_INITIALIZE = 0x0150E828;
        const uint NVAPI_ENUMPHYSICALGPUS = 0xE5AC921F;
        const uint NVAPI_GPU_GETCONNECTEDDISPLAYIDS = 0x0078DBA2;
        const uint NVAPI_I2CWRITE = 0xE812EB07;
        const uint NVAPI_GPU_GETEDID = 0x37D32E69;

        delegate IntPtr NvAPI_QueryInterface_Delegate(uint id);
        NvAPI_QueryInterface_Delegate? _queryInterface;

        public bool IsAvailable { get; private set; }

        public NvApiTransport()
        {
            try
            {
                if (!NativeLibrary.TryLoad("nvapi64", out _dllHandle))
                    return;

                if (!NativeLibrary.TryGetExport(_dllHandle, "nvapi_QueryInterface", out IntPtr qiPtr))
                    return;

                _queryInterface = Marshal.GetDelegateForFunctionPointer<NvAPI_QueryInterface_Delegate>(qiPtr);

                _NvAPI_Initialize = GetNvDelegate<NvAPI_Initialize_Delegate>(NVAPI_INITIALIZE);
                _NvAPI_EnumPhysicalGPUs = GetNvDelegate<NvAPI_EnumPhysicalGPUs_Delegate>(NVAPI_ENUMPHYSICALGPUS);
                _NvAPI_GPU_GetConnectedDisplayIds = GetNvDelegate<NvAPI_GPU_GetConnectedDisplayIds_Delegate>(NVAPI_GPU_GETCONNECTEDDISPLAYIDS);
                _NvAPI_I2CWrite = GetNvDelegate<NvAPI_I2CWrite_Delegate>(NVAPI_I2CWRITE);
                _NvAPI_GPU_GetEDID = GetNvDelegate<NvAPI_GPU_GetEDID_Delegate>(NVAPI_GPU_GETEDID);

                if (_NvAPI_Initialize == null || _NvAPI_EnumPhysicalGPUs == null ||
                    _NvAPI_GPU_GetConnectedDisplayIds == null || _NvAPI_I2CWrite == null ||
                    _NvAPI_GPU_GetEDID == null)
                    return;

                int result = _NvAPI_Initialize();
                if (result != 0) return;

                _initialized = true;
                IsAvailable = true;
            }
            catch
            {
                IsAvailable = false;
            }
        }

        T? GetNvDelegate<T>(uint functionId) where T : Delegate
        {
            IntPtr ptr = _queryInterface!(functionId);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        public List<I2CDisplayInfo> EnumerateDisplays()
        {
            var result = new List<I2CDisplayInfo>();
            if (!_initialized) return result;

            var gpuHandles = new IntPtr[64];
            int status = _NvAPI_EnumPhysicalGPUs!(gpuHandles, out int gpuCount);
            if (status != 0) return result;

            for (int g = 0; g < gpuCount; g++)
            {
                IntPtr gpu = gpuHandles[g];

                int displayCount = 0;
                status = _NvAPI_GPU_GetConnectedDisplayIds!(gpu, null, ref displayCount, 0);
                if (status != 0 || displayCount == 0) continue;

                var displayIds = new NV_GPU_DISPLAYIDS[displayCount];
                for (int i = 0; i < displayCount; i++)
                    displayIds[i].version = NV_GPU_DISPLAYIDS_VER;

                status = _NvAPI_GPU_GetConnectedDisplayIds(gpu, displayIds, ref displayCount, 0);
                if (status != 0) continue;

                for (int d = 0; d < displayCount; d++)
                {
                    var display = displayIds[d];
                    if (!display.isConnected) continue;

                    var edid = ReadEdid(gpu, display.displayId);

                    result.Add(new I2CDisplayInfo
                    {
                        VendorDisplayHandle = new NvDisplayHandle(gpu, display.displayId),
                        EdidManufacturerId = edid.ManufacturerId,
                        EdidProductCode = edid.ProductCode,
                        EdidSerial = edid.Serial,
                        ConnectorType = MapConnectorType(display.connectorType)
                    });
                }
            }

            return result;
        }

        (ushort ManufacturerId, ushort ProductCode, uint Serial) ReadEdid(IntPtr gpuHandle, int displayId)
        {
            var edid = new NV_EDID();
            edid.version = NV_EDID_VER;
            edid.edidData = new byte[256];

            int status = _NvAPI_GPU_GetEDID!(gpuHandle, displayId, ref edid);
            if (status != 0 || edid.edidSize < 18)
                return (0, 0, 0);

            byte[] data = edid.edidData;
            if (data[0] != 0x00 || data[1] != 0xFF)
                return (0, 0, 0);

            ushort mfg = (ushort)((data[8] << 8) | data[9]);
            ushort product = (ushort)(data[10] | (data[11] << 8));
            uint serial = (uint)(data[12] | (data[13] << 8) | (data[14] << 16) | (data[15] << 24));

            return (mfg, product, serial);
        }

        public bool SetVcp(object displayHandle, byte i2cAddress, byte vcpCode, uint value)
        {
            if (!_initialized || displayHandle is not NvDisplayHandle handle) return false;

            byte[] msg = DdcCiMessage.BuildSetVcp(i2cAddress, vcpCode, value);

            var i2cInfo = new NV_I2C_INFO_V3();
            i2cInfo.version = NV_I2C_INFO_V3_VER;
            i2cInfo.displayMask = (uint)(1 << handle.DisplayId);
            i2cInfo.bIsDDCPort = true;
            i2cInfo.i2cDevAddress = (byte)(i2cAddress << 1);
            i2cInfo.pbI2cRegAddress = IntPtr.Zero;
            i2cInfo.regAddrSize = 0;
            i2cInfo.i2cSpeed = 0xFFFF;
            i2cInfo.i2cSpeedKhz = NV_I2C_SPEED_DEFAULT;
            i2cInfo.portId = 1;

            IntPtr dataBuf = Marshal.AllocHGlobal(msg.Length);
            try
            {
                Marshal.Copy(msg, 0, dataBuf, msg.Length);
                i2cInfo.pbData = dataBuf;
                i2cInfo.cbSize = (uint)msg.Length;

                int status = _NvAPI_I2CWrite!(handle.GpuHandle, ref i2cInfo);
                return status == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(dataBuf);
            }
        }

        public bool GetVcp(object displayHandle, byte i2cAddress, byte vcpCode, out uint value)
        {
            value = 0;
            return false;
        }

        static ConnectorType MapConnectorType(int nvConnector)
        {
            return nvConnector switch
            {
                0 => ConnectorType.VGA,
                2 or 3 or 32 or 33 => ConnectorType.DVI,
                12 or 13 => ConnectorType.HDMI,
                16 or 17 => ConnectorType.DisplayPort,
                _ => ConnectorType.Unknown
            };
        }

        static readonly int NV_GPU_DISPLAYIDS_VER = Marshal.SizeOf<NV_GPU_DISPLAYIDS>() | (3 << 16);
        static readonly int NV_EDID_VER = Marshal.SizeOf<NV_EDID>() | (3 << 16);
        static readonly int NV_I2C_INFO_V3_VER = Marshal.SizeOf<NV_I2C_INFO_V3>() | (3 << 16);
        const int NV_I2C_SPEED_DEFAULT = 0xFFFF;

        [StructLayout(LayoutKind.Sequential)]
        struct NV_GPU_DISPLAYIDS
        {
            public int version;
            public int connectorType;
            public int displayId;
            public uint flags;

            public bool isConnected => (flags & 0x04) != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NV_EDID
        {
            public int version;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] edidData;
            public int edidSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NV_I2C_INFO_V3
        {
            public int version;
            public uint displayMask;
            [MarshalAs(UnmanagedType.U1)]
            public bool bIsDDCPort;
            public byte i2cDevAddress;
            public IntPtr pbI2cRegAddress;
            public uint regAddrSize;
            public IntPtr pbData;
            public uint cbSize;
            public uint i2cSpeed;
            public int i2cSpeedKhz;
            public byte portId;
        }
    }

    public class NvDisplayHandle(IntPtr gpuHandle, int displayId)
    {
        public IntPtr GpuHandle { get; } = gpuHandle;
        public int DisplayId { get; } = displayId;
    }
}
