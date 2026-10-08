using SimpleKVM.Displays.I2C;
using SimpleKVM.Platform.linux;
using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// DDC/CI over a Linux i2c-dev bus (/dev/i2c-N). The kernel adds the 0x6E destination
    /// byte itself from the slave address, so frames start at the source byte. Needs read/write
    /// access to the bus: Fedora grants it to the logged-in user via uaccess for display buses,
    /// otherwise add the user to the i2c group (see ddcutil's i2c_permissions page).
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class DdcTransport(string devicePath)
    {
        const ulong DdcCiAddress = 0x37;
        const ulong EdidAddress = 0x50;

        public string DevicePath { get; } = devicePath;

        //One transaction at a time per bus; DDC/CI monitors need ~50ms between commands
        readonly object ddcLock = new();

        int Open(ulong slaveAddress)
        {
            int fd = LibC.open(DevicePath, LibC.O_RDWR | LibC.O_CLOEXEC);
            if (fd < 0) return -1;

            if (LibC.ioctl(fd, LibC.I2C_SLAVE, slaveAddress) < 0)
            {
                LibC.close(fd);
                return -1;
            }

            return fd;
        }

        public static bool CanOpen(string devicePath)
        {
            int fd = LibC.open(devicePath, LibC.O_RDWR | LibC.O_CLOEXEC);
            if (fd < 0) return false;
            LibC.close(fd);
            return true;
        }

        public byte[]? ReadEdid()
        {
            lock (ddcLock)
            {
                int fd = Open(EdidAddress);
                if (fd < 0) return null;

                try
                {
                    if (LibC.Write(fd, [0x00]) != 1) return null;

                    var edid = new byte[128];
                    return LibC.Read(fd, edid) == edid.Length && edid[0] == 0x00 && edid[1] == 0xFF ? edid : null;
                }
                finally
                {
                    LibC.close(fd);
                }
            }
        }

        public bool SetVcp(byte sourceAddress, byte vcpCode, uint value)
        {
            var msg = DdcCiMessage.BuildSetVcp(sourceAddress, vcpCode, value);

            lock (ddcLock)
            {
                int fd = Open(DdcCiAddress);
                if (fd < 0) return false;

                try
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        if (LibC.Write(fd, msg) == msg.Length)
                        {
                            Thread.Sleep(50);
                            return true;
                        }
                        Thread.Sleep(100);
                    }
                    return false;
                }
                finally
                {
                    LibC.close(fd);
                }
            }
        }

        public bool GetVcp(byte vcpCode, out uint currentValue)
        {
            currentValue = 0;

            byte[] request = [0x51, 0x82, 0x01, vcpCode, 0];
            request[4] = (byte)(0x6E ^ request[0] ^ request[1] ^ request[2] ^ request[3]);

            lock (ddcLock)
            {
                int fd = Open(DdcCiAddress);
                if (fd < 0) return false;

                try
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        if (LibC.Write(fd, request) != request.Length)
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        Thread.Sleep(50);

                        //Reply: [0x6E][0x88][0x02][result][vcp][type][maxH][maxL][curH][curL][chk]
                        var reply = new byte[11];
                        if (LibC.Read(fd, reply) != reply.Length)
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        byte checksum = 0x50;
                        for (int i = 0; i < 10; i++) checksum ^= reply[i];

                        if (reply[0] != 0x6E || reply[1] != 0x88 || reply[2] != 0x02 || reply[4] != vcpCode || checksum != reply[10])
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        if (reply[3] != 0) return false;    //unsupported VCP code

                        currentValue = (uint)((reply[8] << 8) | reply[9]);
                        return true;
                    }

                    return false;
                }
                finally
                {
                    LibC.close(fd);
                }
            }
        }

        /// <summary>
        /// Reads the MCCS capabilities string via chunked 0xF3 requests. Returns null when the
        /// monitor doesn't answer them; callers fall back to EDID + probing.
        /// </summary>
        public string? ReadCapabilitiesString(Action<string>? log = null)
        {
            var result = new StringBuilder();
            int offset = 0;

            lock (ddcLock)
            {
                int fd = Open(DdcCiAddress);
                if (fd < 0)
                {
                    log?.Invoke($"cannot open {DevicePath}");
                    return null;
                }

                try
                {
                    for (int fragment = 0; fragment < 64; fragment++)   //hard cap against a looping monitor
                    {
                        bool frameFound = false;

                        for (int attempt = 0; attempt < 3 && !frameFound; attempt++)
                        {
                            byte[] request = [0x51, 0x83, 0xF3, (byte)(offset >> 8), (byte)(offset & 0xFF), 0];
                            request[5] = (byte)(0x6E ^ request[0] ^ request[1] ^ request[2] ^ request[3] ^ request[4]);

                            if (LibC.Write(fd, request) != request.Length)
                            {
                                log?.Invoke($"offset {offset}: write failed (attempt {attempt + 1})");
                                Thread.Sleep(100);
                                continue;
                            }

                            Thread.Sleep(50);

                            //Reply: [0x6E][len|0x80][0xE3][offH][offL][up to 32 bytes][chk]
                            var reply = new byte[39];
                            if (LibC.Read(fd, reply) != reply.Length)
                            {
                                log?.Invoke($"offset {offset}: read failed (attempt {attempt + 1})");
                                Thread.Sleep(100);
                                continue;
                            }

                            int len = reply[1] & 0x7F;
                            bool valid = reply[0] == 0x6E && (reply[1] & 0x80) != 0 && reply[2] == 0xE3 && len >= 3 && 2 + len < reply.Length;
                            if (valid)
                            {
                                byte checksum = 0x50;
                                for (int i = 0; i < 2 + len; i++) checksum ^= reply[i];
                                valid = checksum == reply[2 + len] && ((reply[3] << 8) | reply[4]) == offset;
                            }

                            if (!valid)
                            {
                                log?.Invoke($"offset {offset}: no valid frame (attempt {attempt + 1}), raw: {string.Join(" ", reply.Select(b => b.ToString("X2")))}");
                                Thread.Sleep(100);
                                continue;
                            }

                            int fragmentLength = len - 3;
                            if (fragmentLength == 0)
                            {
                                log?.Invoke($"offset {offset}: end of string");
                                return result.Length > 0 ? result.ToString() : null;
                            }

                            for (int i = 0; i < fragmentLength; i++)
                            {
                                var b = reply[5 + i];
                                if (b != 0) result.Append((char)b);     //some monitors NUL-pad the string
                            }

                            log?.Invoke($"offset {offset}: +{fragmentLength} bytes");
                            offset += fragmentLength;
                            frameFound = true;
                        }

                        if (!frameFound) return null;
                    }
                }
                finally
                {
                    LibC.close(fd);
                }
            }

            return result.Length > 0 ? result.ToString() : null;
        }
    }
}
