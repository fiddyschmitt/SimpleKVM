using SimpleKVM.Rules.Triggers;
using SimpleKVM.Utilities.linux;

namespace SimpleKVM.Tests;

// On GNOME the idle time isn't polled: Mutter announces "idle for a second" and "active
// again", and the state between the two is kept here. The no-longer-idle trigger fires on the
// drop from at least its threshold to less, so that drop has to come out of the announcements.
public class LinuxIdleTests
{
    static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_desktop_is_asked_for_the_same_threshold_the_trigger_uses()
    {
#pragma warning disable CA1416  //a constant of the Linux-only idle provider: the same on whatever OS the tests run
        Assert.Equal(NoLongerIdle.IdleThreshold, LinuxIdle.DesktopIdleThreshold);
#pragma warning restore CA1416
    }

    [Fact]
    public void A_reading_past_the_threshold_starts_idle_and_keeps_growing()
    {
        var state = new IdleWatchState(Second);
        state.Sync(TimeSpan.FromSeconds(40), T0);

        Assert.True(state.IsIdle);
        Assert.Equal(TimeSpan.FromSeconds(40), state.IdleAt(T0));
        Assert.Equal(TimeSpan.FromSeconds(45), state.IdleAt(T0.AddSeconds(5)));
    }

    [Fact]
    public void A_reading_below_the_threshold_starts_active_and_reports_zero()
    {
        var state = new IdleWatchState(Second);
        state.Sync(TimeSpan.FromMilliseconds(300), T0);

        Assert.False(state.IsIdle);
        Assert.Equal(TimeSpan.Zero, state.IdleAt(T0.AddSeconds(30)));    //still typing, as far as anyone has said
    }

    [Fact]
    public void Going_idle_counts_from_the_threshold_and_coming_back_drops_to_zero()
    {
        var state = new IdleWatchState(Second);
        state.Sync(TimeSpan.Zero, T0);

        state.BecameIdle(T0.AddSeconds(10));
        Assert.Equal(Second, state.IdleAt(T0.AddSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(4), state.IdleAt(T0.AddSeconds(13)));

        state.BecameActive();
        Assert.Equal(TimeSpan.Zero, state.IdleAt(T0.AddSeconds(13.1)));
    }

    [Fact]
    public void An_idle_announcement_during_an_idle_spell_does_not_restart_it()
    {
        //Mutter fires a new idle watch straight away when the user is already idle. Taking
        //that as the start of the spell made eight hours of idle drop to one second, which
        //the trigger read as the user coming back.
        var state = new IdleWatchState(Second);
        state.Sync(TimeSpan.FromHours(8), T0);

        state.BecameIdle(T0.AddMilliseconds(5));

        Assert.Equal(TimeSpan.FromHours(8) + TimeSpan.FromSeconds(1), state.IdleAt(T0.AddSeconds(1)));
        Assert.False(NoLongerIdle.ShouldFire(TimeSpan.FromHours(8), state.IdleAt(T0.AddSeconds(1))));
    }

    [Fact]
    public void The_announcements_make_the_trigger_fire_once_per_return()
    {
        var state = new IdleWatchState(Second);
        state.Sync(TimeSpan.Zero, T0);

        int fired = 0;
        TimeSpan? previous = null;
        void Sample(double atSeconds)
        {
            var idle = state.IdleAt(T0.AddSeconds(atSeconds));
            if (NoLongerIdle.ShouldFire(previous, idle)) fired++;
            previous = idle;
        }

        Sample(0.1); Sample(0.2);                       //typing
        state.BecameIdle(T0.AddSeconds(1.2));           //stopped at 0.2 s
        Sample(1.3); Sample(5.0);
        state.BecameActive();                           //back at 5.05 s
        Sample(5.1); Sample(5.2); Sample(5.3);
        state.BecameIdle(T0.AddSeconds(6.3));           //and away again
        Sample(6.4);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void A_reading_that_agrees_is_no_contradiction()
    {
        var slack = TimeSpan.FromSeconds(1.5);
        var state = new IdleWatchState(Second);

        state.Sync(TimeSpan.FromSeconds(20), T0);
        Assert.False(state.Contradicts(TimeSpan.FromSeconds(30.4), T0.AddSeconds(10), slack));

        state.BecameActive();
        Assert.False(state.Contradicts(TimeSpan.FromMilliseconds(200), T0.AddSeconds(11), slack));
        Assert.False(state.Contradicts(TimeSpan.FromSeconds(2), T0.AddSeconds(11), slack));   //the idle announcement may still be in flight
    }

    [Fact]
    public void A_missed_announcement_shows_up_as_a_contradiction()
    {
        var slack = TimeSpan.FromSeconds(1.5);
        var state = new IdleWatchState(Second);

        //Thought active, but the desktop says nobody has touched anything for a minute
        state.Sync(TimeSpan.Zero, T0);
        Assert.True(state.Contradicts(TimeSpan.FromSeconds(60), T0.AddSeconds(60), slack));

        //Thought idle since T0, but the desktop saw input five seconds ago
        state.Sync(TimeSpan.FromSeconds(20), T0);
        Assert.True(state.Contradicts(TimeSpan.FromSeconds(5), T0.AddSeconds(60), slack));

        //Thought idle, but the user is typing right now
        Assert.True(state.Contradicts(TimeSpan.Zero, T0.AddSeconds(60), TimeSpan.FromSeconds(0.5)));
    }
}
