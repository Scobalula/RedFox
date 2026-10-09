using System.Buffers.Binary;
using RedFox.Compression;
using RedFox.Compression.Deflate;
using RedFox.Compression.GDeflate;
using RedFox.Compression.LZ4;
using RedFox.Compression.ZStandard;

namespace RedFox.Tests.Compression;

public sealed class CompressionInteroperabilityTests
{
    // These vectors were produced independently of the codecs under test.
    private static readonly byte[] GDeflateFixture = Convert.FromBase64String(
        "BPsCAAHiAAAEAQAABAEAAAsqAQCppQAAKUUAAG6pAAD5egAAFVwAAApBAACZo9T/eQAAACkAAACpAAAA" +
        "BQAAAKkAAABAAgAAeQAAACUAAAAKAAAAyQAAAPkAAAC5AAAABQAAAEUAAACpAAAAxQAAAMUAAACZAAAA" +
        "+QAAAHkAAAAKAAAAaQAAAJkAAAAVAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAOoAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAy8sDACUpAAAKqQAAyQUAAPmpAAC5QAIA" +
        "BXkAAEWjVDipAAAAxQAAAMUAAACZAAAA+QAAAHkAAAAKAAAAaQAAAJkAAAAVAAAAJQAAAKUAAABFAAAA" +
        "qQAAAHoAAABcAAAAQQAAAKkAAAApAAAAbgAAAPkAAAAVAAAACgAAAJkAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAA6gAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");

    private static readonly byte[] Lz4Fixture = Convert.FromBase64String(
        "BCJNGHxQgDgBAAAAAAAdbAEAAP8ZUmVkRm94IGluZGVwZW5kZW50IGNvbXByZXNzaW9uIGZpeHR1cmUu" +
        "CigA////////////////////////////////////////////////////////////////////////////" +
        "////////////////////////////////////////////////////////////////////////////////" +
        "////////////////////////////////////////////////////////////////////////////////" +
        "////////////////////////////////////////////////////////////////////////////////" +
        "////////////////////////////////////////////////////////////////////////////////" +
        "/////////////////////3lQdXJlLgp5OfQ5AAAAAPLeY0I=");

    private static readonly byte[] SeekableZStandardFixture = Convert.FromBase64String(
        "KLUv/WAAf40BAIQCUmVkRm94IGluZGVwZW5kZW50IGNvbXByZXNzaW9uIGZpeHR1cmUuCgEAqP7Xrsoo" +
        "tS/9IAABAAAotS/9YAB/jQEAhAJuZGVwZW5kZW50IGNvbXByZXNzaW9uIGZpeHR1cmUuClJlZEZveCBp" +
        "AQCo/teuyii1L/1ggDeNAQCEAm50IGNvbXByZXNzaW9uIGZpeHR1cmUuClJlZEZveCBpbmRlcGVuZGUB" +
        "AKjCi1dlXipNGDkAAAA7AAAAAIAAAHMOB8EJAAAAAAAAAJnp2FE7AAAAAIAAAMCAZaw7AAAAgDgAAGLz" +
        "q48EAAAAgLHqko8=");

    private static readonly byte[] ZStandardFixture = Convert.FromBase64String(
        "KLUv/aSAOAEAlQEAhAJSZWRGb3ggaW5kZXBlbmRlbnQgY29tcHJlc3Npb24gZml4dHVyZS4KAQCowlm5" +
        "KgOuAx0x");

    private static readonly byte[] UnknownSizeZStandardFixture = Convert.FromBase64String(
        "KLUv/QQ4lQEAhAJSZWRGb3ggaW5kZXBlbmRlbnQgY29tcHJlc3Npb24gZml4dHVyZS4KAQCowlm5KgOu" +
        "Ax0x");

    [Fact]
    public async Task WritersUseAsyncIoWithNonSeekableOutput()
    {
        foreach (CompressionCodec codec in new CompressionCodec[] { new DeflateCodec(), new ZLibCodec(), new GZipCodec(), new LZ4FrameCodec(), new ZStandardCodec(), new SeekableZStandardCodec() })
        {
            using (codec)
            {
                using AsyncOnlyWriteStream output = new();
                await using (Stream writer = codec.CreateCompressionStream(output, true))
                {
                    using CancellationTokenSource cancellation = new();
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await writer.WriteAsync(new byte[1], cancellation.Token));
                    await writer.WriteAsync("async output"u8.ToArray());
                    await writer.FlushAsync();
                }

                Assert.Equal("async output"u8.ToArray(), codec.Decompress(output.ToArray(), 12));
            }
        }
    }

    [Fact]
    public async Task IndependentIndexedFormatsDecodeAndSeek()
    {
        byte[] expected = CreateExpectedData();
        using GDeflateCodec gdeflate = new();
        using SeekableZStandardCodec seekable = new();
        foreach (var item in new[] { (Codec: (CompressionCodec)gdeflate, Input: GDeflateFixture), (Codec: (CompressionCodec)seekable, Input: SeekableZStandardFixture) })
        {
            byte[] input = item.Input;
            Assert.Equal(expected, item.Codec.Decompress(input));
            await using Stream reader = item.Codec.CreateDecompressionStream(new MemoryStream(input));
            reader.Seek(32760, SeekOrigin.Begin);
            byte[] crossing = new byte[20];
            await reader.ReadExactlyAsync(crossing);
            Assert.Equal(expected.AsSpan(32760, 20).ToArray(), crossing);
            reader.Seek(0, SeekOrigin.Begin);
            using MemoryStream output = new();
            await reader.CopyToAsync(output);
            Assert.Equal(expected, output.ToArray());
        }
    }

    [Fact]
    public async Task IndependentFramesDecodeAndChecksumsDetectCorruption()
    {
        byte[] expected = CreateExpectedData();
        using LZ4FrameCodec lz4 = new();
        using ZStandardCodec zstd = new();
        foreach (var item in new[] { (Codec: (CompressionCodec)lz4, Input: Lz4Fixture), (Codec: (CompressionCodec)zstd, Input: ZStandardFixture) })
        {
            byte[] input = item.Input;
            Assert.Equal(expected, item.Codec.Decompress(input));
            await using Stream reader = item.Codec.CreateDecompressionStream(new ShortReadStream(input));
            using MemoryStream output = new();
            await reader.CopyToAsync(output);
            Assert.Equal(expected, output.ToArray());
            input[^1] ^= 1;
            Assert.Throws<CompressionException>(() => item.Codec.Decompress(input, expected.Length));
        }
    }

    [Fact]
    public void UnknownSizesAndConcatenationDoNotInventAllocationSizes()
    {
        using ZStandardCodec codec = new();
        byte[] unknown = UnknownSizeZStandardFixture;
        byte[] known = ZStandardFixture;
        Assert.False(codec.TryGetDecompressedSize(unknown, out _));
        Assert.Throws<NotSupportedException>(() => codec.Decompress(unknown));
        Assert.False(codec.TryGetDecompressedSize([.. known, .. unknown], out _));
        byte[] expected = CreateExpectedData();
        Assert.Equal(expected, codec.Decompress(unknown, expected.Length));
        Assert.Throws<CompressionException>(() => codec.TryGetDecompressedSize(known.AsSpan(0, known.Length - 1), out _));
    }

    [Fact]
    public void MetadataAndAllocationLimitsPreserveSizesBeyondFourGiB()
    {
        using ZStandardCodec codec = new();
        byte[] frame = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, 0xFD2FB528);
        frame[4] = 0xE0;
        BinaryPrimitives.WriteUInt64LittleEndian(frame.AsSpan(5), 4294967297UL);
        frame[13] = 1;
        Assert.True(codec.TryGetDecompressedSize(frame, out long size));
        Assert.Equal(4294967297L, size);
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Decompress(frame));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task StreamingDictionariesSurviveCodecDisposal(int format)
    {
        byte[] dictionary = "History dictionary for archive extraction and compression."u8.ToArray();
        CompressionCodec codec = format switch
        {
            0 => new DeflateCodec(DeflateLevel.BestCompression, dictionary),
            1 => new ZLibCodec(DeflateLevel.BestCompression, dictionary),
            4 => new LZ4FrameCodec(dictionary),
            _ => new ZStandardCodec(3, dictionary)
        };
        using MemoryStream destination = new();
        Stream writer = codec.CreateCompressionStream(destination, true);
        codec.Dispose();
        await writer.WriteAsync(dictionary);
        await writer.FlushAsync();
        await writer.WriteAsync(dictionary);
        await writer.DisposeAsync();
        using CompressionCodec decoder = format switch
        {
            0 => new DeflateCodec(DeflateLevel.DefaultCompression, dictionary),
            1 => new ZLibCodec(DeflateLevel.DefaultCompression, dictionary),
            4 => new LZ4FrameCodec(dictionary),
            _ => new ZStandardCodec(3, dictionary)
        };
        await using Stream reader = decoder.CreateDecompressionStream(new ShortReadStream(destination.ToArray()));
        using MemoryStream restored = new();
        await reader.CopyToAsync(restored);
        Assert.Equal([.. dictionary, .. dictionary], restored.ToArray());
    }

    [Theory]
    [InlineData(LZ4FrameBlockSize.KB64)]
    [InlineData(LZ4FrameBlockSize.KB256)]
    [InlineData(LZ4FrameBlockSize.MB1)]
    [InlineData(LZ4FrameBlockSize.MB4)]
    public async Task Lz4FrameBlockSettingsAndChecksumsWork(LZ4FrameBlockSize blockSize)
    {
        using LZ4FrameCodec codec = new(9, blockSize, false, true, true, []);
        byte[] source = new byte[4194404];
        new Random(12).NextBytes(source);
        byte[] compressed = codec.Compress(source);
        Assert.Equal(source, codec.Decompress(compressed));
        using MemoryStream destination = new();
        await using (Stream writer = codec.CreateCompressionStream(destination, true, source.Length))
            await writer.WriteAsync(source);

        Assert.Equal(source, codec.Decompress(destination.ToArray()));
        compressed[^1] ^= 1;
        Assert.Throws<CompressionException>(() => codec.Decompress(compressed));
    }

    [Fact]
    public void ZlibAndTrainedZstandardDictionariesDetectWrongDictionary()
    {
        byte[] history = "Correct archive dictionary history dictionary."u8.ToArray();
        using ZLibCodec encoder = new(DeflateLevel.DefaultCompression, history);
        using ZLibCodec wrong = new(DeflateLevel.DefaultCompression, "wrong"u8);
        Assert.Throws<CompressionException>(() => wrong.Decompress(encoder.Compress(history), history.Length));
        byte[][] samples = Enumerable.Range(0, 300).Select(i => System.Text.Encoding.UTF8.GetBytes($"Asset texture material {i % 20} name_{i} archive history " + new string('a', 150))).ToArray();
        byte[] dictionary = ZStandardCodec.TrainDictionary(samples, 4096);
        using ZStandardCodec zstd = new(3, dictionary);
        using ZStandardCodec withoutDictionary = new();
        Assert.Throws<CompressionException>(() => withoutDictionary.Decompress(zstd.Compress(samples[0]), samples[0].Length));
    }

    private static byte[] CreateExpectedData()
    {
        byte[] line = "RedFox independent compression fixture.\n"u8.ToArray();
        byte[] data = new byte[line.Length * 2000];
        for (int i = 0; i < 2000; i++)
            line.CopyTo(data, i * line.Length);

        return data;
    }
}
