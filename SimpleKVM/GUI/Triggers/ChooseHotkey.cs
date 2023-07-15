using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SimpleKVM.GUI.Triggers
{
    public partial class ChooseHotkey : Form
    {
        public string? HotkeyStringChosenByUser { get; internal set; }
        public Rule? RuleBeingEdited { get; }

        public ChooseHotkey(string? hotkeyStringChosenByUser, Rule? ruleBeingEdited)
        {
            InitializeComponent();

            textBox1.Text = hotkeyStringChosenByUser;
            RuleBeingEdited = ruleBeingEdited;
        }

        bool winPressed = false;

        private void TextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.ControlKey) return;
            if (e.KeyCode == Keys.Menu) return;
            if (e.KeyCode == Keys.ShiftKey) return;

            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                winPressed = true;
                return;
            }

            Keys pressedKey = e.KeyData ^ e.Modifiers; //remove modifier keys

            var modifierIsPressed = (e.Modifiers != Keys.None || winPressed);

            if (modifierIsPressed && pressedKey != Keys.None)
            {
                var converter = new KeysConverter();

                textBox1.Text = converter.ConvertToString(e.KeyData);

                if (winPressed)
                {
                    textBox1.Text = $"Win+{textBox1.Text}";
                }

                CheckIfHotkeyIsAvailable();
            };
        }

        private void TextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void TextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                winPressed = false;
                return;
            }
        }

        void CheckIfHotkeyIsAvailable()
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                label1.Text = "";
                label1.ForeColor = Color.FromKnownColor(KnownColor.ControlText);
                button1.Enabled = false;
                return;
            }

            bool hotkeyAvailable = true;


            try
            {
                //check if the hotkey is in use
                var hotkey = new Hotkey(textBox1.Text, null);
                hotkey.UnregisterHotkey();

                //check if another rule already uses this hotkey
                bool hotkeyInUseByAnotherRule = Form1
                                                    .Rules
                                                    .Where(rule => rule != RuleBeingEdited)
                                                    .Select(rule => rule.Trigger)
                                                    .OfType<HotkeyTrigger>()
                                                    .Any(trigger => trigger.HotkeyAsString.Equals(textBox1.Text));

                if (hotkeyInUseByAnotherRule)
                {
                    hotkeyAvailable = false;
                }
            }
            catch
            {
                hotkeyAvailable = false;
            }

            if (hotkeyAvailable)
            {
                label1.Text = "Available";
                label1.ForeColor = Color.Green;
                button1.Enabled = true;
            }
            else
            {
                label1.Text = "Unavailable";
                label1.ForeColor = Color.Red;
                button1.Enabled = false;
                return;
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            HotkeyStringChosenByUser = textBox1.Text;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ChooseHotkey_Shown(object sender, EventArgs e)
        {
            textBox1.Focus();
            textBox1.SelectionStart = 0;
            textBox1.SelectionLength = 0;
            CheckIfHotkeyIsAvailable();
        }
    }
}
