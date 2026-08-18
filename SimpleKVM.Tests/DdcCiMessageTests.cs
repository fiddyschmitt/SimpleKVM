using SimpleKVM.Displays.I2C;

namespace SimpleKVM.Tests;

// The DDC/CI "set VCP" packet is wire protocol: a wrong byte or checksum means a monitor
// silently ignores the switch. These lock the exact layout the reference tools expect.
public class DdcCiMessageTests
{
    [Fact]
    public void BuildSetVcp_switch_to_hdmi_has_exact_expected_bytes()
    {
        // Source input (VCP 0x60) -> HDMI 1 (0x11), standard source address 0x51.
        var msg = DdcCiMessage.BuildSetVcp(sourceAddress: 0x51, vcpCode: 0x60, value: 0x11);

        // [source, 0x84 (len 4 | 0x80), 0x03 (SetVCP), vcp, valueHi, valueLo, checksum]
        Assert.Equal(new byte[] { 0x51, 0x84, 0x03, 0x60, 0x00, 0x11, 0xC9 }, msg);
    }

    [Fact]
    public void BuildSetVcp_layout_is_seven_bytes_in_the_documented_order()
    {
        var msg = DdcCiMessage.BuildSetVcp(sourceAddress: 0x51, vcpCode: 0x10, value: 0x1234);

        Assert.Equal(7, msg.Length);
        Assert.Equal(0x51, msg[0]); // source address
        Assert.Equal(0x84, msg[1]); // length 4, high bit set
        Assert.Equal(0x03, msg[2]); // SetVCP opcode
        Assert.Equal(0x10, msg[3]); // VCP code
        Assert.Equal(0x12, msg[4]); // value high byte
        Assert.Equal(0x34, msg[5]); // value low byte
    }

    [Theory]
    [InlineData((byte)0x51, (byte)0x60, 0x11u)]   // standard input switch
    [InlineData((byte)0x50, (byte)0xF4, 0x90u)]   // LG sidechannel: source 0x50, VCP 0xF4
    [InlineData((byte)0x51, (byte)0x10, 0xFFFFu)] // full 16-bit value
    [InlineData((byte)0x51, (byte)0x60, 0x00u)]   // zero value
    public void BuildSetVcp_checksum_makes_the_whole_packet_xor_to_the_seed(byte source, byte vcp, uint value)
    {
        var msg = DdcCiMessage.BuildSetVcp(source, vcp, value);

        // The checksum is seeded with the destination address (0x6E), so XOR-ing every byte of
        // the packet (checksum included) reproduces the seed. This is exactly how a monitor
        // validates the packet, so it is the invariant that matters.
        byte xor = 0;
        foreach (var b in msg) xor ^= b;
        Assert.Equal(DdcCiMessage.DestinationAddress, xor);
    }

    [Fact]
    public void BuildSetVcp_uses_the_lg_sidechannel_source_and_vcp_for_lg_input_switch()
    {
        var msg = DdcCiMessage.BuildSetVcp(LgInputSources.SourceAddress, LgInputSources.VcpCode, 0x90);

        Assert.Equal(0x50, msg[0]);
        Assert.Equal(0xF4, msg[3]);
        Assert.Equal(0x90, msg[5]);
    }
}
