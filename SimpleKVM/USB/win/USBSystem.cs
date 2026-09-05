using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace SimpleKVM.USB.win
{
    [SupportedOSPlatform("windows6.1")]
    public class USBSystem : USB.USBSystem
    {
        readonly List<ManagementEventWatcher> watchers = [];

        public USBSystem()
        {
            //Each class is watched independently: one WMI class failing to register shouldn't
            //cost the others. Only when none could be started is the watcher unusable.
            var failures = new List<string>();
            foreach (var deviceClass in new[] { "Win32_USBHub", "Win32_PointingDevice", "Win32_Keyboard" })
            {
                try
                {
                    WatchDeviceClass(deviceClass);
                }
                catch (Exception ex)
                {
                    failures.Add($"{deviceClass}: {ex.Message}");
                }
            }

            if (watchers.Count == 0)
            {
                throw new InvalidOperationException($"WMI device watching could not be started ({string.Join("; ", failures)})");
            }

            //WatchDeviceClass("Win32_USBController");

            //This one generates a lot of entries
            //WatchDeviceClass("Win32_PnPEntity");

            //These cause an exception
            //WatchDeviceClass("Win32_USBControllerDevice");
            //WatchDeviceClass("Win32_PnPDevice");
        }

        void WatchDeviceClass(string deviceClass)
        {
            var insertQuery = new WqlEventQuery($"SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA '{deviceClass}'");
            var insertWatcher = new ManagementEventWatcher(insertQuery);
            insertWatcher.EventArrived += (sender, e) => PropogateEvent(e, deviceClass, EnumUsbEvent.Inserted);
            insertWatcher.Start();
            watchers.Add(insertWatcher);

            var removeQuery = new WqlEventQuery($"SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA '{deviceClass}'");
            var removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += (sender, e) => PropogateEvent(e, deviceClass, EnumUsbEvent.Removed);
            removeWatcher.Start();
            watchers.Add(removeWatcher);
        }

        void PropogateEvent(EventArrivedEventArgs e, string deviceClass, EnumUsbEvent eventType)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            var deviceID = instance["DeviceID"];
            if (deviceID != null)
            {
                var device = new USBDevice($"{deviceID}", deviceClass);

                OnUsbEvent(new UsbEventArgs(device, eventType));
            }
        }
    };
}
