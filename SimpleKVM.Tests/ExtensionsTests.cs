using SimpleKVM;
using SimpleKVM.Rules;
using SimpleKVM.USB;

namespace SimpleKVM.Tests;

public class ExtensionsTests
{
    [Theory]
    [InlineData(0, "0 minutes")]           // TimeSpan.Zero
    [InlineData(30, "0 minutes")]          // under a minute rounds down to nothing
    [InlineData(60, "1 minute")]           // singular
    [InlineData(120, "2 minutes")]         // plural
    [InlineData(3600, "1 hour")]
    [InlineData(5400, "1 hour 30 minutes")]
    [InlineData(86400, "1 day")]
    [InlineData(90000, "1 day 1 hour")]    // 25 hours
    public void ToPrettyFormat_formats_and_pluralises(int totalSeconds, string expected)
    {
        Assert.Equal(expected, TimeSpan.FromSeconds(totalSeconds).ToPrettyFormat());
    }

    [Fact]
    public void WriteTextFile_replaces_the_file_in_place_and_leaves_no_temporary_behind()
    {
        var path = Path.Combine(Path.GetTempPath(), $"simplekvm-{Guid.NewGuid():N}.json");
        try
        {
            Extensions.WriteTextFile(path, "one");
            Extensions.WriteTextFile(path, "two");

            Assert.Equal("two", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Next_advances_through_enum_values()
    {
        Assert.Equal(EnumRuleStatus.Stopped, EnumRuleStatus.Running.Next());
        Assert.Equal(EnumRuleStatus.Error, EnumRuleStatus.Stopped.Next());
    }

    [Fact]
    public void Next_wraps_around_at_the_end()
    {
        Assert.Equal(EnumRuleStatus.Running, EnumRuleStatus.Disabled.Next());
        // The USB trigger UI toggles the verb with this; it must flip Inserted <-> Removed.
        Assert.Equal(EnumUsbEvent.Removed, EnumUsbEvent.Inserted.Next());
        Assert.Equal(EnumUsbEvent.Inserted, EnumUsbEvent.Removed.Next());
    }
}
