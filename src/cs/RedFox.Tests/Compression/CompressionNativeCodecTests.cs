// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using RedFox.Compression;
using RedFox.Compression.Deflate;
using RedFox.Compression.LZ4;
using RedFox.Compression.ZStandard;

namespace RedFox.Tests.Compression;

public sealed class CompressionNativeCodecTests
{
    [Fact]
    public void DeflateCodec_RoundTrip_WhenNativeAvailable()
    {
        if (!HasNativeLibrary("miniz.dll"))
        {
            return;
        }

        DeflateCodec codec = new();
        byte[] source = Encoding.UTF8.GetBytes("RedFox-Deflate-Codec-Data");
        byte[] compressed = new byte[codec.GetMaxCompressedSize(source.Length)];

        int compressedSize = codec.Compress(source, compressed);
        byte[] decompressed = new byte[source.Length];
        int decompressedSize = codec.Decompress(compressed.AsSpan(0, compressedSize), decompressed);

        Assert.Equal(source.Length, decompressedSize);
        Assert.Equal(source, decompressed);
        Assert.Equal(CompressionCodecFlags.None, codec.Flags);
        Assert.True(codec.GetMaxCompressedSize(source.Length) >= source.Length);
        Assert.Throws<NotSupportedException>(() => codec.GetDecompressedSize(compressed.AsSpan(0, compressedSize)));
        Assert.Throws<NotSupportedException>(() => codec.Decompress(compressed, decompressed, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Lz4Codec_RoundTrip_WhenNativeAvailable()
    {
        if (!HasNativeLibrary("liblz4.dll"))
        {
            return;
        }

        LZ4Codec codec = new();
        byte[] source = Encoding.UTF8.GetBytes("RedFox-LZ4-Codec-Data");
        byte[] compressed = new byte[codec.GetMaxCompressedSize(source.Length)];

        int compressedSize = codec.Compress(source, compressed);
        byte[] decompressed = new byte[source.Length];
        int decompressedSize = codec.Decompress(compressed.AsSpan(0, compressedSize), decompressed);

        Assert.Equal(source.Length, decompressedSize);
        Assert.Equal(source, decompressed);
        Assert.Equal(CompressionCodecFlags.None, codec.Flags);
        Assert.Throws<NotSupportedException>(() => codec.GetDecompressedSize(compressed.AsSpan(0, compressedSize)));
        Assert.Throws<NotSupportedException>(() => codec.Compress(source, compressed, ReadOnlySpan<byte>.Empty));
        Assert.Throws<NotSupportedException>(() => codec.Decompress(compressed, decompressed, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void ZStandardCodec_RoundTrip_WhenNativeAvailable()
    {
        if (!HasNativeLibrary("libzstd.dll"))
        {
            return;
        }

        ZStandardCodec codec = new() { CompressionLevel = 3 };
        byte[] source = Encoding.UTF8.GetBytes("RedFox-Zstd-Codec-Data");
        byte[] compressed = new byte[codec.GetMaxCompressedSize(source.Length)];

        int compressedSize = codec.Compress(source, compressed);
        byte[] decompressed = new byte[source.Length];
        int decompressedSize = codec.Decompress(compressed.AsSpan(0, compressedSize), decompressed);

        Assert.Equal(source.Length, decompressedSize);
        Assert.Equal(source, decompressed);
        Assert.Equal(source.Length, codec.GetDecompressedSize(compressed.AsSpan(0, compressedSize)));
        Assert.Equal(CompressionCodecFlags.SupportsKnownSize, codec.Flags);
        Assert.Throws<NotSupportedException>(() => codec.Decompress(compressed, decompressed, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void DeflateLevel_EnumValues_AreStable()
    {
        Assert.Equal(0, (int)DeflateLevel.NoCompression);
        Assert.Equal(1, (int)DeflateLevel.BestSpeed);
        Assert.Equal(9, (int)DeflateLevel.BestCompression);
        Assert.Equal(10, (int)DeflateLevel.UberCompression);
        Assert.Equal(6, (int)DeflateLevel.DefaultLevel);
        Assert.Equal(-1, (int)DeflateLevel.DefaultCompression);
    }

    private static bool HasNativeLibrary(string fileName)
    {
        string libraryPath = Path.Combine(AppContext.BaseDirectory, "Native", fileName);
        return File.Exists(libraryPath);
    }
}
