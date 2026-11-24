using SimpleKVM.Input.linux;
using SimpleKVM.Platform.linux;
using SimpleKVM.USB.linux;

namespace SimpleKVM.Tests;

// The Linux backend's small pure parts: the USB sysfs snapshot and diff, the .desktop Exec
// escaping, the evdev device filter, and the key-name table.
public class UsbSysfsTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), $"simplekvm-usb-{Guid.NewGuid():N}");

    public UsbSysfsTests()
    {
        // A slice of a real /sys/bus/usb/devices: a root hub, a hub, a keyboard with a serial,
        // a mouse without one, and the keyboard's interface node.
        Device("usb1", vid: "1d6b", pid: "0002", serial: "0000:00:14.0");
        Device("1-7", vid: "05e3", pid: "0610", serial: null);
        Device("1-7.2", vid: "046d", pid: "c52b", serial: "ABC123");
        Device("1-7.3", vid: "1532", pid: "0067", serial: null);

        // The interface node's name carries a colon, which Windows paths can't hold; the
        // skip rule is exercised where the fixture can be built.
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(Path.Combine(root, "1-7.2:1.0"));
            File.WriteAllText(Path.Combine(root, "1-7.2:1.0", "bInterfaceClass"), "03\n");
        }
    }

    void Device(string port, string vid, string pid, string? serial)
    {
        var dir = Path.Combine(root, port);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "idVendor"), vid + "\n");
        File.WriteAllText(Path.Combine(dir, "idProduct"), pid + "\n");
        if (serial != null) File.WriteAllText(Path.Combine(dir, "serial"), serial + "\n");
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void Snapshot_lists_devices_but_not_root_hubs_or_interfaces()
    {
        var snapshot = UsbSysfs.Snapshot(root);

        Assert.Equal(3, snapshot.Count);
        Assert.Equal("VID_046D&PID_C52B&SN_ABC123", snapshot["1-7.2"]);
        Assert.Equal("VID_1532&PID_0067&SN_PORT1-7.3", snapshot["1-7.3"]);   //no serial: the port stands in
        Assert.Equal("VID_05E3&PID_0610&SN_PORT1-7", snapshot["1-7"]);
        Assert.False(snapshot.ContainsKey("usb1"));
        Assert.False(snapshot.ContainsKey("1-7.2:1.0"));
    }

    [Fact]
    public void A_missing_tree_is_an_empty_snapshot()
    {
        Assert.Empty(UsbSysfs.Snapshot(Path.Combine(root, "nope")));
    }

    [Fact]
    public void Diff_reports_inserts_removes_and_a_swapped_device_as_both()
    {
        var before = new Dictionary<string, string> { ["1-1"] = "A", ["1-2"] = "B", ["1-3"] = "C" };
        var after = new Dictionary<string, string> { ["1-1"] = "A", ["1-2"] = "B2", ["1-4"] = "D" };

        var (inserted, removed) = UsbSysfs.Diff(before, after);

        Assert.Equal(new[] { "B2", "D" }, inserted.Order());
        Assert.Equal(new[] { "B", "C" }, removed.Order());
    }
}

public class DesktopEntryTests
{
    [Fact]
    public void A_plain_path_is_just_quoted()
    {
        Assert.Equal("\"/opt/simplekvm/SimpleKVM\"", DesktopEntry.QuoteExecArgument("/opt/simplekvm/SimpleKVM"));
    }

    [Fact]
    public void Spaces_are_fine_inside_the_quotes()
    {
        Assert.Equal("\"/home/me/My Apps/SimpleKVM\"", DesktopEntry.QuoteExecArgument("/home/me/My Apps/SimpleKVM"));
    }

    [Fact]
    public void Reserved_characters_get_both_layers_of_escaping()
    {
        // Per the Desktop Entry spec, a literal backslash in an argument ends up as four, a
        // double quote as backslash-backslash-quote, a dollar likewise, and a percent as two.
        Assert.Equal("\"a\\\\\\\\b\"", DesktopEntry.QuoteExecArgument("a\\b"));
        Assert.Equal("\"say \\\\\"hi\\\\\"\"", DesktopEntry.QuoteExecArgument("say \"hi\""));
        Assert.Equal("\"\\\\$HOME\"", DesktopEntry.QuoteExecArgument("$HOME"));
        Assert.Equal("\"100%%\"", DesktopEntry.QuoteExecArgument("100%"));
    }
}

public class EvdevDeviceFilterTests
{
    [Fact]
    public void Keyboards_mice_and_touchpads_are_wanted()
    {
        Assert.True(EvdevDeviceFilter.IsWanted(eventCapabilities: 0x120013, properties: 0));      //typical keyboard: SYN KEY MSC LED REP
        Assert.True(EvdevDeviceFilter.IsWanted(eventCapabilities: 0x17, properties: 0));          //mouse: SYN KEY REL MSC
        Assert.True(EvdevDeviceFilter.IsWanted(eventCapabilities: 0x1b, properties: 0x5));        //touchpad: SYN KEY ABS MSC, POINTER|BUTTONPAD
    }

    [Fact]
    public void Accelerometers_and_keyless_axisless_devices_are_not()
    {
        Assert.False(EvdevDeviceFilter.IsWanted(eventCapabilities: 0x9, properties: 0x40));       //accelerometer: SYN ABS with INPUT_PROP_ACCELEROMETER
        Assert.False(EvdevDeviceFilter.IsWanted(eventCapabilities: 0x21, properties: 0));         //lid switch: SYN SW
        Assert.False(EvdevDeviceFilter.IsWanted(eventCapabilities: 0x1, properties: 0));          //SYN only
    }

    [Fact]
    public void Sysfs_bitmasks_are_space_separated_hex_words_low_word_last()
    {
        Assert.Equal(0x120013UL, EvdevDeviceFilter.ParseBitmask("120013\n"));
        Assert.Equal(0x3UL, EvdevDeviceFilter.ParseBitmask("1 3"));   //a wide mask: only the low word matters
        Assert.Null(EvdevDeviceFilter.ParseBitmask("   "));
    }
}

public class LinuxKeyCodesTests
{
    [Theory]
    [InlineData("A", 30)]
    [InlineData("D1", 2)]
    [InlineData("F1", 59)]
    [InlineData("F12", 88)]
    [InlineData("F13", 183)]
    [InlineData("NumPad1", 79)]
    [InlineData("Oem4", 26)]            //the names Avalonia's Key.ToString() emits on the chooser
    [InlineData("OemBackslash", 86)]
    [InlineData("PrintScreen", 99)]
    [InlineData("VolumeUp", 115)]
    [InlineData("MediaPlayPause", 164)]
    public void Maps_stored_key_names_to_input_event_codes(string name, int code)
    {
        Assert.True(LinuxKeyCodes.TryGet(name, out var actual));
        Assert.Equal(code, actual);
    }

    [Fact]
    public void Lookup_ignores_case_like_the_other_platforms()
    {
        Assert.True(LinuxKeyCodes.TryGet("numpad1", out var code));
        Assert.Equal(79, code);
    }

    [Fact]
    public void Unknown_names_are_rejected_not_guessed()
    {
        Assert.False(LinuxKeyCodes.TryGet("Hyper", out _));
    }

    [Fact]
    public void Modifiers_are_recognised_on_both_sides_of_the_keyboard()
    {
        Assert.True(LinuxKeyCodes.IsModifier(LinuxKeyCodes.LeftCtrl));
        Assert.True(LinuxKeyCodes.IsModifier(LinuxKeyCodes.RightMeta));
        Assert.False(LinuxKeyCodes.IsModifier(30));
    }
}
