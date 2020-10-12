using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SimpleKVM.USB
{
    public abstract class USBSystem
    {
        public event EventHandler<UsbEventArgs> UsbEvent = delegate { };
        protected virtual void OnUsbEvent(UsbEventArgs e)
        {
            UsbEvent?.Invoke(this, e);
        }

        static USBSystem? usbSystem = null;
        public static USBSystem? INSTANCE
        {
            get
            {
                if (usbSystem == null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        usbSystem = new win.USBSystem();
                    }
                }

                return usbSystem;
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
