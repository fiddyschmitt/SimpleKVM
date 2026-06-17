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
            List<Monitor>? result = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = win.DisplaySystem.GetMonitors()
                                .Cast<Monitor>()
                                .ToList();
            }

            result ??= [];
            return result;
        }

        public static Dictionary<string, int> GetCurrentSources()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return win.DisplaySystem.GetCurrentSources();
            }

            return [];
        }
    }
}
