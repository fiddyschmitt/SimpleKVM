using SimpleKVM.Displays;

namespace SimpleKVM.Tests;

public class EdidTests
{
    readonly byte[] dell = TestEdid.Build("DEL", 0x4AA0, 123456, "DELL U2412M", "ABC123");

    [Fact]
    public void Reads_the_identity_fields()
    {
        Assert.True(Edid.IsValid(dell));
        Assert.Equal("DEL", Edid.PnpId(dell));
        Assert.Equal(0x10AC, Edid.ManufacturerId(dell));
        Assert.Equal(0x4AA0, Edid.ProductCode(dell));
        Assert.Equal(123456u, Edid.SerialNumber(dell));
        Assert.Equal("DELL U2412M", Edid.ModelName(dell));
        Assert.Equal("ABC123", Edid.SerialString(dell));
    }

    [Fact]
    public void Lg_manufacturer_id_matches_the_sidechannel_constant()
    {
        var lg = TestEdid.Build("GSM", 0x5A5A, 1, "LG ULTRAGEAR", null);
        Assert.Equal(SimpleKVM.Displays.I2C.LgInputSources.EdidManufacturerId, Edid.ManufacturerId(lg));
    }

    [Fact]
    public void Missing_descriptors_are_null()
    {
        var bare = TestEdid.Build("ACR", 1, 0, null, null);
        Assert.Null(Edid.ModelName(bare));
        Assert.Null(Edid.SerialString(bare));
    }

    [Fact]
    public void Key_is_the_base_block_only()
    {
        var withExtension = new byte[256];
        dell.CopyTo(withExtension, 0);
        withExtension[200] = 0x42;
        Assert.Equal(Edid.Key(dell), Edid.Key(withExtension));
        Assert.Equal(256, Edid.Key(dell).Length);
    }

    [Fact]
    public void Garbage_is_not_valid_and_does_not_throw()
    {
        Assert.False(Edid.IsValid(null));
        Assert.False(Edid.IsValid([]));
        Assert.False(Edid.IsValid(new byte[128]));
        Assert.Equal(0, Edid.ManufacturerId([1, 2]));
        Assert.Equal("", Edid.PnpId([]));
        Assert.Null(Edid.ModelName(new byte[20]));
    }
}
