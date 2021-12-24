using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SimpleKVM.USB
{
    public class USBDevice
    {
        public string DeviceID;
        public string DeviceClass;

        public USBDevice(string deviceID, string deviceClass)
        {
            DeviceID = deviceID;
            DeviceClass = deviceClass;
        }
    }
}
