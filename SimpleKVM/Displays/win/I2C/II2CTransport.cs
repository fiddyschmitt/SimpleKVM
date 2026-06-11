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

        /// <param name="sourceAddress">The DDC source address byte (0x51 standard, 0x50 for the LG sidechannel).
        /// The I2C slave address is always the standard DDC/CI 0x37 (0x6E on the wire).</param>
        bool SetVcp(object displayHandle, byte sourceAddress, byte vcpCode, uint value);
        bool GetVcp(object displayHandle, byte sourceAddress, byte vcpCode, out uint value);
    }
}
