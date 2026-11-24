using System;

namespace SimpleKVM.Displays.Ddc
{
    /// <summary>
    /// A DDC/CI channel to one monitor, however the OS exposes it (an IOAVService on Apple
    /// Silicon, an i2c-dev bus on Linux). The monitor logic above it is the same everywhere.
    /// </summary>
    public interface IDdcTransport
    {
        /// <summary>The 128-byte EDID base block read over the channel, or null when it doesn't answer.</summary>
        byte[]? ReadEdid();

        bool GetVcp(byte vcpCode, out uint currentValue);

        /// <param name="sourceAddress">0x51 for standard DDC/CI, 0x50 for the LG sidechannel.</param>
        bool SetVcp(byte sourceAddress, byte vcpCode, uint value);

        /// <summary>The MCCS capabilities string, or null when the monitor doesn't answer capability requests.</summary>
        string? ReadCapabilitiesString(Action<string>? log = null);
    }
}
