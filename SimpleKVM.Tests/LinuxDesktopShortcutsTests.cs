using SimpleKVM.Input;
using SimpleKVM.Input.linux;

namespace SimpleKVM.Tests;

// The pure parts of sharing the keyboard with the desktop on Linux: GNOME's shortcut settings as
// gsettings prints and takes them, and key presses as the X server reports them to the hotkey
// chooser's keyboard capture.
public class GnomeKeybindingsTests
{
    static HotkeyGesture Hotkey(string text) => HotkeyGesture.Parse(text);

    [Fact]
    public void A_gsettings_line_is_schema_key_and_value()
    {
        var setting = GnomeKeybindings.ParseLine("org.gnome.shell.extensions.tiling-assistant tile-left-half ['<Super>Left', '<Super>KP_4']");

        Assert.NotNull(setting);
        Assert.Equal("org.gnome.shell.extensions.tiling-assistant", setting.Schema);
        Assert.Equal("tile-left-half", setting.Key);
        Assert.Equal(["<Super>Left", "<Super>KP_4"], setting.Strings);
        Assert.True(setting.IsList);
    }

    [Theory]
    [InlineData("@as []", new string[0])]
    [InlineData("['<Primary><Alt>t']", new[] { "<Primary><Alt>t" })]
    [InlineData("'<Super>e'", new[] { "<Super>e" })]
    [InlineData("[\"<Super>a\", '<Super>b']", new[] { "<Super>a", "<Super>b" })]
    [InlineData("['it\\'s']", new[] { "it's" })]
    [InlineData("true", new string[0])]
    [InlineData("uint32 300", new string[0])]
    public void Values_give_their_strings(string value, string[] expected)
    {
        Assert.Equal(expected, GnomeKeybindings.Strings(value));
    }

    [Theory]
    [InlineData("<Super>KP_4", "Win+NumPad4")]
    [InlineData("<Primary><Alt>Left", "Ctrl+Alt+Left")]
    [InlineData("<Control><Alt>Left", "Ctrl+Alt+Left")]
    [InlineData("<Ctrl><Alt>Left", "Ctrl+Alt+Left")]
    [InlineData("<Shift><Super>Page_Up", "Win+Shift+PageUp")]
    [InlineData("<Super>Prior", "Win+PageUp")]
    [InlineData("<Super>a", "Win+A")]
    [InlineData("<Super>A", "Win+A")]
    [InlineData("<Super>1", "Win+D1")]
    [InlineData("<Mod4>KP_4", "Win+NumPad4")]
    [InlineData("<Alt>ISO_Left_Tab", "Alt+Shift+Tab")]
    [InlineData("<Super><Release>space", "Win+Space")]
    public void An_accelerator_is_the_hotkey_with_the_same_modifiers_and_key(string accelerator, string hotkey)
    {
        Assert.True(GnomeKeybindings.Matches(accelerator, Hotkey(hotkey)));
    }

    [Theory]
    [InlineData("<Super>KP_4", "Win+Shift+NumPad4")]     //a modifier more
    [InlineData("<Super><Shift>KP_4", "Win+NumPad4")]    //a modifier fewer
    [InlineData("<Super>KP_4", "Win+NumPad5")]
    [InlineData("<Super>KP_4", "Win+D4")]                //the number row is another key
    [InlineData("<Super>Left", "Win+NumPad4")]
    [InlineData("<Hyper>KP_4", "NumPad4")]               //a modifier a hotkey can't have
    [InlineData("", "Win+NumPad4")]
    [InlineData("disabled", "Win+NumPad4")]
    public void Anything_else_is_another_combination(string accelerator, string hotkey)
    {
        Assert.False(GnomeKeybindings.Matches(accelerator, Hotkey(hotkey)));
    }

    [Fact]
    public void Freeing_keeps_the_shortcuts_other_keys()
    {
        var setting = GnomeKeybindings.ParseLine("org.gnome.shell.extensions.tiling-assistant tile-left-half ['<Super>Left', '<Super>KP_4']")!;

        Assert.Equal("['<Super>Left']", GnomeKeybindings.Without(setting, Hotkey("Win+NumPad4")));
    }

    [Fact]
    public void Freeing_its_only_key_leaves_an_empty_list_gsettings_can_type()
    {
        var setting = GnomeKeybindings.ParseLine("org.gnome.shell.extensions.tiling-assistant tile-bottom-half ['<Super>KP_2']")!;

        Assert.Equal("@as []", GnomeKeybindings.Without(setting, Hotkey("Win+NumPad2")));
    }

    [Fact]
    public void Freeing_a_single_accelerator_setting_empties_the_string()
    {
        var setting = GnomeKeybindings.ParseLine("org.gnome.settings-daemon.plugins.media-keys.custom-keybinding binding '<Super>KP_4'")!;

        Assert.False(setting.IsList);
        Assert.Equal("''", GnomeKeybindings.Without(setting, Hotkey("Win+NumPad4")));
    }

    [Fact]
    public void Kept_keys_are_quoted_back_with_their_escapes()
    {
        var setting = GnomeKeybindings.ParseLine("some.schema key ['<Super>KP_4', '<Super>apostrophe', 'it\\'s']")!;

        Assert.Equal("['<Super>apostrophe', 'it\\'s']", GnomeKeybindings.Without(setting, Hotkey("Win+NumPad4")));
    }

    [Theory]
    [InlineData("tile-left-half", "Tile left half")]
    [InlineData("switch-to-workspace-left", "Switch to workspace left")]
    [InlineData("screensaver", "Screensaver")]
    public void Setting_keys_read_as_words(string key, string words)
    {
        Assert.Equal(words, GnomeKeybindings.Humanize(key));
    }
}

public class X11KeyDecoderTests
{
    const ushort Shift = X11KeyDecoder.ShiftMask, Ctrl = X11KeyDecoder.ControlMask, Alt = X11KeyDecoder.Mod1Mask,
                 NumLock = X11KeyDecoder.Mod2Mask, Super = X11KeyDecoder.Mod4Mask, CapsLock = 2;

    // The keypad's 4: KP_Left unshifted, KP_4 on the second level
    const uint KpLeft = 0xff96, Kp4 = 0xffb4;

    [Fact]
    public void The_keypad_types_its_digit_while_num_lock_is_on()
    {
        Assert.Equal("Win+NumPad4", X11KeyDecoder.Combination(KpLeft, Kp4, (ushort)(Super | NumLock)));
    }

    [Fact]
    public void Without_num_lock_the_keypad_is_the_arrow_avalonia_would_name()
    {
        Assert.Equal("Win+Left", X11KeyDecoder.Combination(KpLeft, Kp4, Super));
    }

    [Fact]
    public void Shift_with_num_lock_turns_the_keypad_back_into_arrows()
    {
        Assert.Equal("Win+Shift+Left", X11KeyDecoder.Combination(KpLeft, Kp4, (ushort)(Super | Shift | NumLock)));
    }

    [Fact]
    public void Shift_is_a_modifier_not_another_key()
    {
        Assert.Equal("Ctrl+Shift+D1", X11KeyDecoder.Combination('1', '!', (ushort)(Ctrl | Shift)));
        Assert.Equal("Win+Shift+A", X11KeyDecoder.Combination('a', 'A', (ushort)(Super | Shift)));
    }

    [Fact]
    public void Modifiers_come_in_the_choosers_order_and_lock_keys_are_left_out()
    {
        Assert.Equal("Win+Ctrl+Alt+Shift+F9", X11KeyDecoder.Combination(0xffc6, 0xffc6, (ushort)(Shift | Alt | Ctrl | Super | NumLock | CapsLock)));
    }

    [Fact]
    public void A_key_without_modifiers_is_still_reported()
    {
        Assert.Equal("Escape", X11KeyDecoder.Combination(0xff1b, 0, 0));
    }

    [Fact]
    public void The_modifier_keys_themselves_are_not_hotkey_keys()
    {
        Assert.Null(X11KeyDecoder.Combination(0xffeb, 0, Super));       //Super_L
        Assert.Null(X11KeyDecoder.Combination(0xffe3, 0, Ctrl));        //Control_L
    }

    [Fact]
    public void An_upper_case_letter_is_the_same_key()
    {
        Assert.Equal("Win+Q", X11KeyDecoder.Combination('Q', 'Q', Super));
    }
}
