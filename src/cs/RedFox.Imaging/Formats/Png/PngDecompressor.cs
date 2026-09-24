using System;
using System.IO;
using System.IO.Compression;

namespace RedFox.Imaging.Formats.Png;

internal static class PngDecompressor
{
    public static byte[] InflateZlib(byte[] compressed)
    {
        using MemoryStream source = new(compressed);
        using ZLibStream zlib = new(source, CompressionMode.Decompress);
        using MemoryStream output = new();
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
