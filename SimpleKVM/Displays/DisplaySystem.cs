using SimpleKVM.Platform;
using System.Collections.Generic;

namespace SimpleKVM.Displays
{
    public static class DisplaySystem
    {
        public static IList<Monitor> GetMonitors()
        {
            return PlatformServices.Current.Displays.GetMonitors();
        }

        /// <summary>Drops the monitor cache so the next GetMonitors re-enumerates and re-probes every monitor.</summary>
        public static void InvalidateMonitors()
        {
            PlatformServices.Current.Displays.InvalidateMonitors();
        }

        public static Dictionary<string, int> GetCurrentSources()
        {
            return PlatformServices.Current.Displays.GetCurrentSources();
        }
    }
}
