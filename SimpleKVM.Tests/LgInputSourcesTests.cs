using SimpleKVM.Displays.I2C;

namespace SimpleKVM.Tests;

// The LG sidechannel constants are protocol facts confirmed against a real LG monitor; pinning
// them guards against an accidental edit silently breaking LG input switching.
public class LgInputSourcesTests
{
    [Fact]
    public void Sidechannel_constants_are_the_confirmed_values()
    {
        Assert.Equal(0xF4, LgInputSources.VcpCode);
        Assert.Equal(0x50, LgInputSources.SourceAddress);
        Assert.Equal(0x1E6D, LgInputSources.EdidManufacturerId); // "GSM"
    }

    [Fact]
    public void Default_sources_lead_with_hdmi_1()
    {
        var sources = LgInputSources.GetDefaultSources();

        Assert.Equal(4, sources.Count);
        Assert.Equal((0x90, "HDMI 1"), sources[0]);
        Assert.Contains((0xD0, "DisplayPort 1"), sources);
    }
}
