using System.Buffers.Binary;
using RedFox.Compression;
using RedFox.Compression.FastLZ;
using RedFox.Compression.GDeflate;
using RedFox.Compression.ZStandard;

namespace RedFox.Tests.Compression;

public sealed class IndexedCodecTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(200000)]
    public async Task GDeflateTilesRoundTripAndSeek(int length)
    {
        byte[] data = Enumerable.Range(0, length).Select(i => (byte)(i * 17)).ToArray();
        GDeflateCodec codec = new();
        byte[] compressed = codec.Compress(data);
        Assert.Equal(data, codec.Decompress(compressed));
        using MemoryStream output = new();
        await using (Stream writer = codec.CreateCompressionStream(output, true, length))
        {
            await writer.WriteAsync(data.AsMemory(0, 1));
            long beforeFlush = output.Position;
            await writer.FlushAsync();
            Assert.Equal(beforeFlush, output.Position);
            await writer.WriteAsync(data.AsMemory(1));
        }
        Assert.Equal(data, codec.Decompress(output.ToArray()));
        using MemoryStream embedded = new([99, .. compressed, 0xCA, 0xFE]);
        embedded.Position = 1;
        await using Stream reader = codec.CreateDecompressionStream(embedded, true, compressed.Length);
        codec.Dispose();
        Assert.True(reader.CanSeek);
        reader.Seek(-1, SeekOrigin.End);
        Assert.Equal(data[^1], reader.ReadByte());
        reader.Seek(length / 2, SeekOrigin.Begin);
        byte[] tail = new byte[length - length / 2];
        await reader.ReadExactlyAsync(tail);
        Assert.Equal(data.AsSpan(length / 2).ToArray(), tail);
        using GDeflateCodec another = new();
        using ShortReadStream sequential = new([.. compressed, 0xAA]);
        await using Stream nonSeekable = another.CreateDecompressionStream(sequential, true, compressed.Length);
        Assert.False(nonSeekable.CanSeek);
        using MemoryStream restored = new();
        await nonSeekable.CopyToAsync(restored);
        Assert.Equal(data, restored.ToArray());
        Assert.Equal(compressed.Length, sequential.BytesRead);
    }

    [Fact]
    public void GDeflateRejectsEmptyInputsInvalidHeadersAndIncompleteWrites()
    {
        using GDeflateCodec codec = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Compress([]));
        using MemoryStream output = new();
        Assert.Throws<ArgumentException>(() => codec.CreateCompressionStream(output));
        Stream writer = codec.CreateCompressionStream(output, true, 10);
        Assert.Throws<CompressionException>(() => writer.Dispose());
        byte[] compressed = codec.Compress(new byte[65537]);
        compressed[4] = 0;
        Assert.Throws<CompressionException>(() => codec.TryGetDecompressedSize(compressed, out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SeekableZstandardWorksWithIndexedAndSequentialReaders(bool checksums)
    {
        byte[] data = Enumerable.Range(0, 200000).Select(i => (byte)(i * 23)).ToArray();
        SeekableZStandardCodec codec = new(3, 65536, checksums, []);
        byte[] compressed = codec.Compress(data);
        Assert.Equal(data, codec.Decompress(compressed));
        using ZStandardCodec standard = new();
        Assert.Equal(data, standard.Decompress(compressed));
        using MemoryStream output = new();
        await using (Stream writer = codec.CreateCompressionStream(output, true, data.Length))
        {
            await writer.WriteAsync(data.AsMemory(0, 123));
            await writer.FlushAsync();
            await writer.WriteAsync(data.AsMemory(123));
        }
        Assert.Equal(data, codec.Decompress(output.ToArray()));
        using MemoryStream source = new([99, .. compressed, 0xCA, 0xFE]);
        source.Position = 1;
        await using Stream reader = codec.CreateDecompressionStream(source, true, compressed.Length);
        codec.Dispose();
        reader.Seek(65530, SeekOrigin.Begin);
        byte[] crossing = new byte[20];
        await reader.ReadExactlyAsync(crossing);
        Assert.Equal(data.AsSpan(65530, 20).ToArray(), crossing);
        reader.Seek(-1, SeekOrigin.End);
        Assert.Equal(data[^1], reader.ReadByte());
        Assert.Equal(-1, reader.ReadByte());
        using Stream sequential = standard.CreateDecompressionStream(new ShortReadStream(compressed));
        using MemoryStream restored = new();
        await sequential.CopyToAsync(restored);
        Assert.Equal(data, restored.ToArray());
    }

    [Fact]
    public async Task TruncatedGDeflateTilesFailOnSequentialReads()
    {
        using GDeflateCodec codec = new();
        byte[] compressed = codec.Compress(new byte[100]);
        Assert.Throws<CompressionException>(() => codec.CreateDecompressionStream(new ShortReadStream(compressed[..7])));
        using Stream reader = codec.CreateDecompressionStream(new ShortReadStream(compressed[..^1]));
        Assert.Throws<CompressionException>(() => reader.ReadByte());
        await using Stream asynchronous = codec.CreateDecompressionStream(new ShortReadStream(compressed[..^1]));
        await Assert.ThrowsAsync<CompressionException>(async () => await asynchronous.ReadExactlyAsync(new byte[100]));
    }

    [Fact]
    public void SeekEntriesMustContainExactlyOneFrame()
    {
        using ZStandardCodec standard = new();
        byte[] first = standard.Compress(new byte[10]);
        byte[] second = standard.Compress(new byte[10]);
        byte[] table = new byte[25];
        BinaryPrimitives.WriteUInt32LittleEndian(table, 0x184D2A5E);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(4), 17);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(8), (uint)(first.Length + second.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(12), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(21), 0x8F92EAB1);
        byte[] compressed = [.. first, .. second, .. table];
        using SeekableZStandardCodec codec = new();
        Assert.Throws<CompressionException>(() => codec.Decompress(compressed));
        using Stream reader = codec.CreateDecompressionStream(new MemoryStream(compressed));
        Assert.Throws<CompressionException>(() => reader.ReadByte());
    }

    [Fact]
    public void SeekableZstandardChecksumsTablesAndEmptyInputs()
    {
        using SeekableZStandardCodec codec = new(3, 1024, true, []);
        Assert.Empty(codec.Decompress(codec.Compress([])));
        byte[] compressed = codec.Compress(new byte[2000]);
        int frames = (int)BinaryPrimitives.ReadUInt32LittleEndian(compressed.AsSpan(compressed.Length - 9));
        int tableStart = compressed.Length - 17 - frames * 12;
        compressed[tableStart + 16] ^= 1;
        Assert.Throws<CompressionException>(() => codec.Decompress(compressed));
        using Stream reader = codec.CreateDecompressionStream(new MemoryStream(compressed));
        Assert.Throws<CompressionException>(() => reader.ReadByte());
        compressed[^5] |= 4;
        Assert.Throws<CompressionException>(() => codec.TryGetDecompressedSize(compressed, out _));
        Assert.Throws<ArgumentException>(() => codec.CreateDecompressionStream(new ShortReadStream(compressed)));
    }

    [Fact]
    public void FastLzPreservesItsManagedBlockFormat()
    {
        using FastLZCodec codec = new();
        foreach (int length in new[] { 0, 1, 3, 4, 100, 65536 })
        {
            byte[] data = Enumerable.Range(0, length).Select(i => (byte)(i % 40)).ToArray();
            Assert.Equal(data, codec.Decompress(codec.Compress(data), length));
        }
    }

    [Fact(Skip = "External Oodle library and licensed interoperability fixture are unavailable in the repository.")]
    public void OodleExternalInteroperabilityFixture() { }
}
