using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SimpleKVM.Rules.Triggers
{
    public class HotkeyTrigger : Trigger
    {
        public HotkeyTrigger(string hotkeyAsString)
        {
            HotkeyAsString = hotkeyAsString;
        }

        public string HotkeyAsString { get; }

        public override string GetTriggerAsFriendlyString()
        {
            var result = $"whenever {HotkeyAsString} is pressed";
            return result;
        }

        public override void StartMonitoring()
        {
            var converter = new KeysConverter();
            var keys = (Keys)converter.ConvertFromString(HotkeyAsString);

            hotkey = new Hotkey(keys, () =>
            {
                RaiseTriggered();
            });
        }

        Hotkey? hotkey;

        public override void StopMonitoring()
        {
            hotkey?.UnregisterHotkey();
        }
    }
}
