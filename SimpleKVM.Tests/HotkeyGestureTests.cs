using SimpleKVM.Input;

namespace SimpleKVM.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void Parse_reads_modifiers_and_key()
    {
        var g = HotkeyGesture.Parse("Win+NumPad1");

        Assert.True(g.Win);
        Assert.False(g.Ctrl);
        Assert.False(g.Alt);
        Assert.False(g.Shift);
        Assert.Equal("NumPad1", g.KeyName);
    }

    [Fact]
    public void Parse_reads_multiple_modifiers()
    {
        var g = HotkeyGesture.Parse("Ctrl+Alt+F1");

        Assert.True(g.Ctrl);
        Assert.True(g.Alt);
        Assert.False(g.Win);
        Assert.False(g.Shift);
        Assert.Equal("F1", g.KeyName);
    }

    [Fact]
    public void Parse_allows_a_bare_key_with_no_modifier()
    {
        var g = HotkeyGesture.Parse("F5");

        Assert.False(g.Win || g.Ctrl || g.Alt || g.Shift);
        Assert.Equal("F5", g.KeyName);
    }

    [Theory]
    [InlineData("cmd+D")]      // macOS spelling
    [InlineData("command+D")]
    [InlineData("meta+D")]     // Avalonia's KeyModifiers.Meta
    [InlineData("windows+D")]
    public void Parse_treats_all_command_key_spellings_as_Win(string input)
    {
        Assert.True(HotkeyGesture.Parse(input).Win);
    }

    [Theory]
    [InlineData("control+X", "Ctrl")]
    [InlineData("option+Y", "Alt")] // macOS spelling of Alt
    public void Parse_accepts_alternate_modifier_spellings(string input, string expected)
    {
        var g = HotkeyGesture.Parse(input);
        if (expected == "Ctrl") Assert.True(g.Ctrl);
        if (expected == "Alt") Assert.True(g.Alt);
    }

    [Theory]
    [InlineData("Win+NumPad1")]
    [InlineData("Ctrl+Alt+F1")]
    [InlineData("Ctrl+Shift+A")]
    [InlineData("F5")]
    public void Parse_then_ToString_round_trips_canonical_strings(string canonical)
    {
        Assert.Equal(canonical, HotkeyGesture.Parse(canonical).ToString());
    }

    [Fact]
    public void ToString_emits_modifiers_in_canonical_order_regardless_of_input_order()
    {
        // Input order Alt+Ctrl, canonical order is Win, Ctrl, Alt, Shift.
        Assert.Equal("Ctrl+Alt+F1", HotkeyGesture.Parse("Alt+Ctrl+F1").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_rejects_empty_input(string input)
    {
        Assert.Throws<ArgumentException>(() => HotkeyGesture.Parse(input));
    }

    [Fact]
    public void Parse_rejects_an_unknown_modifier()
    {
        Assert.Throws<ArgumentException>(() => HotkeyGesture.Parse("Hyper+K"));
    }
}
