using SimpleKVM.Displays.linux;

namespace SimpleKVM.Tests;

// The Linux backend learns where each monitor sits on the desktop from the compositor, and
// pairs that with the kernel's DRM connectors (where the DDC bus hangs off). These pin the
// parsers to the exact text each tool prints and the pairing rules to the cases that broke.
public class LinuxLayoutTests
{
    // ------------------------------------------------------------------ GNOME / Mutter

    // What `gdbus call ... GetCurrentState` prints for a VM with one virtual display: the
    // serial and the first logical monitor's transform carry type annotations.
    const string MutterSingle =
        "(uint32 3, [(('Virtual-1', 'unknown', 'unknown', 'unknown'), [('1920x1080@60.000', 1920, 1080, 60.0, 1.0, [1.0, 1.25, 1.5, 1.75, 2.0], {'is-current': <true>, 'is-preferred': <true>}), ('1280x720@60.000', 1280, 720, 60.0, 1.0, [1.0], @a{sv} {})], {'is-builtin': <false>, 'display-name': <'Unknown Display'>})], " +
        "[(0, 0, 1.0, uint32 0, true, [('Virtual-1', 'unknown', 'unknown', 'unknown')], @a{sv} {})], " +
        "{'layout-mode': <uint32 1>, 'supports-changing-layout-mode': <true>, 'global-scale-required': <false>})";

    // Two monitors: a scaled, primary Dell on DP-1 and a rotated HDMI one to its right.
    const string MutterTwo =
        "(uint32 7, [(('DP-1', 'DEL', 'DELL U2412M', 'ABC123'), [('1920x1200@59.950', 1920, 1200, 59.950172424316406, 1.25, [1.0, 1.25], {'is-current': <true>})], {'is-builtin': <false>}), " +
        "(('HDMI-A-1', 'ACR', 'S240HL', 'unknown'), [('1920x1080@60.000', 1920, 1080, 60.0, 1.0, [1.0], {'is-current': <true>}), ('1280x720@60.000', 1280, 720, 60.0, 1.0, [1.0], @a{sv} {})], {'is-builtin': <false>})], " +
        "[(0, 0, 1.25, uint32 0, true, [('DP-1', 'DEL', 'DELL U2412M', 'ABC123')], @a{sv} {}), (1536, 0, 1.0, 1, false, [('HDMI-A-1', 'ACR', 'S240HL', 'unknown')], @a{sv} {})], " +
        "{'layout-mode': <uint32 1>})";

    [Fact]
    public void Mutter_reads_a_single_virtual_display()
    {
        var outputs = MutterLayout.Parse(MutterSingle);

        var output = Assert.Single(outputs!);
        Assert.Equal("Virtual-1", output.Name);
        Assert.Equal((0, 0, 1920, 1080), (output.X, output.Y, output.Width, output.Height));
        Assert.Null(output.Vendor);   //"unknown" is not an identity
    }

    [Fact]
    public void Mutter_applies_scale_and_rotation_and_keeps_the_identity_fields()
    {
        var outputs = MutterLayout.Parse(MutterTwo)!;

        Assert.Equal(2, outputs.Count);
        var dell = outputs[0];
        Assert.Equal("DP-1", dell.Name);
        Assert.Equal((0, 0, 1536, 960), (dell.X, dell.Y, dell.Width, dell.Height));   // 1920x1200 / 1.25
        Assert.Equal(("DEL", "DELL U2412M", "ABC123"), (dell.Vendor, dell.Product, dell.Serial));

        var acer = outputs[1];
        Assert.Equal("HDMI-A-1", acer.Name);
        Assert.Equal((1536, 0, 1080, 1920), (acer.X, acer.Y, acer.Width, acer.Height));   // transform 1 = rotated
        Assert.Null(acer.Serial);
    }

    [Fact]
    public void Mutter_physical_layout_mode_does_not_divide_by_scale()
    {
        var physical = MutterTwo.Replace("'layout-mode': <uint32 1>", "'layout-mode': <uint32 2>");
        var dell = MutterLayout.Parse(physical)![0];
        Assert.Equal((1920, 1200), (dell.Width, dell.Height));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Error: GDBus.Error:org.freedesktop.DBus.Error.ServiceUnknown")]
    [InlineData("(uint32 1, [], [], @a{sv} {})")]
    public void Mutter_junk_or_no_monitors_is_null(string text)
    {
        Assert.Null(MutterLayout.Parse(text));
    }

    // ------------------------------------------------------------------ KDE / kscreen-doctor

    const string KScreenJson = """
        {
          "features": 0,
          "outputs": [
            { "connected": true, "enabled": true, "id": 1, "name": "DP-1", "pos": { "x": 0, "y": 0 }, "priority": 1,
              "rotation": 1, "scale": 1.2, "size": { "height": 1440, "width": 2560 }, "type": "DisplayPort" },
            { "connected": true, "enabled": true, "id": 2, "name": "DP-2", "pos": { "x": 2133, "y": 0 }, "priority": 2,
              "rotation": 2, "scale": 1.2, "size": { "height": 1440, "width": 2560 }, "type": "DisplayPort" },
            { "connected": true, "enabled": false, "id": 3, "name": "HDMI-A-1", "pos": { "x": 0, "y": 0 },
              "rotation": 1, "scale": 1, "size": { "height": 1080, "width": 1920 }, "type": "HDMI" },
            { "connected": false, "enabled": false, "id": 4, "name": "DVI-D-1", "pos": { "x": 0, "y": 0 },
              "rotation": 1, "scale": 1, "size": { "height": 0, "width": 0 }, "type": "DVI" }
          ]
        }
        """;

    [Fact]
    public void KScreen_reads_enabled_outputs_with_scale_and_rotation()
    {
        var outputs = KScreenLayout.Parse(KScreenJson)!;

        Assert.Equal(2, outputs.Count);
        Assert.Equal(("DP-1", 0, 0, 2133, 1200), (outputs[0].Name, outputs[0].X, outputs[0].Y, outputs[0].Width, outputs[0].Height));
        Assert.Equal(("DP-2", 2133, 0, 1200, 2133), (outputs[1].Name, outputs[1].X, outputs[1].Y, outputs[1].Width, outputs[1].Height));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"outputs\": []}")]
    public void KScreen_junk_or_no_outputs_is_null(string json)
    {
        Assert.Null(KScreenLayout.Parse(json));
    }

    // ------------------------------------------------------------------ X11 / xrandr

    static string XrandrVerbose(byte[] dellEdid) =>
        "Screen 0: minimum 320 x 200, current 4480 x 1440, maximum 16384 x 16384\n" +
        "DP-0 connected primary 2560x1440+0+0 (0x1c4) normal (normal left inverted right x axis y axis) 597mm x 336mm\n" +
        "\tIdentifier: 0x1b5\n\tTimestamp:  12345\n\tSubpixel:   Unknown\n\tClones:    \n\tCRTC:       0\n" +
        "\tEDID: \n" + string.Concat(Enumerable.Range(0, 8).Select(i => "\t\t" + TestEdid.Hex(dellEdid).Substring(i * 32, 32) + "\n")) +
        "\tnon-desktop: 0 \n\t\tsupported: 0, 1\n" +
        "HDMI-0 connected 1080x1920+2560+0 left (normal left inverted right x axis y axis) 521mm x 293mm\n" +
        "\tIdentifier: 0x1b6\n\tEDID: \n\t\t00ffffffffffff00\n\tnon-desktop: 0 \n" +
        "DVI-D-0 connected (normal left inverted right x axis y axis)\n" +
        "\tIdentifier: 0x1b7\n" +
        "HDMI-1 disconnected (normal left inverted right x axis y axis)\n" +
        "\tIdentifier: 0x1b8\n";

    [Fact]
    public void Xrandr_reads_active_outputs_with_their_edid()
    {
        var dell = TestEdid.Build();
        var outputs = XrandrLayout.Parse(XrandrVerbose(dell))!;

        Assert.Equal(2, outputs.Count);   //the connected-but-disabled DVI and the disconnected HDMI-1 are skipped

        Assert.Equal("DP-0", outputs[0].Name);
        Assert.Equal((0, 0, 2560, 1440), (outputs[0].X, outputs[0].Y, outputs[0].Width, outputs[0].Height));
        Assert.Equal(dell, outputs[0].Edid);

        Assert.Equal("HDMI-0", outputs[1].Name);
        Assert.Equal((2560, 0, 1080, 1920), (outputs[1].X, outputs[1].Y, outputs[1].Width, outputs[1].Height));   //already rotated
        Assert.Null(outputs[1].Edid);   //too short to be an EDID
    }

    [Fact]
    public void Xrandr_with_no_active_output_is_null()
    {
        Assert.Null(XrandrLayout.Parse("Screen 0: minimum 320 x 200\nHDMI-1 disconnected (normal left inverted right x axis y axis)\n"));
    }

    // ------------------------------------------------------------------ pairing

    static readonly byte[] DellEdid = TestEdid.Build("DEL", 0x4AA0, 123456, "DELL U2412M", "ABC123");
    static readonly byte[] AcerEdid = TestEdid.Build("ACR", 0x0332, 0, "S240HL", null);

    static readonly ConnectorInfo DrmDp1 = new("DP-1", "/sys/class/drm/card1-DP-1", DellEdid);
    static readonly ConnectorInfo DrmHdmi1 = new("HDMI-A-1", "/sys/class/drm/card1-HDMI-A-1", AcerEdid);

    [Fact]
    public void Wayland_compositors_pair_by_drm_name()
    {
        var pairs = LayoutJoin.Match([DrmDp1, DrmHdmi1], [new("HDMI-A-1", 1920, 0, 1920, 1080), new("DP-1", 0, 0, 1920, 1200)])!;

        Assert.Equal(2, pairs.Count);
        Assert.Same(DrmHdmi1, pairs[0].Connector);
        Assert.Same(DrmDp1, pairs[1].Connector);
    }

    [Fact]
    public void X11_names_differ_so_outputs_pair_by_edid()
    {
        // NVIDIA's X driver counts from zero: its DP-1 is the kernel's DP-2. Pairing by name would
        // hand DP-1's geometry to the wrong monitor.
        var pairs = LayoutJoin.Match(
            [DrmDp1, DrmHdmi1],
            [new("DP-0", 0, 0, 1920, 1200, Edid: DellEdid), new("HDMI-0", 1920, 0, 1920, 1080, Edid: AcerEdid)])!;

        Assert.Equal(2, pairs.Count);
        Assert.Same(DrmDp1, pairs[0].Connector);
        Assert.Equal(0, pairs[0].Output.X);
        Assert.Same(DrmHdmi1, pairs[1].Connector);
        Assert.Equal(1920, pairs[1].Output.X);
    }

    [Fact]
    public void Mutter_identity_fields_pair_when_the_name_does_not()
    {
        var pairs = LayoutJoin.Match(
            [DrmDp1, DrmHdmi1],
            [new("DisplayPort-0", 0, 0, 1920, 1200, Vendor: "DEL", Product: "DELL U2412M", Serial: "ABC123"),
             new("HDMI-A-0", 1920, 0, 1920, 1080, Vendor: "ACR", Product: "S240HL", Serial: null)])!;

        Assert.Equal(2, pairs.Count);
        Assert.Same(DrmDp1, pairs[0].Connector);
        Assert.Same(DrmHdmi1, pairs[1].Connector);
    }

    [Fact]
    public void A_numeric_serial_pairs_when_the_monitor_has_no_serial_string()
    {
        var pairs = LayoutJoin.Match([DrmHdmi1], [new("X-0", 0, 0, 1, 1, Vendor: "ACR", Product: "S240HL", Serial: "0")]);
        Assert.NotNull(pairs);
    }

    [Fact]
    public void Nothing_pairing_is_reported_as_null_rather_than_an_empty_layout()
    {
        // An X11 session seen through a name-only compositor query: every name misses. The caller
        // must fall back rather than conclude there are no monitors.
        Assert.Null(LayoutJoin.Match([DrmDp1, DrmHdmi1], [new("DP-0", 0, 0, 1, 1), new("HDMI-0", 1, 0, 1, 1)]));
    }

    [Fact]
    public void A_connector_the_compositor_does_not_list_is_left_out()
    {
        var pairs = LayoutJoin.Match([DrmDp1, DrmHdmi1], [new("DP-1", 0, 0, 1, 1)])!;
        Assert.Single(pairs);
        Assert.Same(DrmDp1, pairs[0].Connector);
    }

    [Fact]
    public void Each_connector_pairs_at_most_once()
    {
        var twin = new ConnectorInfo("DP-2", "/sys/class/drm/card1-DP-2", DellEdid);   //identical EDID to DP-1
        var pairs = LayoutJoin.Match(
            [DrmDp1, twin],
            [new("DP-0", 0, 0, 1, 1, Edid: DellEdid), new("DP-1", 1, 0, 1, 1, Edid: DellEdid)])!;

        Assert.Equal(2, pairs.Count);
        Assert.NotSame(pairs[0].Connector, pairs[1].Connector);
    }
}
