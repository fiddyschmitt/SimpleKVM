using System.Collections.Generic;

namespace SimpleKVM.Displays.win.I2C
{
    public enum ConnectorType
    {
        Unknown,
        VGA,
        DVI,
        HDMI,
        DisplayPort,
        UsbC
    }

    public class I2CDisplayInfo
    {
        public required object VendorDisplayHandle { get; set; }
        public required ushort EdidManufacturerId { get; set; }
        public required ushort EdidProductCode { get; set; }
        public uint EdidSerial { get; set; }
        public ConnectorType ConnectorType { get; set; }
    }

    public interface II2CTransport
    {
        bool IsAvailable { get; }
        List<I2CDisplayInfo> EnumerateDisplays();
        bool SetVcp(object displayHandle, byte i2cAddress, byte vcpCode, uint value);
        bool GetVcp(object displayHandle, byte i2cAddress, byte vcpCode, out uint value);
    }
}
