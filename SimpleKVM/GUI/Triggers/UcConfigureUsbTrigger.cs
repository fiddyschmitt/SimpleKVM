using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SimpleKVM.USB;
using System.Linq;
using SimpleKVM.Rules.Triggers;
using System.Text.RegularExpressions;
using SimpleKVM.Rules;

namespace SimpleKVM.GUI.Triggers
{
    public partial class UcConfigureUsbTrigger : UserControl, IValidate, ITriggerCreator
    {
        public UcConfigureUsbTrigger()
        {
            InitializeComponent();

            hyperlinkTemplate = linkChooseUsbDevice.Text;

            DisplayHyperlink();
        }

        public UcConfigureUsbTrigger(USBSystem usbSystem, IValueChangedListener? validationListener = null, Rule? ruleToEdit = null) : this()
        {
            UsbSystem = usbSystem;
            ValueChangedListener = validationListener;

            if (ruleToEdit != null)
            {
                if (ruleToEdit.Trigger is USBTrigger trigger)
                {
                    usbDeviceSelectedByUser = trigger.UsbDevice;
                    usbVerb = trigger.UsbEvent;
                }
            }

            DisplayHyperlink();
        }

        readonly string hyperlinkTemplate;

        void DisplayHyperlink()
        {
            var hyperlink = hyperlinkTemplate.Replace("{verb}", "{" + usbVerb.ToString().ToLower() + "}");

            linkChooseUsbDevice.Links.Clear();

            int linkId = 0;
            while (true)
            {
                var linkStartPosition = hyperlink.IndexOf('{');
                if (linkStartPosition == -1) break;

                hyperlink = hyperlink.Remove(linkStartPosition, 1);

                var linkEndPosition = hyperlink.IndexOf('}');
                hyperlink = hyperlink.Remove(linkEndPosition, 1);

                linkChooseUsbDevice.Links.Add(linkStartPosition, linkEndPosition - linkStartPosition, linkId++);
            }

            linkChooseUsbDevice.Text = hyperlink;
        }

        ChooseUSB? chooseUsbForm;
        USBDevice? usbDeviceSelectedByUser;
        EnumUsbEvent usbVerb = EnumUsbEvent.Inserted;

        public USBSystem? UsbSystem { get; }
        public IValueChangedListener? ValueChangedListener { get; }

        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();

            if (usbDeviceSelectedByUser == null) result.Add(new ValidationResult(linkChooseUsbDevice, "Please choose a USB device", ErrorIconAlignment.TopLeft, -(int)(0.24 * linkChooseUsbDevice.Width)));

            return result;
        }

        private void ShowUsbChooser()
        {
            if (UsbSystem == null)
            {
                MessageBox.Show("Could not initialise USB Monitor", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            chooseUsbForm ??= new ChooseUSB(UsbSystem);

            var diagResult = chooseUsbForm.ShowDialog();

            if (diagResult == DialogResult.OK)
            {
                usbDeviceSelectedByUser = chooseUsbForm.SelectedDevice;
                usbVerb = chooseUsbForm.SelectedVerb;
                DisplayHyperlink();

                ValueChangedListener?.ValueChanged();
            }
        }

        private void ToggleVerb()
        {
            usbVerb = usbVerb.Next();
            DisplayHyperlink();
        }

        public Trigger? GetTrigger()
        {
            if (usbDeviceSelectedByUser == null) return null;
            if (UsbSystem == null) return null;

            var result = new USBTrigger(usbDeviceSelectedByUser, usbVerb);

            return result;
        }

        private void LinkChooseUsbDevice_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int linkId = (int)e.Link.LinkData;
            switch (linkId)
            {
                case 0:
                    ShowUsbChooser();
                    break;

                case 1:
                    ToggleVerb();
                    break;
            }
        }
    }
}
