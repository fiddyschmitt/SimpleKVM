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

        public static Dictionary<string, int> GetCurrentSources()
        {
            return PlatformServices.Current.Displays.GetCurrentSources();
        }
    }
}
