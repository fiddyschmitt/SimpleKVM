using System.Text;

namespace SimpleKVM.Tests;

/// <summary>Builds a valid 128-byte EDID base block with the fields the app reads.</summary>
public static class TestEdid
{
    public static byte[] Build(string pnpId = "DEL", ushort productCode = 0x4AA0, uint serialNumber = 123456, string? modelName = "DELL U2412M", string? serialString = "ABC123")
    {
        var edid = new byte[128];
        new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 }.CopyTo(edid, 0);

        int id = ((pnpId[0] - 'A' + 1) << 10) | ((pnpId[1] - 'A' + 1) << 5) | (pnpId[2] - 'A' + 1);
        edid[8] = (byte)(id >> 8);
        edid[9] = (byte)id;
        edid[10] = (byte)productCode;
        edid[11] = (byte)(productCode >> 8);
        edid[12] = (byte)serialNumber;
        edid[13] = (byte)(serialNumber >> 8);
        edid[14] = (byte)(serialNumber >> 16);
        edid[15] = (byte)(serialNumber >> 24);
        edid[18] = 1;   //EDID 1.4
        edid[19] = 4;

        int descriptor = 54;
        if (modelName != null) WriteDescriptor(edid, descriptor, 0xFC, modelName);
        descriptor += 18;
        if (serialString != null) WriteDescriptor(edid, descriptor, 0xFF, serialString);

        int sum = 0;
        for (int i = 0; i < 127; i++) sum += edid[i];
        edid[127] = (byte)((256 - (sum & 0xFF)) & 0xFF);
        return edid;
    }

    static void WriteDescriptor(byte[] edid, int offset, byte tag, string text)
    {
        edid[offset + 3] = tag;
        var bytes = Encoding.ASCII.GetBytes((text + "\n").PadRight(13, ' '));
        Array.Copy(bytes, 0, edid, offset + 5, 13);
    }

    public static string Hex(byte[] edid) => Convert.ToHexString(edid).ToLowerInvariant();
}
