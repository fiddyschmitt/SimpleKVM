using SimpleKVM.Platform.linux;

namespace SimpleKVM.Tests;

// gdbus prints its results in GVariant text format with type annotations on every
// non-default scalar and on the first element of every array. The Linux backend's GNOME
// and idle paths read that output, so the parser has to take exactly what gdbus emits.
public class GVariantTextTests
{
    [Fact]
    public void Reads_an_annotated_integer_result_tuple()
    {
        // What `gdbus call ... GetIdletime` prints: the regex the port used matched the "64" in "uint64".
        Assert.Equal(5432L, GVariantText.ParseSingleResult("(uint64 5432,)"));
        Assert.Equal(1234L, GVariantText.ParseSingleResult("(uint32 1234,)"));
    }

    [Fact]
    public void Reads_scalars()
    {
        Assert.Equal(true, GVariantText.Parse("true"));
        Assert.Equal(false, GVariantText.Parse("false"));
        Assert.Equal(-7L, GVariantText.Parse("-7"));
        Assert.Equal(31L, GVariantText.Parse("byte 0x1f"));
        Assert.Equal(1.25, GVariantText.Parse("1.25"));
        Assert.Equal(59.950172424316406, GVariantText.Parse("59.950172424316406"));
        Assert.Equal("DP-1", GVariantText.Parse("'DP-1'"));
        Assert.Equal("it's", GVariantText.Parse("\"it's\""));
        Assert.Equal("a\\b'c", GVariantText.Parse(@"'a\\b\'c'"));
        Assert.Equal("/org/gnome/Mutter", GVariantText.Parse("objectpath '/org/gnome/Mutter'"));
        Assert.Null(GVariantText.Parse("@ms nothing"));
    }

    [Fact]
    public void Unwraps_variants_and_drops_type_prefixes()
    {
        Assert.Equal(true, GVariantText.Parse("<true>"));
        Assert.Equal("x", GVariantText.Parse("<'x'>"));
        var empty = Assert.IsType<Dictionary<string, object?>>(GVariantText.Parse("@a{sv} {}"));
        Assert.Empty(empty);
        var emptyArray = Assert.IsType<List<object?>>(GVariantText.Parse("@as []"));
        Assert.Empty(emptyArray);
    }

    [Fact]
    public void Reads_containers()
    {
        var tuple = Assert.IsType<List<object?>>(GVariantText.Parse("(1, 'a', [2, 3], (true,), ())"));
        Assert.Equal(5, tuple.Count);
        Assert.Equal(1L, tuple[0]);
        Assert.Equal("a", tuple[1]);
        Assert.Equal(new List<object?> { 2L, 3L }, tuple[2]);
        Assert.Equal(new List<object?> { true }, tuple[3]);
        Assert.Empty(Assert.IsType<List<object?>>(tuple[4]));

        var dict = Assert.IsType<Dictionary<string, object?>>(GVariantText.Parse("{'is-current': <true>, 'name': <'x'>}"));
        Assert.Equal(true, dict["is-current"]);
        Assert.Equal("x", dict["name"]);
    }

    [Fact]
    public void Only_the_first_array_element_carries_annotations_as_gdbus_prints_them()
    {
        // Two logical monitors: gdbus annotates the uint32 transform of the first only.
        var arr = Assert.IsType<List<object?>>(GVariantText.Parse("[(0, 0, 1.0, uint32 0, true), (1920, 0, 1.0, 0, false)]"));
        var first = Assert.IsType<List<object?>>(arr[0]);
        var second = Assert.IsType<List<object?>>(arr[1]);
        Assert.Equal(0L, first[3]);
        Assert.Equal(1920L, second[0]);
        Assert.Equal(false, second[4]);
    }

    [Fact]
    public void Reads_a_mutter_style_monitor_entry_whose_modes_contain_the_bracket_brace_sequence()
    {
        // The "], {" inside each mode is what stopped the port's lazy regex early.
        const string text = "(('DP-1', 'DEL', 'DELL U2412M', 'ABC'), [('1920x1200@59.950', 1920, 1200, 59.950172424316406, 1.0, [1.0, 1.25], {'is-current': <true>}), ('1920x1080@60.000', 1920, 1080, 60.0, 1.0, [1.0], @a{sv} {})], {'is-builtin': <false>})";
        var monitor = Assert.IsType<List<object?>>(GVariantText.Parse(text));

        var spec = Assert.IsType<List<object?>>(monitor[0]);
        Assert.Equal("DP-1", spec[0]);

        var modes = Assert.IsType<List<object?>>(monitor[1]);
        Assert.Equal(2, modes.Count);
        var current = Assert.IsType<List<object?>>(modes[0]);
        Assert.Equal(1920L, current[1]);
        Assert.Equal(1200L, current[2]);
        var props = Assert.IsType<Dictionary<string, object?>>(current[6]);
        Assert.Equal(true, props["is-current"]);
    }

    [Theory]
    [InlineData("(1,")]
    [InlineData("'unterminated")]
    [InlineData("[1 2]")]
    [InlineData("bogus")]
    public void Rejects_malformed_text(string text)
    {
        Assert.Throws<FormatException>(() => GVariantText.Parse(text));
    }
}
