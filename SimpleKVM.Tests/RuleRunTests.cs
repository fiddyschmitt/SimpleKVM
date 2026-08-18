using System.Diagnostics;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Rules.Triggers;

namespace SimpleKVM.Tests;

// Rule.Run's timing contract: the rule delay comes first, then every action fires as its own
// offset from that moment - per-action delays are parallel, not chained.
public class RuleRunTests
{
    // A trigger that never fires; the tests call Run() directly.
    sealed class NoTrigger : Trigger
    {
        public override string GetTriggerAsFriendlyString() => "never";
        public override void StartMonitoring() { }
        public override void StopMonitoring() { }
    }

    // A stand-in action that records when it ran, optionally sleeping first like a monitor delay would.
    sealed class RecordingAction(int delayMs, bool result = true) : IAction
    {
        public double? RanAtMs { get; private set; }
        readonly Stopwatch clock = Stopwatch.StartNew();

        public bool Run()
        {
            if (delayMs > 0) Thread.Sleep(delayMs);
            RanAtMs = clock.Elapsed.TotalMilliseconds;
            return result;
        }
    }

    [Fact]
    public void Actions_run_and_run_count_increments_when_any_action_ran()
    {
        var a = new RecordingAction(0);
        var b = new RecordingAction(0);
        var rule = new Rule("r", new NoTrigger(), [a, b]);

        rule.Run();

        Assert.NotNull(a.RanAtMs);
        Assert.NotNull(b.RanAtMs);
        Assert.Equal(1, rule.RunCount);
        Assert.NotNull(rule.LastRun);
    }

    [Fact]
    public void Run_count_does_not_increment_when_no_action_did_anything()
    {
        // e.g. every monitor was "Leave unchanged"
        var rule = new Rule("r", new NoTrigger(), [new RecordingAction(0, result: false)]);

        rule.Run();

        Assert.Equal(0, rule.RunCount);
        Assert.Null(rule.LastRun);
    }

    [Fact]
    public void Per_action_delays_are_offsets_from_the_rule_firing_not_a_chain()
    {
        // Two delayed actions of 400 ms each. Chained, the second would land at ~800 ms;
        // as parallel offsets both land at ~400 ms and the whole run takes ~400 ms.
        var a = new RecordingAction(400);
        var b = new RecordingAction(400);
        var rule = new Rule("r", new NoTrigger(), [a, b]);

        var total = Stopwatch.StartNew();
        rule.Run();
        total.Stop();

        Assert.InRange(total.ElapsedMilliseconds, 350, 700);   // not ~800+
        Assert.InRange(a.RanAtMs!.Value, 350, 700);
        Assert.InRange(b.RanAtMs!.Value, 350, 700);
    }

    [Fact]
    public void An_undelayed_action_is_not_held_up_by_a_delayed_one()
    {
        var fast = new RecordingAction(0);
        var slow = new RecordingAction(400);
        var rule = new Rule("r", new NoTrigger(), [slow, fast]);   // slow first in the list on purpose

        rule.Run();

        Assert.InRange(fast.RanAtMs!.Value, 0, 150);
        Assert.InRange(slow.RanAtMs!.Value, 350, 700);
    }

    [Fact]
    public void Run_waits_for_every_action_before_counting_the_run()
    {
        var slow = new RecordingAction(300);
        var rule = new Rule("r", new NoTrigger(), [slow]);

        rule.Run();

        // If Run returned before the delayed action finished, RanAtMs would still be null here.
        Assert.NotNull(slow.RanAtMs);
        Assert.Equal(1, rule.RunCount);
    }
}
