using SimpleKVM.Displays;

namespace SimpleKVM.Tests;

public class CapabilitiesParserTests
{
    // The exact MCCS capabilities string the Acer S240HL returns, captured via `--get-caps`.
    // This is the input that exposed the earlier mac parsing bug, so it earns a permanent test.
    const string AcerCaps =
        "(prot(monitor)type(LCD)model(ACER)cmds(01 02 03 07 0C E3 F3)" +
        "vcp(02 04 05 08 0B 10 12 14(05 08 0B) 16 18 1A 52 60(01 03 11) 6C 6E 70 " +
        "AC AE B2 B6 C6 C8 C9 CC(01 02 03 04 05 06 08 09 0A 0C 0D 0E 14 16 1E) D6(01 05) DF)" +
        "mswhql(1)asset_eep(40)mccs_ver(2.0))";

    [Fact]
    public void Parse_acer_reads_model_and_mccs_version()
    {
        var caps = CapabilitiesParser.Parse(AcerCaps);

        Assert.Equal("ACER", caps.Model);
        Assert.Equal("2.0", caps.MccsVersion);
    }

    [Fact]
    public void Parse_acer_reads_the_input_source_values_that_drive_the_source_list()
    {
        var caps = CapabilitiesParser.Parse(AcerCaps);

        // VCP 0x60 (input source) with its allowed values 0x01 (VGA), 0x03 (DVI), 0x11 (HDMI).
        Assert.True(caps.VcpFeatures.ContainsKey(0x60));
        Assert.Equal(new byte[] { 0x01, 0x03, 0x11 }, caps.VcpFeatures[0x60]);
    }

    [Fact]
    public void Parse_acer_keeps_nested_value_groups_scoped_to_their_own_code()
    {
        var caps = CapabilitiesParser.Parse(AcerCaps);

        Assert.Equal(new byte[] { 0x05, 0x08, 0x0B }, caps.VcpFeatures[0x14]); // colour preset
        Assert.Equal(new byte[] { 0x01, 0x05 }, caps.VcpFeatures[0xD6]);       // power mode
        // A code with no value group parses as present-with-no-values, not swallowed.
        Assert.True(caps.VcpFeatures.ContainsKey(0x10)); // brightness
        Assert.Empty(caps.VcpFeatures[0x10]);
        Assert.True(caps.VcpFeatures.ContainsKey(0xDF)); // VCP version, last code, no group
    }

    [Fact]
    public void Parse_handles_ddcutil_style_concatenated_codes()
    {
        // Some displays emit VCP codes with no separators; the parser must split every two hex chars.
        var caps = CapabilitiesParser.Parse("(vcp(101214))");

        Assert.Equal(new[] { (byte)0x10, (byte)0x12, (byte)0x14 }, caps.VcpFeatures.Keys.Order());
    }

    [Fact]
    public void Parse_tolerates_a_model_containing_spaces()
    {
        var caps = CapabilitiesParser.Parse("(model(DELL U2412M)mccs_ver(2.1))");

        Assert.Equal("DELL U2412M", caps.Model);
        Assert.Equal("2.1", caps.MccsVersion);
    }

    [Fact]
    public void Parse_of_junk_returns_empty_without_throwing()
    {
        var caps = CapabilitiesParser.Parse("not a capabilities string");

        Assert.Null(caps.Model);
        Assert.Null(caps.MccsVersion);
        Assert.Empty(caps.VcpFeatures);
    }
}
