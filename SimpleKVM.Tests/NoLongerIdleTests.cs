using SimpleKVM.Rules.Triggers;

namespace SimpleKVM.Tests;

// The trigger samples the system idle time every 100 ms. It must fire once when the user comes
// back after being away, not on every keypress and mouse movement of ordinary use.
public class NoLongerIdleTests
{
    [Fact]
    public void Fires_when_input_arrives_after_the_user_was_idle()
    {
        Assert.True(NoLongerIdle.ShouldFire(previousIdleTime: TimeSpan.FromMinutes(10), idleTime: TimeSpan.FromMilliseconds(40)));
    }

    [Fact]
    public void Does_not_fire_for_input_during_ordinary_use()
    {
        // Two samples 100 ms apart while the user is typing: the idle time drops, but the user was never idle.
        Assert.False(NoLongerIdle.ShouldFire(previousIdleTime: TimeSpan.FromMilliseconds(70), idleTime: TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public void Does_not_fire_while_the_idle_time_keeps_growing()
    {
        Assert.False(NoLongerIdle.ShouldFire(previousIdleTime: TimeSpan.FromSeconds(5), idleTime: TimeSpan.FromSeconds(5.1)));
    }

    [Fact]
    public void Does_not_fire_on_the_first_sample()
    {
        Assert.False(NoLongerIdle.ShouldFire(previousIdleTime: null, idleTime: TimeSpan.Zero));
    }

    [Fact]
    public void Threshold_is_one_second()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), NoLongerIdle.IdleThreshold);
        Assert.True(NoLongerIdle.ShouldFire(TimeSpan.FromSeconds(1), TimeSpan.Zero));
        Assert.False(NoLongerIdle.ShouldFire(TimeSpan.FromMilliseconds(999), TimeSpan.Zero));
    }
}
