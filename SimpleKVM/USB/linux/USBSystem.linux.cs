using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;

namespace SimpleKVM.USB.linux
{
    /// <summary>
    /// USB insert/remove events by polling /sys/bus/usb/devices. Needs no permissions.
    /// Device ids use the same VID/PID/serial format as macOS, with the port path standing in
    /// for devices without a serial number.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class USBSystem : USB.USBSystem
    {
        const string DevicesDir = "/sys/bus/usb/devices";
        static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        public USBSystem()
        {
            if (!Directory.Exists(DevicesDir))
                throw new PlatformNotSupportedException($"{DevicesDir} not found");

            var thread = new Thread(WatcherThread)
            {
                IsBackground = true,
                Name = "sysfs USB watcher"
            };
            thread.Start();
        }

        void WatcherThread()
        {
            var known = Snapshot();

            while (true)
            {
                Thread.Sleep(PollInterval);

                try
                {
                    var current = Snapshot();

                    foreach (var (port, id) in current)
                    {
                        if (!known.TryGetValue(port, out var oldId) || oldId != id)
                            OnUsbEvent(new UsbEventArgs(new USBDevice(id, "usb"), EnumUsbEvent.Inserted));
                    }

                    foreach (var (port, id) in known)
                    {
                        if (!current.TryGetValue(port, out var newId) || newId != id)
                            OnUsbEvent(new UsbEventArgs(new USBDevice(id, "usb"), EnumUsbEvent.Removed));
                    }

                    known = current;
                }
                catch
                {
                    //The watcher thread must never take down the app
                }
            }
        }

        /// <summary>Port path (e.g. "1-7.2") to device id, for every device (not interface, not root hub).</summary>
        static Dictionary<string, string> Snapshot()
        {
            var result = new Dictionary<string, string>();

            foreach (var path in Directory.GetDirectories(DevicesDir))
            {
                var port = Path.GetFileName(path);
                if (port.Contains(':') || port.StartsWith("usb")) continue;

                var vid = ReadAttr(path, "idVendor");
                var pid = ReadAttr(path, "idProduct");
                if (vid == null || pid == null) continue;

                var serial = ReadAttr(path, "serial");
                var suffix = !string.IsNullOrEmpty(serial) ? serial : $"PORT{port}";

                result[port] = $"VID_{vid.ToUpperInvariant()}&PID_{pid.ToUpperInvariant()}&SN_{suffix}";
            }

            return result;
        }

        static string? ReadAttr(string devicePath, string name)
        {
            try
            {
                return File.ReadAllText(Path.Combine(devicePath, name)).Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
