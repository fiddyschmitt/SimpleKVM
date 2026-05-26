using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SimpleKVM.Displays.win.I2C
{
    public class AmdAdlTransport : II2CTransport
    {
        IntPtr _context;
        bool _initialized;

        delegate int ADL2_Main_Control_Create_Delegate(ADL_Main_Memory_Alloc_Delegate callback, int enumConnectedAdapters, out IntPtr context);
        delegate int ADL2_Main_Control_Destroy_Delegate(IntPtr context);
        delegate int ADL2_Adapter_NumberOfAdapters_Get_Delegate(IntPtr context, out int numAdapters);
        delegate int ADL2_Adapter_Active_Get_Delegate(IntPtr context, int adapterIndex, out int status);
        delegate int ADL2_Display_DisplayInfo_Get_Delegate(IntPtr context, int adapterIndex, out int numDisplays, out IntPtr displayInfoArray, int forceDetect);
        delegate int ADL2_Display_DDCBlockAccess_Get_Delegate(IntPtr context, int adapterIndex, int displayIndex, int commandIndex, int sendMsgLen, byte[] sendMsgBuf, out int recvMsgLen, byte[] recvMsgBuf);
        delegate IntPtr ADL_Main_Memory_Alloc_Delegate(int size);

        ADL2_Main_Control_Create_Delegate? _ADL2_Main_Control_Create;
        ADL2_Main_Control_Destroy_Delegate? _ADL2_Main_Control_Destroy;
        ADL2_Adapter_NumberOfAdapters_Get_Delegate? _ADL2_Adapter_NumberOfAdapters_Get;
        ADL2_Adapter_Active_Get_Delegate? _ADL2_Adapter_Active_Get;
        ADL2_Display_DisplayInfo_Get_Delegate? _ADL2_Display_DisplayInfo_Get;
        ADL2_Display_DDCBlockAccess_Get_Delegate? _ADL2_Display_DDCBlockAccess_Get;

        IntPtr _dllHandle;

        public bool IsAvailable { get; private set; }

        public AmdAdlTransport()
        {
            try
            {
                if (!NativeLibrary.TryLoad("atiadlxx", out _dllHandle))
                    return;

                _ADL2_Main_Control_Create = GetDelegate<ADL2_Main_Control_Create_Delegate>("ADL2_Main_Control_Create");
                _ADL2_Main_Control_Destroy = GetDelegate<ADL2_Main_Control_Destroy_Delegate>("ADL2_Main_Control_Destroy");
                _ADL2_Adapter_NumberOfAdapters_Get = GetDelegate<ADL2_Adapter_NumberOfAdapters_Get_Delegate>("ADL2_Adapter_NumberOfAdapters_Get");
                _ADL2_Adapter_Active_Get = GetDelegate<ADL2_Adapter_Active_Get_Delegate>("ADL2_Adapter_Active_Get");
                _ADL2_Display_DisplayInfo_Get = GetDelegate<ADL2_Display_DisplayInfo_Get_Delegate>("ADL2_Display_DisplayInfo_Get");
                _ADL2_Display_DDCBlockAccess_Get = GetDelegate<ADL2_Display_DDCBlockAccess_Get_Delegate>("ADL2_Display_DDCBlockAccess_Get");

                if (_ADL2_Main_Control_Create == null || _ADL2_Main_Control_Destroy == null ||
                    _ADL2_Adapter_NumberOfAdapters_Get == null || _ADL2_Display_DisplayInfo_Get == null ||
                    _ADL2_Display_DDCBlockAccess_Get == null)
                    return;

                int result = _ADL2_Main_Control_Create(ADLAllocMemory, 1, out _context);
                if (result != 0) return;

                _initialized = true;
                IsAvailable = true;
            }
            catch
            {
                IsAvailable = false;
            }
        }

        T? GetDelegate<T>(string name) where T : Delegate
        {
            if (!NativeLibrary.TryGetExport(_dllHandle, name, out IntPtr ptr))
                return null;
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        static IntPtr ADLAllocMemory(int size)
        {
            return Marshal.AllocHGlobal(size);
        }

        public List<I2CDisplayInfo> EnumerateDisplays()
        {
            var result = new List<I2CDisplayInfo>();
            if (!_initialized) return result;

            try
            {
                _ADL2_Adapter_NumberOfAdapters_Get!(_context, out int numAdapters);

                for (int adapterIdx = 0; adapterIdx < numAdapters; adapterIdx++)
                {
                    if (_ADL2_Adapter_Active_Get != null)
                    {
                        _ADL2_Adapter_Active_Get(_context, adapterIdx, out int active);
                        if (active == 0) continue;
                    }

                    int status = _ADL2_Display_DisplayInfo_Get!(_context, adapterIdx, out int numDisplays, out IntPtr displayInfoPtr, 0);
                    if (status != 0 || numDisplays == 0) continue;

                    try
                    {
                        int structSize = Marshal.SizeOf<ADLDisplayInfo>();
                        for (int d = 0; d < numDisplays; d++)
                        {
                            try
                            {
                                var info = Marshal.PtrToStructure<ADLDisplayInfo>(displayInfoPtr + d * structSize);

                                if ((info.displayInfoValue & ADL_DISPLAY_CONNECTED) == 0) continue;
                                if ((info.displayInfoValue & ADL_DISPLAY_MAPPED) == 0) continue;

                                var edid = ReadEdid(adapterIdx, info.displayID.displayLogicalIndex);

                                result.Add(new I2CDisplayInfo
                                {
                                    VendorDisplayHandle = new AmdDisplayHandle(adapterIdx, info.displayID.displayLogicalIndex),
                                    EdidManufacturerId = edid.ManufacturerId,
                                    EdidProductCode = edid.ProductCode,
                                    EdidSerial = edid.Serial,
                                    ConnectorType = MapConnectorType(info.displayConnector)
                                });
                            }
                            catch
                            {
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(displayInfoPtr);
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        (ushort ManufacturerId, ushort ProductCode, uint Serial) ReadEdid(int adapterIndex, int displayIndex)
        {
            try
            {
                var sendBuf = new byte[] { 0xA0, 0x00 };
                var recvBuf = new byte[128];

                int status = _ADL2_Display_DDCBlockAccess_Get!(_context, adapterIndex, displayIndex, 0, sendBuf.Length, sendBuf, out _, recvBuf);
                if (status != 0)
                    return (0, 0, 0);

                if (recvBuf.Length < 18 || recvBuf[0] != 0x00 || recvBuf[1] != 0xFF)
                    return (0, 0, 0);

                ushort mfg = (ushort)((recvBuf[8] << 8) | recvBuf[9]);
                ushort product = (ushort)(recvBuf[10] | (recvBuf[11] << 8));
                uint serial = (uint)(recvBuf[12] | (recvBuf[13] << 8) | (recvBuf[14] << 16) | (recvBuf[15] << 24));

                return (mfg, product, serial);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        public bool SetVcp(object displayHandle, byte i2cAddress, byte vcpCode, uint value)
        {
            if (!_initialized || displayHandle is not AmdDisplayHandle handle) return false;

            byte[] msg = DdcCiMessage.BuildSetVcp(i2cAddress, vcpCode, value);

            byte[] sendBuf = new byte[msg.Length + 1];
            sendBuf[0] = (byte)(i2cAddress << 1);
            Array.Copy(msg, 0, sendBuf, 1, msg.Length);

            var recvBuf = new byte[1];
            int status = _ADL2_Display_DDCBlockAccess_Get!(_context, handle.AdapterIndex, handle.DisplayIndex, 0, sendBuf.Length, sendBuf, out _, recvBuf);
            return status == 0;
        }

        public bool GetVcp(object displayHandle, byte i2cAddress, byte vcpCode, out uint value)
        {
            value = 0;
            return false;
        }

        static ConnectorType MapConnectorType(int adlConnector)
        {
            return adlConnector switch
            {
                1 => ConnectorType.VGA,
                2 or 3 => ConnectorType.DVI,
                4 or 7 or 12 => ConnectorType.HDMI,
                10 or 11 => ConnectorType.DisplayPort,
                _ => ConnectorType.Unknown
            };
        }

        const int ADL_DISPLAY_CONNECTED = 0x00000001;
        const int ADL_DISPLAY_MAPPED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        struct ADLDisplayID
        {
            public int displayLogicalIndex;
            public int displayPhysicalIndex;
            public int displayLogicalAdapterIndex;
            public int displayPhysicalAdapterIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ADLDisplayInfo
        {
            public ADLDisplayID displayID;
            public int displayControllerIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string displayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string displayManufacturerName;
            public int displayType;
            public int displayOutputType;
            public int displayConnector;
            public int displayInfoMask;
            public int displayInfoValue;
        }
    }

    public class AmdDisplayHandle(int adapterIndex, int displayIndex)
    {
        public int AdapterIndex { get; } = adapterIndex;
        public int DisplayIndex { get; } = displayIndex;
    }
}
