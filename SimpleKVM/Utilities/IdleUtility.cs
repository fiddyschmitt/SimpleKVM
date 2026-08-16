using SimpleKVM.Platform;
using System;

namespace SimpleKVM.Utilities
{
    public static class IdleUtility
    {
        public static TimeSpan GetIdleTimeSpan()
        {
            return PlatformServices.Current.Idle.GetIdleTimeSpan();
        }
    }
}
