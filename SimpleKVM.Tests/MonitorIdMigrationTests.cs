using SimpleKVM.Rules;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Rules.Triggers;

namespace SimpleKVM.Tests;

// Rules name their monitors by id. When a platform measures monitors differently from an earlier
// version (macOS before 2.5.0 used points), the rules saved by that version are pointed at the ids
// the same monitors have now, so the upgrade itself can't orphan them.
public class MonitorIdMigrationTests
{
    sealed class TestMonitor(string id) : SimpleKVM.Displays.Monitor(id, "test", [])
    {
        public override int GetCurrentSource() => 0;
        public override bool SetSource(int newSourceId) => false;
    }

    static Rule RuleFor(params (string MonitorId, int Source)[] targets) =>
        new("rule", new HotkeyTrigger("Ctrl+Alt+F1"),
            targets.Select(t => (IAction)new SetMonitorSourceAction(new TestMonitor(t.MonitorId), t.Source)).ToList());

    static IEnumerable<SetMonitorSourceAction> Actions(IEnumerable<Rule> rules) =>
        rules.SelectMany(rule => rule.Actions).OfType<SetMonitorSourceAction>();

    [Fact]
    public void Actions_targeting_an_earlier_id_move_to_the_current_one()
    {
        List<Rule> rules = [RuleFor(("EARLIER-A", 17), ("UNCHANGED-B", 15)), RuleFor(("EARLIER-A", 15))];

        var changes = MonitorIdMigration.Apply(rules, new Dictionary<string, string> { ["EARLIER-A"] = "CURRENT-A" });

        Assert.Equal([("EARLIER-A", "CURRENT-A"), ("EARLIER-A", "CURRENT-A")], changes);
        Assert.Equal(["CURRENT-A", "UNCHANGED-B", "CURRENT-A"], Actions(rules).Select(a => a.Monitor.MonitorUniqueId));
        Assert.Equal([17, 15, 15], Actions(rules).Select(a => a.SetMonitorSourceIdTo));     //the inputs stay as they were
    }

    [Fact]
    public void Nothing_changes_when_no_id_did()
    {
        List<Rule> rules = [RuleFor(("A", 17))];

        Assert.Empty(MonitorIdMigration.Apply(rules, new Dictionary<string, string>()));
        Assert.Empty(MonitorIdMigration.Apply(rules, new Dictionary<string, string> { ["SOMETHING-ELSE"] = "B" }));
        Assert.Equal("A", Actions(rules).Single().Monitor.MonitorUniqueId);
    }

    [Fact]
    public void Running_it_again_changes_nothing_more()
    {
        // It runs at every start, so the second run must find nothing left to do
        List<Rule> rules = [RuleFor(("EARLIER-A", 17))];
        var earlier = new Dictionary<string, string> { ["EARLIER-A"] = "CURRENT-A" };

        MonitorIdMigration.Apply(rules, earlier);

        Assert.Empty(MonitorIdMigration.Apply(rules, earlier));
        Assert.Equal("CURRENT-A", Actions(rules).Single().Monitor.MonitorUniqueId);
    }
}
