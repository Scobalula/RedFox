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
    [Fact]
    public void PassThroughBuffersAndAllocationLimits()
    {
        using PassThroughCodec codec = new();
        byte[] source = [1, 2, 3, 4];
        Assert.Equal(source, codec.Decompress(codec.Compress(source)));
        Assert.Equal([2, 3], codec.Compress(source.AsSpan(1, 2)));
        Assert.True(codec.TryGetDecompressedSize(source, out long size));
        Assert.Equal(4, size);
        Assert.Throws<CompressionException>(() => codec.Compress(source, new byte[3]));
        Assert.Throws<CompressionException>(() => codec.Decompress(source, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Decompress(source, null, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Decompress(source, -1));
        Assert.Empty(codec.Decompress([]));
    }

    [Fact]
    public async Task PassThroughStreamPreservesSeekingAndReadBoundary()
    {
        using PassThroughCodec codec = new();
        using MemoryStream source = new([0, 1, 2, 3, 4, 5]);
        source.Position = 1;
        await using Stream reader = codec.CreateDecompressionStream(source, true, 3);
        Assert.True(reader.CanSeek);
        Assert.Equal(3, reader.Length);
        byte[] output = new byte[8];
        Assert.Equal(3, await reader.ReadAsync(output));
        Assert.Equal(4, source.Position);
        Assert.Equal(0, reader.Read(output));
        reader.Seek(-2, SeekOrigin.End);
        Assert.Equal(2, reader.ReadByte());
        Assert.Throws<IOException>(() => reader.Seek(4, SeekOrigin.Begin));
        await reader.DisposeAsync();
        Assert.True(source.CanRead);
    }

    [Fact]
    public void CapabilityFlagsHaveIndependentBits()
    {
        int combined = 0;
        foreach (CompressionCodecFlags flag in Enum.GetValues<CompressionCodecFlags>())
        {
            int value = (int)flag;
            Assert.Equal(0, value & (value - 1));
            Assert.Equal(0, combined & value);
            combined |= value;
        }
        Assert.Equal(63, combined);
    }

    [Fact]
    public void CompressionExceptionKeepsNativeDetailsAndInnerException()
    {
        InvalidOperationException inner = new("inner");
        Assert.Same(inner, new CompressionException("outer", inner).InnerException);
        Assert.Contains("bad status", new CompressionException("failed", "decompression", "bad status").Message);
    }
}
