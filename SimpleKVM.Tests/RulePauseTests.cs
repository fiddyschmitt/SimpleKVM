using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;

namespace SimpleKVM.Tests;

// Rule.Pause and Rule.Edit: rules keep running while the rule editor is open, a step that needs
// them out of the way (recording a hotkey, picking a USB device, the editor's test) stops them
// only for as long as it takes, and every rule comes back the way it was, however the pauses
// overlap and whatever is done to the rule in the meantime.
public class RulePauseTests
{
    // A trigger that counts its starts and can fail to start, the way a hotkey another app has taken does.
    sealed class CountingTrigger : Trigger
    {
        public int Starts { get; private set; }
        public bool Running { get; private set; }
        public bool FailToStart { get; set; }

        public override string GetTriggerAsFriendlyString() => "counting";

        public override void StartMonitoring()
        {
            if (FailToStart) throw new InvalidOperationException("taken by another app");
            Starts++;
            Running = true;
        }

        public override void StopMonitoring() => Running = false;
    }

    static (Rule Rule, CountingTrigger Trigger) RunningRule()
    {
        var trigger = new CountingTrigger();
        var rule = new Rule("r", trigger, []);
        rule.StartMonitoring();
        return (rule, trigger);
    }

    [Fact]
    public void A_paused_rule_is_stopped_until_the_pause_ends()
    {
        var (rule, trigger) = RunningRule();

        var pause = Rule.Pause([rule]);

        Assert.Equal(EnumRuleStatus.Stopped, rule.Status);
        Assert.True(rule.IsPaused);
        Assert.False(trigger.Running);

        pause.Dispose();

        Assert.Equal(EnumRuleStatus.Running, rule.Status);
        Assert.False(rule.IsPaused);
        Assert.True(trigger.Running);
        Assert.Equal(2, trigger.Starts);
    }

    [Fact]
    public void Only_the_rules_given_are_paused()
    {
        // the hotkey chooser pauses the hotkey rules; a No Longer Idle or USB rule keeps running
        var (hotkeyRule, _) = RunningRule();
        var (idleRule, idleTrigger) = RunningRule();

        using (Rule.Pause([hotkeyRule]))
        {
            Assert.Equal(EnumRuleStatus.Stopped, hotkeyRule.Status);
            Assert.Equal(EnumRuleStatus.Running, idleRule.Status);
            Assert.True(idleTrigger.Running);
        }
    }

    [Fact]
    public void Rules_that_are_disabled_or_stopped_are_left_alone()
    {
        var (disabled, disabledTrigger) = RunningRule();
        disabled.Disable();
        var stopped = new Rule("never started", new CountingTrigger(), []);

        using (Rule.Pause([disabled, stopped]))
        {
            Assert.False(disabled.IsPaused);
            Assert.False(stopped.IsPaused);
        }

        Assert.Equal(EnumRuleStatus.Disabled, disabled.Status);
        Assert.Equal(EnumRuleStatus.Stopped, stopped.Status);
        Assert.False(disabledTrigger.Running);
    }

    [Fact]
    public void A_rule_that_failed_to_start_is_tried_again_when_the_pause_ends()
    {
        var trigger = new CountingTrigger { FailToStart = true };
        var rule = new Rule("r", trigger, []);
        rule.StartMonitoring();
        Assert.Equal(EnumRuleStatus.Error, rule.Status);

        var pause = Rule.Pause([rule]);
        Assert.True(rule.IsPaused);

        trigger.FailToStart = false;   // the other app let go of the hotkey
        pause.Dispose();

        Assert.Equal(EnumRuleStatus.Running, rule.Status);
    }

    [Fact]
    public void Overlapping_pauses_hold_the_rule_until_the_last_one_ends()
    {
        // the hotkey chooser opened while the editor's test still shows its result
        var (rule, _) = RunningRule();

        var test = Rule.Pause([rule]);
        var chooser = Rule.Pause([rule]);

        chooser.Dispose();
        Assert.Equal(EnumRuleStatus.Stopped, rule.Status);

        test.Dispose();
        Assert.Equal(EnumRuleStatus.Running, rule.Status);
    }

    [Fact]
    public void Ending_a_pause_twice_does_not_end_a_later_one()
    {
        var (rule, _) = RunningRule();

        var first = Rule.Pause([rule]);
        first.Dispose();
        var second = Rule.Pause([rule]);
        first.Dispose();

        Assert.Equal(EnumRuleStatus.Stopped, rule.Status);

        second.Dispose();
        Assert.Equal(EnumRuleStatus.Running, rule.Status);
    }

    [Fact]
    public void A_rule_deleted_during_a_pause_stays_stopped()
    {
        var (rule, trigger) = RunningRule();

        var pause = Rule.Pause([rule]);
        rule.StopMonitoring();   // what deleting a rule does to it
        pause.Dispose();

        Assert.Equal(EnumRuleStatus.Stopped, rule.Status);
        Assert.False(trigger.Running);
    }

    [Fact]
    public void A_rule_disabled_during_a_pause_stays_disabled()
    {
        var (rule, trigger) = RunningRule();

        var pause = Rule.Pause([rule]);
        rule.Disable();
        pause.Dispose();

        Assert.Equal(EnumRuleStatus.Disabled, rule.Status);
        Assert.False(trigger.Running);
    }

    [Fact]
    public void A_rule_enabled_during_a_pause_is_no_longer_held_by_it()
    {
        var (rule, _) = RunningRule();

        var first = Rule.Pause([rule]);
        rule.Enable();
        Assert.Equal(EnumRuleStatus.Running, rule.Status);
        Assert.False(rule.IsPaused);

        // a second pause holds it now, and the end of the first must not cut that one short
        var second = Rule.Pause([rule]);
        first.Dispose();
        Assert.Equal(EnumRuleStatus.Stopped, rule.Status);

        second.Dispose();
        Assert.Equal(EnumRuleStatus.Running, rule.Status);
    }

    [Fact]
    public void An_edited_rule_moves_from_its_old_trigger_to_its_new_one()
    {
        // the rule kept running on its old trigger while the editor was open
        var (rule, oldTrigger) = RunningRule();
        var newTrigger = new CountingTrigger();

        rule.Edit(() => rule.Trigger = newTrigger);

        Assert.False(oldTrigger.Running);
        Assert.True(newTrigger.Running);
        Assert.Equal(EnumRuleStatus.Running, rule.Status);
    }

    [Fact]
    public void An_edited_disabled_rule_stays_disabled()
    {
        var (rule, _) = RunningRule();
        rule.Disable();
        var newTrigger = new CountingTrigger();

        rule.Edit(() => rule.Trigger = newTrigger);

        Assert.Equal(EnumRuleStatus.Disabled, rule.Status);
        Assert.False(newTrigger.Running);
    }

    [Fact]
    public void A_rule_edited_during_a_pause_starts_on_its_new_trigger_when_the_pause_ends()
    {
        // saved while the editor's test still shows its result
        var (rule, oldTrigger) = RunningRule();
        var pause = Rule.Pause([rule]);
        var newTrigger = new CountingTrigger();

        rule.Edit(() => rule.Trigger = newTrigger);
        Assert.Equal(EnumRuleStatus.Stopped, rule.Status);
        Assert.False(newTrigger.Running);

        pause.Dispose();
        Assert.Equal(EnumRuleStatus.Running, rule.Status);
        Assert.True(newTrigger.Running);
        Assert.False(oldTrigger.Running);
    }

    [Fact]
    public void An_edited_rule_starts_again_when_the_edit_fails()
    {
        var (rule, trigger) = RunningRule();

        Assert.Throws<InvalidOperationException>(() => rule.Edit(() => throw new InvalidOperationException("bad edit")));

        Assert.Equal(EnumRuleStatus.Running, rule.Status);
        Assert.True(trigger.Running);
    }
}
