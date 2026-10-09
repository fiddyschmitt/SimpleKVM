using SimpleKVM.Input;
using SimpleKVM.Input.linux;
using SimpleKVM.Platform.linux;

namespace SimpleKVM.Tests;

// How the app presents itself to a Linux desktop: the .desktop files it writes, the session
// it finds itself in, and the names it gives keys and shortcuts when the desktop, not the app,
// is the one watching the keyboard.

public class DesktopEntryFileTests
{
    const string Exe = "/home/me/.local/bin/simplekvm";

    [Fact]
    public void The_launcher_names_the_program_the_icon_and_the_window_class()
    {
        var entry = DesktopEntry.Launcher(Exe, shown: true);

        Assert.StartsWith("[Desktop Entry]\n", entry);
        Assert.Contains("\nType=Application\n", entry);
        Assert.Contains("\nName=Simple KVM\n", entry);
        Assert.Contains($"\nExec=\"{Exe}\"\n", entry);
        Assert.Contains($"\nIcon={DesktopEntry.AppId}\n", entry);
        Assert.Contains("\nStartupWMClass=SimpleKVM\n", entry);    //the class Avalonia gives its X11 windows
        Assert.Contains("\nNoDisplay=false\n", entry);
    }

    [Fact]
    public void The_launcher_offers_quit_as_a_right_click_action()
    {
        var entry = DesktopEntry.Launcher(Exe, shown: true);

        Assert.Contains("\nActions=quit;\n", entry);
        Assert.Contains("\n[Desktop Action quit]\n", entry);
        Assert.Contains($"\nExec=\"{Exe}\" --quit\n", entry);
    }

    [Fact]
    public void A_hidden_launcher_is_the_same_entry_kept_out_of_the_menu()
    {
        var hidden = DesktopEntry.Launcher(Exe, shown: false);

        Assert.Contains("\nNoDisplay=true\n", hidden);
        Assert.Equal(DesktopEntry.Launcher(Exe, shown: true), hidden.Replace("NoDisplay=true", "NoDisplay=false"));
    }

    [Fact]
    public void Entries_are_written_with_unix_line_endings_whatever_built_them()
    {
        Assert.DoesNotContain('\r', DesktopEntry.Launcher(Exe, shown: true));
        Assert.DoesNotContain('\r', DesktopEntry.Autostart(Exe, "--minimized"));
    }

    [Fact]
    public void The_autostart_entry_starts_the_program_with_its_arguments()
    {
        var entry = DesktopEntry.Autostart("/opt/My Apps/simplekvm", "--minimized");

        Assert.Contains("\nExec=\"/opt/My Apps/simplekvm\" --minimized\n", entry);
        Assert.Contains("\nX-GNOME-Autostart-enabled=true\n", entry);
    }

    [Fact]
    public void The_application_id_is_one_the_portals_accept()
    {
        //GNOME discards shortcut requests from an id that isn't reverse-DNS; and the user asked for lower case
        Assert.Contains('.', DesktopEntry.AppId);
        Assert.DoesNotMatch("[A-Z]", DesktopEntry.AppId);
        Assert.Equal(DesktopEntry.AppId + ".desktop", DesktopEntry.FileName);
    }

    [Fact]
    public void ReadKey_finds_a_key_of_the_main_group_only()
    {
        var entry = DesktopEntry.Launcher(Exe, shown: false);

        Assert.Equal("true", DesktopEntry.ReadKey(entry, "NoDisplay"));
        Assert.Equal(Exe, DesktopEntry.ReadKey(entry, DesktopEntry.ExecutableKey));
        Assert.Equal("Simple KVM", DesktopEntry.ReadKey(entry, "Name"));    //not the quit action's "Quit Simple KVM"
        Assert.Equal($"\"{Exe}\"", DesktopEntry.ReadKey(entry, "Exec"));    //nor its Exec
        Assert.Null(DesktopEntry.ReadKey(entry, "Hidden"));
    }

    [Fact]
    public void ReadKey_copes_with_windows_line_endings_and_other_peoples_files()
    {
        Assert.Equal("true", DesktopEntry.ReadKey("[Desktop Entry]\r\nNoDisplay=true\r\n", "NoDisplay"));
        Assert.Null(DesktopEntry.ReadKey("", "NoDisplay"));
        Assert.Null(DesktopEntry.ReadKey("[Other]\nNoDisplay=true\n", "NoDisplay"));
    }
}

public class LinuxSessionTests
{
    static Func<string, string?> Env(params (string Key, string Value)[] variables)
    {
        var map = variables.ToDictionary(v => v.Key, v => v.Value);
        return key => map.GetValueOrDefault(key);
    }

    [Fact]
    public void The_session_type_decides_when_the_session_states_it()
    {
        Assert.True(LinuxSession.IsX11Session(Env(("XDG_SESSION_TYPE", "x11"), ("DISPLAY", ":0"))));

        //A Wayland session has a DISPLAY too (XWayland), which must not make it an X session
        Assert.False(LinuxSession.IsX11Session(Env(("XDG_SESSION_TYPE", "wayland"), ("DISPLAY", ":0"), ("WAYLAND_DISPLAY", "wayland-0"))));
        Assert.False(LinuxSession.IsX11Session(Env(("XDG_SESSION_TYPE", "wayland"), ("DISPLAY", ":0"))));
    }

    [Fact]
    public void Without_a_stated_type_a_display_and_no_compositor_socket_means_x11()
    {
        Assert.True(LinuxSession.IsX11Session(Env(("DISPLAY", ":0"))));
        Assert.True(LinuxSession.IsX11Session(Env(("XDG_SESSION_TYPE", "tty"), ("DISPLAY", ":1"))));    //startx from a console
        Assert.False(LinuxSession.IsX11Session(Env(("DISPLAY", ":0"), ("WAYLAND_DISPLAY", "wayland-1"))));
        Assert.False(LinuxSession.IsX11Session(Env()));    //a plain SSH shell
    }
}

public class LinuxKeysymTests
{
    [Theory]
    [InlineData("A", 0x61u, "a")]               //the unshifted symbol, which is what the X keyboard mapping lists first
    [InlineData("z", 0x7au, "z")]
    [InlineData("D1", 0x31u, "1")]
    [InlineData("F1", 0xffbeu, "F1")]
    [InlineData("F12", 0xffc9u, "F12")]
    [InlineData("F24", 0xffd5u, "F24")]
    [InlineData("NumPad1", 0xffb1u, "KP_1")]
    [InlineData("Return", 0xff0du, "Return")]
    [InlineData("Enter", 0xff0du, "Return")]
    [InlineData("PageUp", 0xff55u, "Prior")]
    [InlineData("OemMinus", 0x2du, "minus")]
    [InlineData("VolumeUp", 0x1008ff13u, "XF86AudioRaiseVolume")]
    public void Keys_have_the_x_keysym_they_type(string keyName, uint keysym, string name)
    {
        Assert.True(LinuxKeyCodes.TryGetKeysym(keyName, out var actual, out var actualName));
        Assert.Equal((keysym, name), (actual, actualName));
    }

    [Fact]
    public void A_key_without_a_keysym_of_its_own_says_so()
    {
        Assert.False(LinuxKeyCodes.TryGetKeysym("BrowserStop", out _, out _));   //grabbed by its position instead
        Assert.False(LinuxKeyCodes.TryGetKeysym("NoSuchKey", out _, out _));
    }

    [Theory]
    [InlineData("A")] [InlineData("D0")] [InlineData("F13")] [InlineData("NumPad9")] [InlineData("Multiply")]
    [InlineData("Space")] [InlineData("Oemtilde")] [InlineData("MediaPlayPause")] [InlineData("Scroll")]
    public void Every_key_with_a_keysym_also_has_a_kernel_code(string keyName)
    {
        //The X11 backend falls back on the kernel code when the layout lacks the symbol, and
        //all three backends must accept the same set of keys
        Assert.True(LinuxKeyCodes.TryGetKeysym(keyName, out _, out _));
        Assert.True(LinuxKeyCodes.TryGet(keyName, out _));
    }

    [Theory]
    [InlineData("Ctrl+Alt+F1", "CTRL+ALT+F1")]
    [InlineData("Ctrl+Shift+F9", "CTRL+SHIFT+F9")]
    [InlineData("Win+NumPad1", "LOGO+KP_1")]
    [InlineData("Alt+Ctrl+a", "CTRL+ALT+a")]
    [InlineData("Ctrl+Alt+Shift+Win+OemMinus", "CTRL+ALT+SHIFT+LOGO+minus")]
    public void A_gesture_becomes_an_xdg_shortcut_trigger(string gesture, string trigger)
    {
        Assert.Equal(trigger, LinuxKeyCodes.ToShortcutTrigger(HotkeyGesture.Parse(gesture)));
    }

    [Fact]
    public void A_gesture_on_a_key_without_a_keysym_proposes_no_trigger()
    {
        //The desktop then asks the user to choose the keys
        Assert.Null(LinuxKeyCodes.ToShortcutTrigger(HotkeyGesture.Parse("Ctrl+BrowserStop")));
    }
}

//PortalHotkeys is Linux-only for what it talks to; ShortcutId is plain string work that runs on any OS
#pragma warning disable CA1416
public class PortalShortcutIdTests
{
    [Theory]
    [InlineData("Ctrl+Alt+F1", "ctrl-alt-f1")]
    [InlineData("Win+NumPad1", "win-numpad1")]
    [InlineData("Ctrl+Shift+OemMinus", "ctrl-shift-oemminus")]
    public void A_shortcut_is_named_after_its_key_combination(string gesture, string id)
    {
        Assert.Equal(id, PortalHotkeys.ShortcutId(HotkeyGesture.Parse(gesture)));
    }

    [Fact]
    public void The_name_does_not_depend_on_how_the_combination_was_written()
    {
        //The desktop remembers what the user agreed to under this name, so it has to come out
        //the same for the same keys however the rule spells them
        Assert.Equal(PortalHotkeys.ShortcutId(HotkeyGesture.Parse("Ctrl+Alt+F1")),
                     PortalHotkeys.ShortcutId(HotkeyGesture.Parse("Alt + Control + F1")));
    }

    [Fact]
    public void A_different_combination_is_a_different_shortcut()
    {
        //Desktops keep the keys a shortcut already has, so new keys under an old name would be ignored
        Assert.NotEqual(PortalHotkeys.ShortcutId(HotkeyGesture.Parse("Ctrl+Alt+F1")),
                        PortalHotkeys.ShortcutId(HotkeyGesture.Parse("Ctrl+Alt+F2")));
    }
}
#pragma warning restore CA1416
