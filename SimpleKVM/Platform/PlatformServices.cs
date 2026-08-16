using System;

namespace SimpleKVM.Platform
{
    /// <summary>
    /// The only place in the codebase that switches on the operating system.
    /// </summary>
    public static class PlatformServices
    {
        static readonly Lazy<IPlatform> current = new(Create);

        public static IPlatform Current => current.Value;

        static IPlatform Create()
        {
#if WINDOWS
            if (OperatingSystem.IsWindows()) return new win.WindowsPlatform();
#endif
            throw new PlatformNotSupportedException("SimpleKVM does not support this operating system yet.");
        }
    }
}
