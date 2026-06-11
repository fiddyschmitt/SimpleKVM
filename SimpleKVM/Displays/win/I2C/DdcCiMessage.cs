namespace SimpleKVM.Displays.win.I2C
{
    public static class DdcCiMessage
    {
        /// <summary>
        /// I2C slave 0x37 shifted left for a write. All DDC/CI traffic goes here, including the LG
        /// sidechannel (which only changes the source address byte, not the slave address).
        /// </summary>
        public const byte DestinationAddress = 0x6E;

        const byte SetVcpOpcode = 0x03;

        public static byte[] BuildSetVcp(byte sourceAddress, byte vcpCode, uint value)
        {
            byte msb = (byte)((value >> 8) & 0xFF);
            byte lsb = (byte)(value & 0xFF);

            byte[] msg =
            [
                sourceAddress,
                0x84, // length=4 | 0x80
                SetVcpOpcode,
                vcpCode,
                msb,
                lsb,
                0 // checksum placeholder
            ];

            byte checksum = DestinationAddress;
            for (int i = 0; i < msg.Length - 1; i++)
                checksum ^= msg[i];

            msg[^1] = checksum;
            return msg;
        }
    }
}
