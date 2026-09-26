using System;
using System.IO;
using System.IO.Compression;

namespace RedFox.Imaging.Formats.Png;

internal static class PngDecompressor
{
    public static byte[] InflateZlib(byte[] compressed, long expectedLength)
    {
        ArgumentNullException.ThrowIfNull(compressed);
        if (expectedLength < 0)
            throw new InvalidDataException("The PNG decompressed size is negative.");
        if (expectedLength > Array.MaxLength)
            throw new InvalidDataException($"PNG image data would inflate to {expectedLength} bytes, which exceeds the maximum buffer size.");

        using MemoryStream source = new(compressed);
        using ZLibStream zlib = new(source, CompressionMode.Decompress);
        byte[] output = new byte[expectedLength];

        if (zlib.ReadAtLeast(output, output.Length, throwOnEndOfStream: false) < output.Length)
            throw new InvalidDataException("Unexpected end of PNG image data.");
        if (zlib.ReadByte() >= 0)
            throw new InvalidDataException("Unexpected trailing data in PNG image data.");

        return output;
    }
}
