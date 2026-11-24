using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleKVM.USB.linux
{
    /// <summary>
    /// Reads the USB device tree the kernel publishes under /sys/bus/usb/devices into
    /// "port path to device id" snapshots, and diffs two of them. Pure file reads against a
    /// directory, so it runs against a copy of a real sysfs tree in tests.
    /// </summary>
    public static class UsbSysfs
    {
        public const string DefaultDevicesDir = "/sys/bus/usb/devices";

        /// <summary>
        /// Port path (e.g. "1-7.2") to device id, for every device: not interfaces ("1-7.2:1.0")
        /// and not root hubs ("usb1"). The id is VID/PID plus the serial number when the device
        /// has one, otherwise the port path, which is what the macOS backend does too.
        /// </summary>
        public static Dictionary<string, string> Snapshot(string devicesDir = DefaultDevicesDir)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(devicesDir)) return result;

            foreach (var path in Directory.GetDirectories(devicesDir))
            {
                var port = Path.GetFileName(path);
                if (port.Contains(':') || port.StartsWith("usb", StringComparison.Ordinal)) continue;

                var vid = ReadAttribute(path, "idVendor");
                var pid = ReadAttribute(path, "idProduct");
                if (vid == null || pid == null) continue;

                var serial = ReadAttribute(path, "serial");
                var suffix = !string.IsNullOrEmpty(serial) ? serial : $"PORT{port}";

                result[port] = $"VID_{vid.ToUpperInvariant()}&PID_{pid.ToUpperInvariant()}&SN_{suffix}";
            }

            return result;
        }

        /// <summary>
        /// What changed between two snapshots. A port whose device id changed counts as a
        /// removal of the old device and an insertion of the new one.
        /// </summary>
        public static (List<string> Inserted, List<string> Removed) Diff(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
        {
            var inserted = new List<string>();
            var removed = new List<string>();

            foreach (var (port, id) in after)
            {
                if (!before.TryGetValue(port, out var oldId) || oldId != id) inserted.Add(id);
            }

            foreach (var (port, id) in before)
            {
                if (!after.TryGetValue(port, out var newId) || newId != id) removed.Add(id);
            }

            return (inserted, removed);
        }

        static string? ReadAttribute(string devicePath, string name)
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
