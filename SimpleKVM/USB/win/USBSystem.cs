using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace SimpleKVM.USB.win
{
    public class USBSystem : USB.USBSystem
    {
        public USBSystem()
        {
            var insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
            var insertWatcher = new ManagementEventWatcher(insertQuery);
            insertWatcher.EventArrived += (sender, e) => PropogateEvent(e, EnumUsbEvent.Inserted);
            insertWatcher.Start();

            var removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
            ManagementEventWatcher removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += (sender, e) => PropogateEvent(e, EnumUsbEvent.Removed);
            removeWatcher.Start();
        }

        void PropogateEvent(EventArrivedEventArgs e, EnumUsbEvent eventType)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            var deviceID = instance["DeviceID"];
            if (deviceID != null)
            {
                var device = new USBDevice($"{deviceID}");

                OnUsbEvent(new UsbEventArgs(device, eventType));
            }
        }
    };
}
