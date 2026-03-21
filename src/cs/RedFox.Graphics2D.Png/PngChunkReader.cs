using System.Buffers.Binary;
using System.Text;

namespace RedFox.Graphics2D.Png;

internal static class PngChunkReader
{
    public static PngChunk ReadChunk(Stream stream)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        stream.ReadExactly(lengthBytes);
        uint length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        if (length > int.MaxValue)
        {
            throw new InvalidDataException("PNG chunk is too large.");
        }
        Span<byte> typeBytes = stackalloc byte[4];
        stream.ReadExactly(typeBytes);
        string type = Encoding.ASCII.GetString(typeBytes);
        byte[] data = new byte[(int)length];
        if (length > 0)
        {
            stream.ReadExactly(data);
        }
        Span<byte> crcBytes = stackalloc byte[4];
        stream.ReadExactly(crcBytes);
        uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(crcBytes);
        uint actualCrc = PngCrc.ComputeCrc(typeBytes, data);
        if (expectedCrc != actualCrc)
        {
            throw new InvalidDataException($"CRC mismatch in chunk '{type}'.");
        }
        return new PngChunk(type, data);
    }
}
