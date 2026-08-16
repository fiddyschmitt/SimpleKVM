using SimpleKVM.Platform;
using System;

namespace SimpleKVM.USB
{
    public abstract class USBSystem
    {
        public event EventHandler<UsbEventArgs> UsbEvent = delegate { };
        protected virtual void OnUsbEvent(UsbEventArgs e)
        {
            UsbEvent?.Invoke(this, e);
        }

        public static USBSystem? INSTANCE
        {
            get
            {
                try
                {
                    return PlatformServices.Current.Usb;
                }
                catch (PlatformNotSupportedException)
                {
                    return null;
                }
            }
        }
    }

    public class UsbEventArgs : EventArgs
    {
        public UsbEventArgs(USBDevice device, EnumUsbEvent usbEvent)
        {
            Device = device;
            UsbEvent = usbEvent;
        }

        public USBDevice Device { get; }
        public EnumUsbEvent UsbEvent { get; }
    }

    public enum EnumUsbEvent
    {
        Inserted = 2,
        Removed = 3
    }
}
