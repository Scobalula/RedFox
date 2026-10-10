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
using RedFox.Compression.GDeflate;
using RedFox.Compression.LZ4;
using RedFox.Compression.ZStandard;

namespace RedFox.Tests.Compression;

public sealed class CompressionNativeCodecTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void BufferRoundTripsReuseContextsAndRejectSmallDestinations(int format)
    {
        using CompressionCodec codec = CreateCodec(format, []);
        byte[] source = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("RedFox reusable compression context. ", 2000)));
        for (int i = 0; i < 3; i++)
        {
            byte[] compressed = codec.Compress(source);
            Assert.Equal(source, codec.Decompress(compressed, source.Length));
            Assert.Throws<CompressionException>(() => codec.Decompress(compressed, new byte[source.Length - 1]));
        }

        Assert.Empty(codec.Decompress(codec.Compress([]), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.GetMaxCompressedSize(-1));
        Assert.True(codec.Flags.HasFlag(CompressionCodecFlags.Compression));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void StatefulNativeBufferOperationsAreSafeWhenSharedAcrossThreads(int format)
    {
        using CompressionCodec codec = CreateCodec(format, []);
        byte[] source = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Mesh preview decompression uses a shared codec. ", 200)));
        byte[] compressed = codec.Compress(source);

        Parallel.For(0, 64, _ => Assert.Equal(source, codec.Decompress(compressed, source.Length)));
        Parallel.For(0, 64, _ => Assert.Equal(source, codec.Decompress(codec.Compress(source), source.Length)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void DictionaryIsCopiedAndReused(int format)
    {
        byte[] dictionary = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Dictionary history used for RedFox archive blocks. ", 300)));
        byte[] original = dictionary.ToArray();
        byte[] source = dictionary.AsSpan(200, 3000).ToArray();
        using CompressionCodec encoder = CreateCodec(format, dictionary);
        Array.Fill(dictionary, (byte)0);
        using CompressionCodec decoder = CreateCodec(format, original);
        for (int i = 0; i < 3; i++)
            Assert.Equal(source, decoder.Decompress(encoder.Compress(source), source.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task StreamsHandleTinyIoFlushOwnershipLimitsAndCancellation(int format)
    {
        CompressionCodec codec = CreateCodec(format, []);
        byte[] data = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Small stream writes and reads. ", 3000)));
        using MemoryStream output = new();
        await using (Stream writer = codec.CreateCompressionStream(output, true, data.Length))
        {
            for (int i = 0; i < data.Length; i += 17)
            {
                await writer.WriteAsync(data.AsMemory(i, Math.Min(17, data.Length - i)));
                if (i == 170)
                    await writer.FlushAsync();
            }
        }

        byte[] compressed = output.ToArray();
        byte[] embedded = [.. compressed, 0xCA, 0xFE];
        using ShortReadStream input = new(embedded);
        await using Stream reader = codec.CreateDecompressionStream(input, true, compressed.Length);
        codec.Dispose();
        byte[] restored = new byte[data.Length];
        for (int i = 0; i < restored.Length;)
            i += await reader.ReadAsync(restored.AsMemory(i, Math.Min(7, restored.Length - i)));

        Assert.Equal(-1, reader.ReadByte());
        Assert.Equal(data, restored);
        Assert.Equal(compressed.Length, input.BytesRead);
        Assert.True(input.AsyncRead);
        Assert.False(reader.CanSeek);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ReadExactlyAsync(new byte[1], cancellation.Token));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void ConcatenatedFramesAndTruncation(int format)
    {
        using CompressionCodec codec = CreateCodec(format, []);
        byte[] first = codec.Compress("first"u8);
        byte[] second = codec.Compress("second"u8);
        byte[] combined = [.. first, .. second];
        Assert.Equal("firstsecond"u8.ToArray(), codec.Decompress(combined, 11));
        using Stream reader = codec.CreateDecompressionStream(new ShortReadStream(combined));
        using MemoryStream output = new();
        reader.CopyTo(output);
        Assert.Equal("firstsecond"u8.ToArray(), output.ToArray());
        using Stream truncated = codec.CreateDecompressionStream(new ShortReadStream(combined[..^1]));
        Assert.Throws<CompressionException>(() => truncated.CopyTo(Stream.Null));
        if (format != 2)
        {
            Assert.True(codec.TryGetDecompressedSize(combined, out long size));
            Assert.Equal(11, size);
        }
    }

    [Fact]
    public void NativeLibrariesArePackaged()
    {
        foreach (string library in new[] { "zlib1.dll", "liblz4.dll", "libzstd.dll", "libgdeflate.dll" })
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, library)), $"Missing bundled DLL: {library}");
    }

    [Fact]
    public void ZlibDecodesIndependentPythonFixtures()
    {
        byte[] source = "hello world"u8.ToArray();
        using DeflateCodec raw = new();
        using ZLibCodec zlib = new();
        using GZipCodec gzip = new();
        Assert.Equal(source, raw.Decompress(Convert.FromHexString("cb48cdc9c95728cf2fca490100"), 11));
        Assert.Equal(source, zlib.Decompress(Convert.FromHexString("789ccb48cdc9c95728cf2fca4901001a0b045d"), 11));
        Assert.Equal(source, gzip.Decompress(Convert.FromHexString("1f8b0800000000000203cb48cdc9c95728cf2fca49010085114a0d0b000000"), 11));
    }

    [Fact]
    public void ZstandardTrainingPreservesSampleBoundaries()
    {
        byte[][] samples = Enumerable.Range(0, 500).Select(i => Encoding.UTF8.GetBytes($"Asset {i}: textures/models/materials/archive data repeated {i % 12}. " + new string((char)('a' + i % 20), 100))).ToArray();
        byte[] dictionary = ZStandardCodec.TrainDictionary(samples, 4096);
        Assert.InRange(ZStandardCodec.TrainDictionary(samples).Length, 1, 65536);
        using ZStandardCodec codec = new(3, dictionary);
        Assert.Equal(samples[20], codec.Decompress(codec.Compress(samples[20])));
        Assert.Throws<ArgumentException>(() => ZStandardCodec.TrainDictionary(new byte[10], new[] { 9 }, new byte[100]));
        Assert.Throws<CompressionException>(() => ZStandardCodec.TrainDictionary(new byte[10], new[] { 10 }, new byte[100]));
    }

    private static CompressionCodec CreateCodec(int format, byte[] dictionary) => format switch
    {
        0 => new DeflateCodec(DeflateLevel.DefaultCompression, dictionary),
        1 => new ZLibCodec(DeflateLevel.DefaultCompression, dictionary),
        2 => new GZipCodec(),
        3 => new LZ4Codec(dictionary),
        4 => new LZ4FrameCodec(dictionary),
        5 => new ZStandardCodec(3, dictionary),
        6 => new GDeflateCodec(),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
