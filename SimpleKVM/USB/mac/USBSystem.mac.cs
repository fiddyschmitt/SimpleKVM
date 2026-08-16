using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace SimpleKVM.USB.mac
{
    /// <summary>
    /// USB insert/remove events via IOKit matching notifications on a dedicated CFRunLoop
    /// thread. Push-based, so considerably faster than the 2-second WMI polling on Windows.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public class USBSystem : USB.USBSystem
    {
        const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
        const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        delegate void IOServiceMatchingCallback(IntPtr refCon, uint iterator);

        [DllImport(IOKit)]
        static extern IntPtr IONotificationPortCreate(uint mainPort);

        [DllImport(IOKit)]
        static extern IntPtr IONotificationPortGetRunLoopSource(IntPtr notifyPort);

        [DllImport(IOKit)]
        static extern IntPtr IOServiceMatching(string name);

        [DllImport(IOKit)]
        static extern int IOServiceAddMatchingNotification(IntPtr notifyPort, string notificationType, IntPtr matching, IOServiceMatchingCallback callback, IntPtr refCon, out uint iterator);

        [DllImport(IOKit)]
        static extern uint IOIteratorNext(uint iterator);

        [DllImport(IOKit)]
        static extern int IOObjectRelease(uint obj);

        [DllImport(IOKit)]
        static extern IntPtr IORegistryEntryCreateCFProperty(uint entry, IntPtr key, IntPtr allocator, uint options);

        [DllImport(CoreFoundation)]
        static extern IntPtr CFRunLoopGetCurrent();

        [DllImport(CoreFoundation)]
        static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

        [DllImport(CoreFoundation)]
        static extern void CFRunLoopRun();

        [DllImport(CoreFoundation)]
        static extern bool CFNumberGetValue(IntPtr number, int theType, out int value);

        [DllImport(CoreFoundation)]
        static extern void CFRelease(IntPtr cf);

        const int kCFNumberSInt32Type = 3;

        //Keep the callback delegates alive for the lifetime of the process
        readonly IOServiceMatchingCallback insertedCallback;
        readonly IOServiceMatchingCallback removedCallback;

        uint insertedIterator;
        uint removedIterator;
        bool draining = true;

        public USBSystem()
        {
            insertedCallback = (refCon, iterator) => DrainIterator(iterator, EnumUsbEvent.Inserted);
            removedCallback = (refCon, iterator) => DrainIterator(iterator, EnumUsbEvent.Removed);

            var thread = new Thread(WatcherThread)
            {
                IsBackground = true,
                Name = "IOKit USB watcher"
            };
            thread.Start();
        }

        void WatcherThread()
        {
            try
            {
                var notifyPort = IONotificationPortCreate(0);
                var source = IONotificationPortGetRunLoopSource(notifyPort);

                var cfLib = NativeLibrary.Load(CoreFoundation);
                var defaultMode = Marshal.ReadIntPtr(NativeLibrary.GetExport(cfLib, "kCFRunLoopDefaultMode"));

                CFRunLoopAddSource(CFRunLoopGetCurrent(), source, defaultMode);

                //Each registration consumes its own matching dictionary
                IOServiceAddMatchingNotification(notifyPort, "IOServiceFirstMatch", IOServiceMatching("IOUSBDevice"), insertedCallback, IntPtr.Zero, out insertedIterator);
                IOServiceAddMatchingNotification(notifyPort, "IOServiceTerminate", IOServiceMatching("IOUSBDevice"), removedCallback, IntPtr.Zero, out removedIterator);

                //IOKit requires the initial iterators be drained to arm the notifications;
                //these are the devices already present, so no events are raised for them
                draining = true;
                DrainIterator(insertedIterator, EnumUsbEvent.Inserted);
                DrainIterator(removedIterator, EnumUsbEvent.Removed);
                draining = false;

                CFRunLoopRun();
            }
            catch
            {
                //The watcher thread must never take down the app
            }
        }

        void DrainIterator(uint iterator, EnumUsbEvent eventType)
        {
            uint service;
            while ((service = IOIteratorNext(iterator)) != 0)
            {
                try
                {
                    if (!draining)
                    {
                        var deviceId = BuildDeviceId(service);
                        if (deviceId != null)
                        {
                            OnUsbEvent(new UsbEventArgs(new USBDevice(deviceId, "IOUSBDevice"), eventType));
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    IOObjectRelease(service);
                }
            }
        }

        static string? BuildDeviceId(uint service)
        {
            var vid = GetIntProperty(service, "idVendor");
            var pid = GetIntProperty(service, "idProduct");
            if (vid == null || pid == null) return null;

            //Serial number when the device has one, location id (physical port path) otherwise
            var serial = GetStringProperty(service, "USB Serial Number");
            var location = GetIntProperty(service, "locationID");
            var suffix = !string.IsNullOrEmpty(serial) ? serial : $"LOC{location:X8}";

            return $"VID_{vid:X4}&PID_{pid:X4}&SN_{suffix}";
        }

        static int? GetIntProperty(uint service, string propertyName)
        {
            var key = Displays.mac.IOKitNative.CreateCFString(propertyName);
            try
            {
                var value = IORegistryEntryCreateCFProperty(service, key, IntPtr.Zero, 0);
                if (value == IntPtr.Zero) return null;

                try
                {
                    return CFNumberGetValue(value, kCFNumberSInt32Type, out int result) ? result : null;
                }
                finally
                {
                    CFRelease(value);
                }
            }
            finally
            {
                CFRelease(key);
            }
        }

        static string? GetStringProperty(uint service, string propertyName)
        {
            return Displays.mac.IOKitNative.GetRegistryStringProperty(service, propertyName);
        }
    }
}
