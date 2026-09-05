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

        static readonly object instanceLock = new();
        static USBSystem? instance;

        /// <summary>
        /// Why the USB watcher could not be started (no backend for this OS, or on Windows a
        /// broken WMI service), or null while it is running or has not been needed yet.
        /// </summary>
        public static string? InitializationError { get; private set; }

        /// <summary>
        /// The platform's USB watcher, or null when it could not be started; see
        /// <see cref="InitializationError"/>. A failure is remembered rather than retried on every
        /// access, and never takes the app down: hotkey and idle rules keep working without it.
        /// </summary>
        public static USBSystem? INSTANCE
        {
            get
            {
                lock (instanceLock)
                {
                    if (instance != null) return instance;
                    if (InitializationError != null) return null;

                    try
                    {
                        instance = PlatformServices.Current.Usb;
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        InitializationError = ex.Message;
                        Console.WriteLine($"USB device watching is unavailable: {ex}");
                        return null;
                    }
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
