// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.Compression;

namespace RedFox.Tests.Compression;

public sealed class CompressionCoreTests
{
    private sealed class SpyCodec : CompressionCodec
    {
        public int LastCompressSourceLength { get; private set; }

        public int LastDecompressSourceLength { get; private set; }

        public int LastCompressDictionaryLength { get; private set; }

        public int LastDecompressDictionaryLength { get; private set; }

        public override CompressionCodecFlags Flags => CompressionCodecFlags.SupportsDictionaries;

        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            LastCompressSourceLength = source.Length;
            source.CopyTo(destination);
            return source.Length;
        }

        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        {
            LastCompressSourceLength = source.Length;
            LastCompressDictionaryLength = dictionary.Length;
            source.CopyTo(destination);
            return source.Length;
        }

        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            LastDecompressSourceLength = source.Length;
            source.CopyTo(destination);
            return source.Length;
        }

        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        {
            LastDecompressSourceLength = source.Length;
            LastDecompressDictionaryLength = dictionary.Length;
            source.CopyTo(destination);
            return source.Length;
        }

        public override int GetMaxCompressedSize(int inputSize) => inputSize + 8;

        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer) => compressedBuffer.Length;
    }

    [Fact]
    public void PassThroughCodec_SpanRoundTrip()
    {
        PassThroughCodec codec = new();
        byte[] source = [1, 2, 3, 4];
        Span<byte> compressed = stackalloc byte[4];
        Span<byte> decompressed = stackalloc byte[4];

        int compressedSize = codec.Compress(source, compressed);
        int decompressedSize = codec.Decompress(compressed[..compressedSize], decompressed);

        Assert.Equal(source.Length, compressedSize);
        Assert.Equal(source.Length, decompressedSize);
        Assert.Equal(source, decompressed.ToArray());
        Assert.Equal(CompressionCodecFlags.None, codec.Flags);
        Assert.Equal(source.Length, codec.GetMaxCompressedSize(source.Length));
        Assert.Equal(source.Length, codec.GetDecompressedSize(compressed[..compressedSize]));
    }

    [Fact]
    public void PassThroughCodec_ArrayOverloads_UseOffsets()
    {
        PassThroughCodec codec = new();
        byte[] source = [10, 11, 12, 13, 14, 15];
        byte[] compressed = new byte[12];
        byte[] decompressed = new byte[12];
        byte[] dictionary = [1, 2, 3];

        int compressedSize = codec.Compress(source, 1, 4, compressed, 3, dictionary);
        int decompressedSize = codec.Decompress(compressed, 3, compressedSize, decompressed, 5, dictionary);

        Assert.Equal(4, compressedSize);
        Assert.Equal(4, decompressedSize);
        Assert.Equal([11, 12, 13, 14], decompressed.AsSpan(5, 4).ToArray());
    }

    [Fact]
    public void PassThroughCodec_WithShortDestination_ThrowsCompressionException()
    {
        PassThroughCodec codec = new();
        byte[] source = [1, 2, 3];
        byte[] destination = new byte[2];

        CompressionException exception = Assert.Throws<CompressionException>(() => codec.Compress(source, destination.AsSpan()));
        Assert.Contains("Destination buffer is not large enough", exception.Message);
    }

    [Fact]
    public void CompressionException_Constructors_SetExpectedData()
    {
        CompressionException defaultException = new();
        Assert.NotNull(defaultException.Message);

        CompressionException messageException = new("fail");
        Assert.Equal("fail", messageException.Message);

        InvalidOperationException innerException = new("inner");
        CompressionException withInner = new("outer", innerException);
        Assert.Equal("outer", withInner.Message);
        Assert.Same(innerException, withInner.InnerException);

        CompressionException detailed = new("failed", "compression", "bad status");
        Assert.Contains("failed", detailed.Message);
        Assert.Contains("compression", detailed.Message);
        Assert.Contains("bad status", detailed.Message);
    }

    [Fact]
    public void CompressionCodec_ArrayOverloads_InvokeSpanImplementations()
    {
        SpyCodec codec = new();
        byte[] source = [1, 2, 3, 4, 5, 6];
        byte[] destination = new byte[16];
        byte[] dictionary = [9, 9];

        int compressSize = codec.Compress(source, 1, 3, destination, 2, dictionary);
        int decompressSize = codec.Decompress(destination, 2, compressSize, destination, 6, dictionary);

        Assert.Equal(3, compressSize);
        Assert.Equal(3, decompressSize);
        Assert.Equal(3, codec.LastCompressSourceLength);
        Assert.Equal(2, codec.LastCompressDictionaryLength);
        Assert.Equal(3, codec.LastDecompressSourceLength);
        Assert.Equal(2, codec.LastDecompressDictionaryLength);
    }

    [Fact]
    public void CompressionCodecFlags_HasExpectedBitValues()
    {
        CompressionCodecFlags combined = CompressionCodecFlags.SupportsDictionaries | CompressionCodecFlags.SupportsKnownSize;

        Assert.Equal(0, (int)CompressionCodecFlags.None);
        Assert.True(combined.HasFlag(CompressionCodecFlags.SupportsDictionaries));
        Assert.True(combined.HasFlag(CompressionCodecFlags.SupportsKnownSize));
    }
}
