using Newtonsoft.Json;
using SimpleKVM;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Rules.Triggers;
using SimpleKVM.USB;

namespace SimpleKVM.Tests;

// Exercises the whole persistence path: TypeNameHandling.Auto (as RuleStore.Save writes it) plus
// DeserializJson (as RuleStore.Load reads it, through SafeSerializationBinder).
public class RuleSerializationTests
{
    static string Save(List<Rule> rules) =>
        JsonConvert.SerializeObject(rules, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

    [Fact]
    public void Hotkey_rule_round_trips_through_save_and_load()
    {
        var original = new List<Rule>
        {
            new("Switch to this computer", new HotkeyTrigger("Win+NumPad1"), [])
            {
                RunCount = 5,
                DelaySeconds = 3,
                Status = EnumRuleStatus.Running,
            }
        };

        var loaded = Save(original).DeserializJson<List<Rule>>();

        var rule = Assert.Single(loaded!);
        Assert.Equal("Switch to this computer", rule.Name);
        Assert.Equal(5, rule.RunCount);
        Assert.Equal(3, rule.DelaySeconds);
        Assert.Equal(EnumRuleStatus.Running, rule.Status);
        var trigger = Assert.IsType<HotkeyTrigger>(rule.Trigger);
        Assert.Equal("Win+NumPad1", trigger.HotkeyAsString);
    }

    [Fact]
    public void Usb_rule_round_trips_the_device_and_event()
    {
        var original = new List<Rule>
        {
            new("Switch on dock", new USBTrigger(new USBDevice("VID_1234&PID_5678&SN_ABC", "IOUSBDevice"), EnumUsbEvent.Inserted), [])
        };

        var loaded = Save(original).DeserializJson<List<Rule>>();

        var trigger = Assert.IsType<USBTrigger>(Assert.Single(loaded!).Trigger);
        Assert.Equal("VID_1234&PID_5678&SN_ABC", trigger.UsbDevice.DeviceID);
        Assert.Equal(EnumUsbEvent.Inserted, trigger.UsbEvent);
    }

    // A real on-disk rules.json (the format users actually have). The Monitor $type names the
    // Windows concrete type; on macOS the binder maps it, so this loads on either platform.
    const string RealRulesJson = """
    [
      {
        "Trigger": {
          "$type": "SimpleKVM.Rules.Triggers.HotkeyTrigger, SimpleKVM",
          "HotkeyAsString": "Win+NumPad1"
        },
        "Actions": [
          {
            "$type": "SimpleKVM.Rules.Actions.SetMonitorSourceAction, SimpleKVM",
            "Monitor": {
              "$type": "SimpleKVM.Displays.win.Monitor, SimpleKVM",
              "MonitorUniqueId": "8E34800754286219FDAB0601FDF57760"
            },
            "SetMonitorSourceIdTo": 17
          }
        ],
        "RunCount": 42,
        "Status": 0,
        "Name": "Switch to this computer",
        "DelaySeconds": 0
      }
    ]
    """;

    [Fact]
    public void A_real_rules_json_file_loads_with_its_monitor_action_intact()
    {
        var loaded = RealRulesJson.DeserializJson<List<Rule>>();

        var rule = Assert.Single(loaded!);
        Assert.Equal("Switch to this computer", rule.Name);
        Assert.Equal(42, rule.RunCount);

        var action = Assert.IsType<SetMonitorSourceAction>(Assert.Single(rule.Actions));
        Assert.Equal("8E34800754286219FDAB0601FDF57760", action.Monitor.MonitorUniqueId);
        Assert.Equal(17, action.SetMonitorSourceIdTo);
    }

    [Fact]
    public void Loading_a_rules_file_naming_a_forbidden_type_throws()
    {
        // A tampered file that tries to instantiate an arbitrary type must be refused, not run.
        const string malicious = """
        [
          {
            "Trigger": { "$type": "System.Diagnostics.Process, System.Diagnostics.Process" },
            "Name": "evil"
          }
        ]
        """;

        Assert.ThrowsAny<JsonException>(() => malicious.DeserializJson<List<Rule>>());
    }
}

public class MonitorDelaySerializationTests
{
    [Fact]
    public void Per_monitor_delay_round_trips_through_rules_json()
    {
        // Written the way RuleStore.Save writes it (a real per-OS Monitor $type), then read back
        // through the SafeSerializationBinder path RuleStore.Load uses.
        const string json = """
        [{
          "Trigger": { "$type": "SimpleKVM.Rules.Triggers.HotkeyTrigger, SimpleKVM", "HotkeyAsString": "Win+F1" },
          "Actions": [{
            "$type": "SimpleKVM.Rules.Actions.SetMonitorSourceAction, SimpleKVM",
            "Monitor": { "$type": "SimpleKVM.Displays.win.Monitor, SimpleKVM", "MonitorUniqueId": "ABC123" },
            "SetMonitorSourceIdTo": 17,
            "DelaySeconds": 5
          }],
          "Name": "r"
        }]
        """;

        var loaded = json.DeserializJson<List<Rule>>();

        var action = Assert.IsType<SetMonitorSourceAction>(Assert.Single(Assert.Single(loaded!).Actions));
        Assert.Equal(5, action.DelaySeconds);
        Assert.Equal(17, action.SetMonitorSourceIdTo);

        // and it survives being written out again
        var rewritten = JsonConvert.SerializeObject(loaded, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        Assert.Contains("\"DelaySeconds\": 5", rewritten.Replace("\"DelaySeconds\":5", "\"DelaySeconds\": 5"));
    }
    [Fact]
    public void A_rules_json_written_before_monitor_delays_existed_loads_with_delay_zero()
    {
        // The pre-2.5 format: no DelaySeconds on the action at all.
        const string legacy = """
        [
          {
            "Trigger": { "$type": "SimpleKVM.Rules.Triggers.HotkeyTrigger, SimpleKVM", "HotkeyAsString": "Win+F1" },
            "Actions": [
              {
                "$type": "SimpleKVM.Rules.Actions.SetMonitorSourceAction, SimpleKVM",
                "Monitor": { "$type": "SimpleKVM.Displays.win.Monitor, SimpleKVM", "MonitorUniqueId": "ABC123" },
                "SetMonitorSourceIdTo": 17
              }
            ],
            "Name": "r"
          }
        ]
        """;

        var loaded = legacy.DeserializJson<List<Rule>>();

        var action = Assert.IsType<SetMonitorSourceAction>(Assert.Single(Assert.Single(loaded!).Actions));
        Assert.Equal(0, action.DelaySeconds);
    }

}
