using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using SimpleKVM.USB;
using System.Collections.ObjectModel;

namespace SimpleKVM.Ui
{
    public class ChooseUsbWindow : Window
    {
        readonly USBSystem usbSystem;
        readonly ObservableCollection<UsbEventEntry> events = [];
        readonly ListBox listBox;

        public USBDevice? SelectedDevice { get; private set; }
        public EnumUsbEvent SelectedVerb { get; private set; } = EnumUsbEvent.Inserted;

        public ChooseUsbWindow(USBSystem usbSystem)
        {
            this.usbSystem = usbSystem;

            Title = "Choose USB device";
            Icon = App.LoadIcon();
            Width = 560;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            listBox = new ListBox { ItemsSource = events };
            listBox.DoubleTapped += (s, e) => SelectCurrent();

            var layout = new DockPanel { Margin = new Thickness(12) };
            var instructions = new TextBlock
            {
                Text = "Insert or remove the USB device you want to use as the trigger, then double-click its event below.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(instructions, Dock.Top);
            layout.Children.Add(instructions);
            layout.Children.Add(listBox);

            Content = layout;

            Opened += (s, e) => usbSystem.UsbEvent += UsbSystem_UsbEvent;
            Closed += (s, e) => usbSystem.UsbEvent -= UsbSystem_UsbEvent;
        }

        void UsbSystem_UsbEvent(object? sender, UsbEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                events.Insert(0, new UsbEventEntry(e));
            });
        }

        void SelectCurrent()
        {
            if (listBox.SelectedItem is not UsbEventEntry entry) return;

            SelectedDevice = entry.EventArgs.Device;
            SelectedVerb = entry.EventArgs.UsbEvent;
            Close(true);
        }

        class UsbEventEntry(UsbEventArgs eventArgs)
        {
            public UsbEventArgs EventArgs { get; } = eventArgs;

            public override string ToString()
            {
                return $"{EventArgs.UsbEvent}   {EventArgs.Device.DeviceClass} {EventArgs.Device.DeviceID}";
            }
        }
    }
}
