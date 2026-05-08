using System.Collections.Generic;

namespace SimpleKVM.Displays.win.I2C
{
    public static class LgInputSources
    {
        public const byte VcpCode = 0xF4;
        public const byte I2CAddress = 0x50;

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
