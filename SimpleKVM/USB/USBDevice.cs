using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SimpleKVM.USB
{
    public class USBDevice
    {
        public string DeviceID;

        public USBDevice(string deviceID)
        {
            DeviceID = deviceID;
        }
    }
}
