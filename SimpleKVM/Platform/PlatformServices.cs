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
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return new win.WindowsPlatform();
            if (OperatingSystem.IsMacOS()) return new mac.MacPlatform();
            throw new PlatformNotSupportedException("SimpleKVM does not support this operating system yet.");
        }
    }
}
