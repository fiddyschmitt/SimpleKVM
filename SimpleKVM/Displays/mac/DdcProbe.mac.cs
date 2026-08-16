using SimpleKVM.Displays.I2C;
using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;

namespace SimpleKVM.Displays.mac
{
    /// <summary>
    /// The --probe-ddc diagnostic: verifies the private IOAVService symbols resolve,
    /// enumerates external DCPAVServiceProxy nodes, and exercises EDID + VCP 0x60
    /// read/write against the attached monitor. This is the go/no-go test for DDC
    /// on this Mac's video port.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class DdcProbe
    {
        const uint DdcChipAddress = 0x37;       //7-bit DDC/CI slave
        const uint DdcDataAddress = 0x51;       //standard DDC source byte, sent as the I2C register/offset

        public static int Run()
        {
            Console.WriteLine("=== SimpleKVM DDC probe ===");

            //1. Private symbol resolution
            if (!IOKitNative.ResolveAvServiceSymbols())
            {
                Console.WriteLine("FAIL: could not resolve IOAVService symbols in any candidate framework.");
                return 1;
            }
            Console.WriteLine($"IOAVService symbols resolved from: {IOKitNative.AvServiceSymbolSource}");

            //2. Enumerate DCPAVServiceProxy nodes
            var matching = IOKitNative.IOServiceMatching("DCPAVServiceProxy");
            int status = IOKitNative.IOServiceGetMatchingServices(0, matching, out uint iterator);
            if (status != 0)
            {
                Console.WriteLine($"FAIL: IOServiceGetMatchingServices returned {status}");
                return 1;
            }

            int externalCount = 0;
            int result = 1;

            uint service;
            while ((service = IOKitNative.IOIteratorNext(iterator)) != 0)
            {
                try
                {
                    IOKitNative.IORegistryEntryGetRegistryEntryID(service, out ulong entryId);
                    var location = IOKitNative.GetRegistryStringProperty(service, "Location") ?? "(no Location)";
                    Console.WriteLine($"DCPAVServiceProxy id=0x{entryId:X} Location={location}");

                    if (location != "External") continue;
                    externalCount++;

                    if (ProbeService(service) == 0)
                    {
                        result = 0;
                    }
                }
                finally
                {
                    IOKitNative.IOObjectRelease(service);
                }
            }
            IOKitNative.IOObjectRelease(iterator);

            if (externalCount == 0)
            {
                Console.WriteLine("FAIL: no DCPAVServiceProxy with Location=External found. Is an external monitor attached?");
                return 1;
            }

            Console.WriteLine(result == 0
                ? "=== PROBE PASSED: DDC read/write works on this port ==="
                : "=== PROBE FAILED: see messages above (if on built-in HDMI, retry via a USB-C adapter) ===");
            return result;
        }

        static int ProbeService(uint service)
        {
            var avService = IOKitNative.AVServiceCreateWithService(service);
            if (avService == IntPtr.Zero)
            {
                Console.WriteLine("  FAIL: IOAVServiceCreateWithService returned null");
                return 1;
            }

            //3. EDID via the AV service (read-only sanity check of the I2C path)
            var edid = IOKitNative.AVServiceCopyEDID(avService);
            if (edid != null && edid.Length >= 16)
            {
                var mfg = (ushort)((edid[8] << 8) | edid[9]);
                var product = (ushort)(edid[10] | (edid[11] << 8));
                Console.WriteLine($"  EDID: {edid.Length} bytes, manufacturer=0x{mfg:X4} ({DecodeMfg(mfg)}), product=0x{product:X4}");
            }
            else
            {
                Console.WriteLine("  EDID: not available via IOAVServiceCopyEDID (continuing)");
            }

            //4. VCP 0x60 read
            var current = TryReadVcp(avService, 0x60, out string detail);
            Console.WriteLine($"  VCP 0x60 read: {detail}");
            if (current == null) return 1;

            //5. Safe write test: set input source to its current value
            var msg = DdcCiMessage.BuildSetVcp(0x51, 0x60, (uint)current.Value);
            int writeStatus = 0;
            for (int i = 0; i < 2; i++)     //m1ddc sends every write twice (DDC_ITERATIONS)
            {
                Thread.Sleep(10);
                writeStatus = IOKitNative.AVServiceWriteI2C(avService, DdcChipAddress, msg[0], msg.Skip(1).ToArray());
                if (writeStatus != 0) break;
            }
            Console.WriteLine($"  VCP 0x60 no-op write (value {current.Value}): status={writeStatus} ({(writeStatus == 0 ? "OK" : "FAIL")})");

            return writeStatus == 0 ? 0 : 1;
        }

        public static int? TryReadVcp(IntPtr avService, byte vcpCode, out string detail)
        {
            detail = "no attempt made";

            for (int attempt = 0; attempt < 3; attempt++)
            {
                //DDC/CI GetVCP request, framed exactly like m1ddc: the source byte (0x51) goes out
                //as the I2C data address, and the request checksum is seeded with 0x6E only
                byte[] request = [0x82, 0x01, vcpCode, 0];
                request[3] = (byte)(0x6E ^ request[0] ^ request[1] ^ request[2]);

                int writeStatus = 0;
                for (int i = 0; i < 2; i++)     //m1ddc sends every write twice (DDC_ITERATIONS)
                {
                    Thread.Sleep(10);
                    writeStatus = IOKitNative.AVServiceWriteI2C(avService, DdcChipAddress, DdcDataAddress, request);
                    if (writeStatus != 0) break;
                }

                Thread.Sleep(50);

                //The DCP sometimes prepends stale bytes before the actual frame, so read generously
                //and scan for a frame that checksums correctly
                var reply = new byte[24];
                int readStatus = IOKitNative.AVServiceReadI2C(avService, DdcChipAddress, DdcDataAddress, reply);

                if (writeStatus != 0 || readStatus != 0)
                {
                    detail = $"write status={writeStatus}, read status={readStatus}";
                    Thread.Sleep(100);
                    continue;
                }

                var hex = string.Join(" ", reply.Select(b => b.ToString("X2")));

                //Standard reply frame: [0x6E][len|0x80][0x02][result][vcp][type][maxH][maxL][curH][curL][chk],
                //where chk = 0x50 ^ all preceding frame bytes
                for (int start = 0; start + 11 <= reply.Length; start++)
                {
                    if (reply[start] != 0x6E || reply[start + 1] != 0x88 || reply[start + 2] != 0x02) continue;
                    if (reply[start + 4] != vcpCode) continue;

                    byte checksum = 0x50;
                    for (int i = start; i < start + 10; i++) checksum ^= reply[i];
                    if (checksum != reply[start + 10]) continue;

                    if (reply[start + 3] != 0) break;   //result code: unsupported VCP

                    int value = (reply[start + 8] << 8) | reply[start + 9];
                    detail = $"OK, current value={value} (frame at offset {start}, raw: {hex})";
                    return value;
                }

                detail = $"no valid frame found (raw: {hex})";
                Thread.Sleep(100);
            }

            detail += " — giving up after 3 attempts";
            return null;
        }

        static string DecodeMfg(ushort mfg)
        {
            return new string(
            [
                (char)('A' - 1 + ((mfg >> 10) & 0x1F)),
                (char)('A' - 1 + ((mfg >> 5) & 0x1F)),
                (char)('A' - 1 + (mfg & 0x1F)),
            ]);
        }
    }
}
