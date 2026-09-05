using Avalonia.Controls;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;
using SimpleKVM.USB;
using System.Collections.Generic;

namespace SimpleKVM.Ui.Controls
{
    public class UsbTriggerView : UserControl, IValidate, ITriggerCreator
    {
        readonly HyperlinkButton deviceLink;
        readonly HyperlinkButton verbLink;
        readonly USBSystem? usbSystem;
        readonly IValueChangedListener? valueChangedListener;

        USBDevice? usbDeviceSelectedByUser;
        EnumUsbEvent usbVerb = EnumUsbEvent.Inserted;

        public UsbTriggerView(USBSystem? usbSystem, IValueChangedListener? valueChangedListener, Rule? ruleToEdit)
        {
            this.usbSystem = usbSystem;
            this.valueChangedListener = valueChangedListener;

            if (ruleToEdit?.Trigger is USBTrigger trigger)
            {
                usbDeviceSelectedByUser = trigger.UsbDevice;
                usbVerb = trigger.UsbEvent;
            }

            deviceLink = new HyperlinkButton
            {
                Content = "this",
                Padding = new Avalonia.Thickness(0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            deviceLink.Click += async (s, e) => await ShowUsbChooser();

            if (usbSystem == null)
            {
                //No watcher (e.g. WMI is broken on this machine): the rule can still be edited,
                //but no device can be picked and the trigger won't fire
                deviceLink.IsEnabled = false;
                ToolTip.SetTip(deviceLink, UnavailableMessage);
            }

            verbLink = new HyperlinkButton
            {
                Padding = new Avalonia.Thickness(0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            verbLink.Click += (s, e) =>
            {
                usbVerb = usbVerb.Next();
                UpdateVerbText();
            };

            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4
            };
            row.Children.Add(new TextBlock { Text = "Whenever", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            row.Children.Add(deviceLink);
            row.Children.Add(new TextBlock { Text = "USB device is", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            row.Children.Add(verbLink);
            row.Children.Add(new TextBlock { Text = ", set the monitor sources to:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });

            Content = row;

            UpdateVerbText();
        }

        void UpdateVerbText()
        {
            verbLink.Content = usbVerb.ToString().ToLower();
        }

        static string UnavailableMessage => $"USB device watching is unavailable on this computer: {USBSystem.InitializationError}";

        async System.Threading.Tasks.Task ShowUsbChooser()
        {
            if (usbSystem == null) return;
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var chooser = new ChooseUsbWindow(usbSystem);
            var ok = await chooser.ShowDialog<bool>(owner);

            if (ok && chooser.SelectedDevice != null)
            {
                usbDeviceSelectedByUser = chooser.SelectedDevice;
                usbVerb = chooser.SelectedVerb;
                UpdateVerbText();

                valueChangedListener?.ValueChanged();
            }
        }

        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();

            if (usbDeviceSelectedByUser == null)
            {
                result.Add(new ValidationResult(deviceLink, usbSystem == null ? UnavailableMessage : "Please choose a USB device"));
            }

            return result;
        }

        public Trigger? GetTrigger()
        {
            if (usbDeviceSelectedByUser == null) return null;

            return new USBTrigger(usbDeviceSelectedByUser, usbVerb);
        }
    }
}
