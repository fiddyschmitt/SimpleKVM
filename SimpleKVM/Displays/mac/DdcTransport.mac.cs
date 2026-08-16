using SimpleKVM.Displays.I2C;
using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace SimpleKVM.Displays.mac
{
    /// <summary>
    /// DDC/CI over a display's IOAVService (Apple Silicon DCP). Framing matches m1ddc;
    /// replies are located by scanning for a checksum-valid frame because the DCP
    /// prepends stale bytes to I2C reads.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public class DdcTransport(IntPtr avService)
    {
        const uint ChipAddress = 0x37;          //7-bit DDC/CI slave
        const uint DataAddress = 0x51;          //standard DDC source byte, sent as the I2C register

        readonly object ddcLock = new();

        public IntPtr AvService { get; } = avService;

        public byte[]? ReadEdid()
        {
            lock (ddcLock)
            {
                return IOKitNative.AVServiceCopyEDID(AvService);
            }
        }

        public bool SetVcp(byte sourceAddress, byte vcpCode, uint value)
        {
            var msg = DdcCiMessage.BuildSetVcp(sourceAddress, vcpCode, value);

            lock (ddcLock)
            {
                for (int i = 0; i < 2; i++)     //m1ddc sends every write twice (DDC_ITERATIONS)
                {
                    Thread.Sleep(10);
                    int status = IOKitNative.AVServiceWriteI2C(AvService, ChipAddress, msg[0], msg.Skip(1).ToArray());
                    if (status != 0) return false;
                }
            }

            return true;
        }

        public bool GetVcp(byte vcpCode, out uint currentValue)
        {
            currentValue = 0;

            lock (ddcLock)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    byte[] request = [0x82, 0x01, vcpCode, 0];
                    request[3] = (byte)(0x6E ^ request[0] ^ request[1] ^ request[2]);

                    bool writeOk = true;
                    for (int i = 0; i < 2; i++)
                    {
                        Thread.Sleep(10);
                        if (IOKitNative.AVServiceWriteI2C(AvService, ChipAddress, DataAddress, request) != 0)
                        {
                            writeOk = false;
                            break;
                        }
                    }

                    Thread.Sleep(50);

                    var reply = new byte[24];
                    if (!writeOk || IOKitNative.AVServiceReadI2C(AvService, ChipAddress, DataAddress, reply) != 0)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    //Reply frame: [0x6E][0x88][0x02][result][vcp][type][maxH][maxL][curH][curL][chk]
                    for (int start = 0; start + 11 <= reply.Length; start++)
                    {
                        if (reply[start] != 0x6E || reply[start + 1] != 0x88 || reply[start + 2] != 0x02) continue;
                        if (reply[start + 4] != vcpCode) continue;

                        byte checksum = 0x50;
                        for (int i = start; i < start + 10; i++) checksum ^= reply[i];
                        if (checksum != reply[start + 10]) continue;

                        if (reply[start + 3] != 0) return false;    //result code: unsupported VCP

                        currentValue = (uint)((reply[start + 8] << 8) | reply[start + 9]);
                        return true;
                    }

                    Thread.Sleep(100);
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the MCCS capabilities string via chunked 0xF3 requests. Returns null when the
        /// monitor doesn't answer them (common); callers fall back to EDID + probing.
        /// </summary>
        public string? ReadCapabilitiesString()
        {
            var result = new StringBuilder();
            int offset = 0;

            lock (ddcLock)
            {
                for (int fragment = 0; fragment < 64; fragment++)   //hard cap against a looping monitor
                {
                    byte[] request = [0x83, 0xF3, (byte)(offset >> 8), (byte)(offset & 0xFF), 0];
                    request[4] = (byte)(0x6E ^ request[0] ^ request[1] ^ request[2] ^ request[3]);

                    bool writeOk = true;
                    for (int i = 0; i < 2; i++)
                    {
                        Thread.Sleep(10);
                        if (IOKitNative.AVServiceWriteI2C(AvService, ChipAddress, DataAddress, request) != 0)
                        {
                            writeOk = false;
                            break;
                        }
                    }
                    if (!writeOk) return null;

                    Thread.Sleep(50);

                    var reply = new byte[48];
                    if (IOKitNative.AVServiceReadI2C(AvService, ChipAddress, DataAddress, reply) != 0) return null;

                    //Reply frame: [0x6E][len|0x80][0xE3][offH][offL][fragment bytes...][chk]
                    bool frameFound = false;
                    for (int start = 0; start + 7 <= reply.Length; start++)
                    {
                        if (reply[start] != 0x6E || (reply[start + 1] & 0x80) == 0 || reply[start + 2] != 0xE3) continue;

                        int len = reply[start + 1] & 0x7F;          //bytes after the length byte, excluding checksum
                        if (len < 3 || start + 2 + len >= reply.Length) continue;

                        byte checksum = 0x50;
                        for (int i = start; i < start + 2 + len; i++) checksum ^= reply[i];
                        if (checksum != reply[start + 2 + len]) continue;

                        int replyOffset = (reply[start + 3] << 8) | reply[start + 4];
                        if (replyOffset != offset) continue;

                        int fragmentLength = len - 3;
                        if (fragmentLength == 0)
                        {
                            return result.Length > 0 ? result.ToString() : null;
                        }

                        for (int i = 0; i < fragmentLength; i++)
                        {
                            result.Append((char)reply[start + 5 + i]);
                        }

                        offset += fragmentLength;
                        frameFound = true;
                        break;
                    }

                    if (!frameFound) return null;
                }
            }

            return result.Length > 0 ? result.ToString() : null;
        }
    }
}
