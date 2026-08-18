using SimpleKVM.Displays;

namespace SimpleKVM.Tests;

public class VcpSourceNamesTests
{
    [Theory]
    [InlineData(-1, "Leave unchanged")]
    [InlineData(1, "VGA 1")]
    [InlineData(3, "DVI 1")]
    [InlineData(15, "DisplayPort 1")]
    [InlineData(17, "HDMI 1")]
    [InlineData(18, "HDMI 2")]
    public void Known_source_ids_map_to_their_mccs_names(int id, string expected)
    {
        Assert.Equal(expected, VcpSourceNames.SourceIdToName(id));
    }

    [Fact]
    public void Unknown_source_id_falls_back_to_its_number()
    {
        Assert.Equal("153", VcpSourceNames.SourceIdToName(0x99));
    }
}
