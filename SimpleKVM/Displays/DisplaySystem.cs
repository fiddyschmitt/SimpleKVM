using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SimpleKVM.Displays
{
    public static class DisplaySystem
    {
        static List<Monitor>? cachedMonitors = null;

        public static IList<Monitor> GetMonitors(bool useCached)
        {
            if (useCached && cachedMonitors != null)
            {
                return cachedMonitors;
            }

            List<Monitor> result;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = win.DisplaySystem.GetMonitors()
                                .Cast<Monitor>()
                                .ToList();

                cachedMonitors = result;

                return result;
            }

            result = new List<Monitor>();
            return result;
        }
    }
}
