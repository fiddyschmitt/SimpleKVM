using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;

namespace SimpleKVM.USB.linux
{
    /// <summary>
    /// USB insert/remove events by polling the device tree in sysfs. Needs no permissions.
    /// Device ids use the same VID/PID/serial format as macOS, with the port path standing in
    /// for devices without a serial number.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class USBSystem : USB.USBSystem
    {
        static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        public USBSystem()
        {
            if (!Directory.Exists(UsbSysfs.DefaultDevicesDir))
                throw new PlatformNotSupportedException($"{UsbSysfs.DefaultDevicesDir} not found");

            var thread = new Thread(WatcherThread)
            {
                IsBackground = true,
                Name = "sysfs USB watcher"
            };
            thread.Start();
        }

        void WatcherThread()
        {
            //Everything, the first snapshot included, sits inside the guard: an unhandled
            //exception on this thread would take the whole app down
            Dictionary<string, string>? known = null;

            while (true)
            {
                try
                {
                    var current = UsbSysfs.Snapshot();

                    if (known != null)
                    {
                        var (inserted, removed) = UsbSysfs.Diff(known, current);
                        foreach (var id in inserted) OnUsbEvent(new UsbEventArgs(new USBDevice(id, "usb"), EnumUsbEvent.Inserted));
                        foreach (var id in removed) OnUsbEvent(new UsbEventArgs(new USBDevice(id, "usb"), EnumUsbEvent.Removed));
                    }

                    known = current;
                }
                catch
                {
                    //The watcher thread must never take down the app
                }

                Thread.Sleep(PollInterval);
            }
        }
    }
}
