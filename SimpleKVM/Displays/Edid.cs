using System;
using System.Text;

namespace SimpleKVM.Displays
{
    /// <summary>
    /// Reads the fields of an EDID base block that the app cares about: the identity used to
    /// pair a monitor seen by one API with the same monitor seen by another, and the model
    /// name shown to the user. All accessors tolerate short or garbage input and answer with
    /// zero, null or false rather than throwing.
    /// </summary>
    public static class Edid
    {
        public const int BaseBlockLength = 128;

        public static bool IsValid(byte[]? edid)
        {
            return edid != null && edid.Length >= BaseBlockLength && edid[0] == 0x00 && edid[1] == 0xFF && edid[2] == 0xFF && edid[3] == 0xFF
                && edid[4] == 0xFF && edid[5] == 0xFF && edid[6] == 0xFF && edid[7] == 0x00;
        }

        /// <summary>Bytes 8-9 as the big-endian value the transports compare, e.g. 0x1E6D for LG.</summary>
        public static ushort ManufacturerId(byte[] edid)
        {
            return edid.Length >= 10 ? (ushort)((edid[8] << 8) | edid[9]) : (ushort)0;
        }

        /// <summary>The three-letter PNP id packed into bytes 8-9, e.g. "DEL", "GSM", "ACR".</summary>
        public static string PnpId(byte[] edid)
        {
            var id = ManufacturerId(edid);
            if (id == 0) return "";
            return new string(
            [
                (char)('A' - 1 + ((id >> 10) & 0x1F)),
                (char)('A' - 1 + ((id >> 5) & 0x1F)),
                (char)('A' - 1 + (id & 0x1F)),
            ]);
        }

        public static ushort ProductCode(byte[] edid)
        {
            return edid.Length >= 12 ? (ushort)(edid[10] | (edid[11] << 8)) : (ushort)0;
        }

        public static uint SerialNumber(byte[] edid)
        {
            return edid.Length >= 16 ? (uint)(edid[12] | (edid[13] << 8) | (edid[14] << 16) | (edid[15] << 24)) : 0u;
        }

        /// <summary>The display name from the 0xFC descriptor, e.g. "S240HL" or "DELL U2412M".</summary>
        public static string? ModelName(byte[] edid) => Descriptor(edid, 0xFC);

        /// <summary>The serial number string from the 0xFF descriptor, when the monitor has one.</summary>
        public static string? SerialString(byte[] edid) => Descriptor(edid, 0xFF);

        /// <summary>
        /// The base block as hex: the key two readings of the same monitor share. Two monitors
        /// of the same model usually differ in their serial number bytes, but not always (see
        /// <see cref="IsAmbiguous"/>).
        /// </summary>
        public static string Key(byte[] edid)
        {
            return Convert.ToHexString(edid, 0, Math.Min(edid.Length, BaseBlockLength));
        }

        /// <summary>
        /// The 18-byte descriptors at offsets 54, 72, 90 and 108 hold text when they start with
        /// two zero bytes and carry the tag in byte 3; the text is 13 bytes, newline-terminated.
        /// </summary>
        static string? Descriptor(byte[] edid, byte tag)
        {
            foreach (int offset in new[] { 54, 72, 90, 108 })
            {
                if (offset + 18 > edid.Length) break;
                if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 3] != tag) continue;

                var text = Encoding.ASCII.GetString(edid, offset + 5, 13);
                int newline = text.IndexOf('\n');
                if (newline >= 0) text = text[..newline];
                text = text.Trim();

                return text.Length > 0 ? text : null;
            }

            return null;
        }
    }
}
