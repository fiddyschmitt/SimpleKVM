using System.Collections.Generic;

namespace SimpleKVM.Displays.win.I2C
{
    public static class LgInputSources
    {
        public const byte VcpCode = 0xF4;

        /// <summary>
        /// LG's service/factory sidechannel ("DDC2AB") is selected by using 0x50 as the DDC source
        /// address byte instead of the standard 0x51. The message is still sent to I2C slave 0x37.
        /// See https://github.com/rockowitz/ddcutil/wiki/Switching-input-source-on-LG-monitors
        /// </summary>
        public const byte SourceAddress = 0x50;

        public static List<(int SourceId, string SourceName)> GetDefaultSources()
        {
            return
            [
                (0x90, "HDMI 1"),
                (0x91, "HDMI 2"),
                (0xD0, "DisplayPort 1"),
                (0xD1, "DisplayPort 2 / USB-C"),
            ];
        }
    }
}
