namespace RedFox.Graphics2D.Png;

internal static class PngCrc
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static uint ComputeCrc(ReadOnlySpan<byte> typeBytes, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    }
}
