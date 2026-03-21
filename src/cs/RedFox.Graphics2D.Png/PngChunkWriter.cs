using System;
using System.IO;
using System.IO.Compression;

namespace RedFox.Graphics2D.Png;

internal static class PngChunkWriter
{
    public static void WriteCompressedIdat(Stream stream, ReadOnlySpan<byte> data, CompressionLevel compressionLevel)
    {
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, compressionLevel, leaveOpen: true))
        {
            zlib.Write(data);
        }
        if (compressed.TryGetBuffer(out ArraySegment<byte> segment) && segment.Array is not null)
        {
            WriteChunkSplit(stream, "IDAT", segment.Array.AsSpan(segment.Offset, (int)compressed.Length));
        }
        else
        {
            WriteChunkSplit(stream, "IDAT", compressed.ToArray());
        }
    }

    public static void WriteCompressedIdatFilterNoneRgba(Stream stream, ReadOnlySpan<byte> rgba, int width, int height, CompressionLevel compressionLevel)
    {
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, compressionLevel, leaveOpen: true))
        {
            int rowBytes = width * 4;
            byte[] rowBuffer = new byte[rowBytes + 1];
            rowBuffer[0] = 0;
            for (int y = 0; y < height; y++)
            {
                rgba.Slice(y * rowBytes, rowBytes).CopyTo(rowBuffer.AsSpan(1));
                zlib.Write(rowBuffer);
            }
        }
        if (compressed.TryGetBuffer(out ArraySegment<byte> segment) && segment.Array is not null)
        {
            WriteChunkSplit(stream, "IDAT", segment.Array.AsSpan(segment.Offset, (int)compressed.Length));
        }
        else
        {
            WriteChunkSplit(stream, "IDAT", compressed.ToArray());
        }
    }

    public static void WriteChunkSplit(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            int length = Math.Min(PngConstants.MaxChunkWriteSize, data.Length - offset);
            WriteChunk(stream, type, data.Slice(offset, length));
            offset += length;
        }
    }

    public static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> typeBytes = [(byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3]];
        Span<byte> lengthBytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)data.Length);
        stream.Write(lengthBytes);
        stream.Write(typeBytes);
        if (!data.IsEmpty)
        {
            stream.Write(data);
        }
        uint crc = PngCrc.ComputeCrc(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    public static void WriteIHDR(Stream stream, int width, int height, byte bitDepth, byte colorType, byte interlaceMethod)
    {
        Span<byte> data = stackalloc byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data[0..4], (uint)width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data[4..8], (uint)height);
        data[8] = bitDepth;
        data[9] = colorType;
        data[10] = 0;
        data[11] = 0;
        data[12] = interlaceMethod;
        WriteChunk(stream, "IHDR", data);
    }

    public static void WriteSRGB(Stream stream, byte renderingIntent)
    {
        Span<byte> data = [renderingIntent];
        WriteChunk(stream, "sRGB", data);
    }
}
