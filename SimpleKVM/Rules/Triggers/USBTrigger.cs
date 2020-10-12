using Newtonsoft.Json;
using SimpleKVM.USB;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleKVM.Rules.Triggers
{
    [JsonObject]
    public class USBTrigger : Trigger
    {
        public USBTrigger(USBDevice usbDevice, EnumUsbEvent usbEvent)
        {
            UsbDevice = usbDevice;
            UsbEvent = usbEvent;
        }

        public USBDevice UsbDevice { get; set; }
        public EnumUsbEvent UsbEvent { get; set; }

        [JsonIgnore]
        public USBSystem? UsbSystem => SimpleKVM.USB.USBSystem.INSTANCE;

        public override string GetTriggerAsFriendlyString()
        {
            var result = $"whenever a specific USB device is {UsbEvent.ToString().ToLower()}";
            return result;
        }


        public override void StartMonitoring()
        {
            if (UsbSystem != null)
            {
                UsbSystem.UsbEvent -= UsbSystem_UsbEvent;   //a tricky manoeuvre to ensure we don't register for the event multiple times
                UsbSystem.UsbEvent += UsbSystem_UsbEvent;
            }
        }

        private void UsbSystem_UsbEvent(object? sender, UsbEventArgs e)
        {
            if (UsbDevice.DeviceID.Equals(e.Device.DeviceID) && UsbEvent == e.UsbEvent)
            {
                RaiseTriggered();
            }
        }

        public override void StopMonitoring()
        {
            if (UsbSystem != null)
            {
                UsbSystem.UsbEvent -= UsbSystem_UsbEvent;
            }
        }
    }
}
