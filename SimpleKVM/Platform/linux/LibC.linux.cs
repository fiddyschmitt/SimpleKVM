using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.linux
{
    [SupportedOSPlatform("linux")]
    static class LibC
    {
        public const int O_RDWR = 2;
        public const int O_RDONLY = 0;
        public const int O_CLOEXEC = 0x80000;

        /// <summary>i2c-dev: set the 7-bit slave address for subsequent read/write calls.</summary>
        public const ulong I2C_SLAVE = 0x0703;

        [DllImport("libc", SetLastError = true)]
        public static extern int open(string path, int flags);

        [DllImport("libc", SetLastError = true)]
        public static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        public static extern int ioctl(int fd, ulong request, ulong arg);

        [DllImport("libc", SetLastError = true)]
        public static extern unsafe nint read(int fd, byte* buffer, nint count);

        [DllImport("libc", SetLastError = true)]
        public static extern unsafe nint write(int fd, byte* buffer, nint count);

        public static unsafe int Read(int fd, byte[] buffer)
        {
            fixed (byte* p = buffer) return (int)read(fd, p, buffer.Length);
        }

        public static unsafe int Write(int fd, byte[] buffer)
        {
            fixed (byte* p = buffer) return (int)write(fd, p, buffer.Length);
        }
    }
}
