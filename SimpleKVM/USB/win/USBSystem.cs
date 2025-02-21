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
            WatchDeviceClass("Win32_USBHub");
            WatchDeviceClass("Win32_PointingDevice");
            WatchDeviceClass("Win32_Keyboard");

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

            var removeQuery = new WqlEventQuery($"SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA '{deviceClass}'");
            var removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += (sender, e) => PropogateEvent(e, deviceClass, EnumUsbEvent.Removed);
            removeWatcher.Start();
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
