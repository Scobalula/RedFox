using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace RedFox.Imaging.Formats.Png;

internal static class PngChunkReader
{
    public static PngChunk ReadChunk(Stream stream)
    {
        Span<byte> typeBytes = stackalloc byte[4];
        int length = ReadChunkHeader(stream, typeBytes);
        return ReadChunkBody(stream, typeBytes, length);
    }

    public static int ReadChunkHeader(Stream stream, Span<byte> typeBytes)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        stream.ReadExactly(lengthBytes);
        uint length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);

        if (length > int.MaxValue || (stream.CanSeek && length > stream.Length - stream.Position))
            throw new InvalidDataException("PNG chunk length exceeds the available data.");

        stream.ReadExactly(typeBytes);
        return (int)length;
    }

    public static PngChunk ReadChunkBody(Stream stream, ReadOnlySpan<byte> typeBytes, int length)
    {
        string type = Encoding.ASCII.GetString(typeBytes);
        byte[] data = new byte[length];
        stream.ReadExactly(data);

        Span<byte> crcBytes = stackalloc byte[4];
        stream.ReadExactly(crcBytes);
        uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(crcBytes);
        uint actualCrc = PngCrc.ComputeCrc(typeBytes, data);

        if (expectedCrc != actualCrc)
            throw new InvalidDataException($"CRC mismatch in chunk '{type}'.");

        return new PngChunk(type, data);
    }
}
