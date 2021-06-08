using SimpleKVM.USB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SimpleKVM
{
    public partial class ChooseUSB : Form
    {
        public USBDevice? SelectedDevice { get; protected set; }
        public USBSystem UsbSystem { get; }
        public EnumUsbEvent SelectedVerb { get; protected set; } = EnumUsbEvent.Inserted;

        public ChooseUSB(USBSystem usbSystem)
        {
            InitializeComponent();
            UsbSystem = usbSystem;
        }

        private void UsbSystem_UsbEvent(object? sender, UsbEventArgs e)
        {
            Invoke(new MethodInvoker(() =>
            {
                var lvi = new ListViewItem(new[] { e.UsbEvent.ToString(), e.Device.DeviceID })
                {
                    Tag = e
                };

                listView1.Items.Insert(0, lvi);
                SizeLastColumn(listView1);
            }
            ));
        }

        private void ListView1_Resize(object sender, EventArgs e)
        {
            SizeLastColumn((ListView)sender);
        }

        private void SizeLastColumn(ListView lv)
        {
            lv.Columns[^1].Width = -2;
        }

        private void ListView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;

            if (listView1.SelectedItems[0].Tag is UsbEventArgs usbEventArgs)
            {
                SelectedDevice = usbEventArgs.Device;
                SelectedVerb = usbEventArgs.UsbEvent;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ChooseUSB_Shown(object sender, EventArgs e)
        {
            UsbSystem.UsbEvent += UsbSystem_UsbEvent;
        }

        private void ChooseUSB_FormClosed(object sender, FormClosedEventArgs e)
        {
            UsbSystem.UsbEvent -= UsbSystem_UsbEvent;
        }
    }
}
