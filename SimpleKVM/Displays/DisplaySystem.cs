using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SimpleKVM.Displays
{
    public static class DisplaySystem
    {
        public static IList<Monitor> GetMonitors()
        {
            List<Monitor> result;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = Displays.win.DisplaySystem.GetMonitors()
                                .Cast<Monitor>()
                                .ToList();

                return result;
            }

            result = new List<Monitor>();
            return result;
        }
    }
}
