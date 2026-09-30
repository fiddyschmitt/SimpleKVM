using SimpleKVM.Utilities.linux;

namespace SimpleKVM.Tests;

// The D-Bus idle fallback (no input group) reads what `gdbus call` prints. The port's regex
// took the first digits of "(uint64 5432,)", which are the 64 in "uint64", so every
// No-Longer-Idle rule saw a constant 64 ms and never fired.
public class LinuxIdleTests
{
    [Fact]
    public void Mutter_reply_is_milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(5432), LinuxIdle.ParseIdleReply("(uint64 5432,)\n", inSeconds: false));
    }

    [Fact]
    public void A_reply_in_seconds_is_scaled()
    {
        Assert.Equal(TimeSpan.FromSeconds(12), LinuxIdle.ParseIdleReply("(uint32 12,)", inSeconds: true));
    }

    [Fact]
    public void Zero_idle_is_zero_not_the_annotation_width()
    {
        Assert.Equal(TimeSpan.Zero, LinuxIdle.ParseIdleReply("(uint64 0,)", inSeconds: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Error: GDBus.Error:org.freedesktop.DBus.Error.ServiceUnknown: The name is not activatable")]
    [InlineData("('not a number',)")]
    public void Anything_else_is_no_answer(string text)
    {
        Assert.Null(LinuxIdle.ParseIdleReply(text, inSeconds: false));
    }
}
