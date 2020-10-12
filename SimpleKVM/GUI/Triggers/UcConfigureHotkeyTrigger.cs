using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;

namespace SimpleKVM.GUI.Triggers
{
    public partial class UcConfigureHotkeyTrigger : UserControl, IValidate, ITriggerCreator
    {
        public UcConfigureHotkeyTrigger()
        {
            InitializeComponent();

            hyperlinkTemplate = linkLabel1.Text;

            DisplayHyperlink();
        }

        public UcConfigureHotkeyTrigger(IValueChangedListener? validationListener = null, Rule? ruleToEdit = null) : this()
        {
            ValueChangedListener = validationListener;
            RuleToEdit = ruleToEdit;
            if (ruleToEdit != null)
            {
                if (ruleToEdit.Trigger is HotkeyTrigger trigger)
                {
                    hotkeyStringChosenByUser = trigger.HotkeyAsString;
                }
            }

            DisplayHyperlink();
        }

        readonly string hyperlinkTemplate;
        public IValueChangedListener? ValueChangedListener { get; }
        public Rule? RuleToEdit { get; }

        string? hotkeyStringChosenByUser;

        void DisplayHyperlink()
        {
            string hyperlink = hyperlinkTemplate;
            if (hotkeyStringChosenByUser != null)
            {
                hyperlink = hyperlinkTemplate.Replace("{this hotkey}", "{" + hotkeyStringChosenByUser + "}");
            }            

            linkLabel1.Links.Clear();

            int linkId = 0;
            while (true)
            {
                var linkStartPosition = hyperlink.IndexOf('{');
                if (linkStartPosition == -1) break;

                hyperlink = hyperlink.Remove(linkStartPosition, 1);

                var linkEndPosition = hyperlink.IndexOf('}');
                hyperlink = hyperlink.Remove(linkEndPosition, 1);

                linkLabel1.Links.Add(linkStartPosition, linkEndPosition - linkStartPosition, linkId++);
            }

            linkLabel1.Text = hyperlink;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int linkId = (int)e.Link.LinkData;
            switch (linkId)
            {
                case 0:
                    ShowHotkeyChooser();
                    break;
            }
        }

        ChooseHotkey? chooseHotkeyForm;

        private void ShowHotkeyChooser()
        {
            chooseHotkeyForm ??= new ChooseHotkey(hotkeyStringChosenByUser, RuleToEdit);

            var diagResult = chooseHotkeyForm.ShowDialog();

            if (diagResult == DialogResult.OK)
            {
                hotkeyStringChosenByUser = chooseHotkeyForm.HotkeyStringChosenByUser;
                DisplayHyperlink();

                ValueChangedListener?.ValueChanged();
            }
        }

        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();

            if (hotkeyStringChosenByUser == null) result.Add(new ValidationResult(linkLabel1, "Please choose a hotkey", ErrorIconAlignment.TopLeft, -(int)(0.35 * linkLabel1.Width)));

            return result;
        }

        public Trigger? GetTrigger()
        {
            if (hotkeyStringChosenByUser == null) return null;

            var result = new HotkeyTrigger(hotkeyStringChosenByUser);

            return result;
        }
    }
}
