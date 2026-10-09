using RedFox.Compression;
using RedFox.Compression.LZ4;

namespace RedFox.Tests.Compression;

public sealed class BlockDecompressionStreamTests
{
    [Fact]
    public async Task MixedBlocksSeekAcrossBoundariesAndCacheOneBlock()
    {
        using LZ4Codec codec = new();
        byte[] first = "stored"u8.ToArray();
        byte[] second = codec.Compress("compressed"u8);
        byte[] third = "end"u8.ToArray();
        using MemoryStream source = new([99, .. second, .. third, .. first]);
        source.Position = 1;
        CompressedBlock[] blocks = [new(second.Length + third.Length, first.Length, first.Length), new(0, second.Length, 10), new(second.Length, third.Length, third.Length)];
        int calls = 0;
        using BlockDecompressionStream reader = new(source, blocks, (i, input, output) =>
        {
            calls++;
            if (i == 1)
                return codec.Decompress(input, output);

            input.CopyTo(output);
            return input.Length;
        }, true);

        reader.Seek(7, SeekOrigin.Begin);
        Assert.Equal('o', reader.ReadByte());
        reader.Seek(8, SeekOrigin.Begin);
        Assert.Equal('m', reader.ReadByte());
        Assert.Equal(1, calls);
        reader.Seek(5, SeekOrigin.Begin);
        byte[] result = new byte[14];
        await reader.ReadExactlyAsync(result);
        Assert.Equal("dcompressedend"u8.ToArray(), result);
        Assert.Equal(0, reader.Read(result));
        Assert.Throws<IOException>(() => reader.Seek(-20, SeekOrigin.End));
        Assert.Throws<NotSupportedException>(() => reader.Write([1]));
    }

    [Fact]
    public void InvalidIndexesAndDecodedSizeMismatchFail()
    {
        using MemoryStream source = new([1, 2]);
        Assert.Throws<ArgumentException>(() => new BlockDecompressionStream(source, [new(1, 2, 2)], (i, input, output) => 0));
        using BlockDecompressionStream reader = new(source, [new(0, 2, 2)], (i, input, output) => 1, true);
        Assert.Throws<CompressionException>(() => reader.ReadByte());
        Assert.Throws<ArgumentException>(() => new BlockDecompressionStream(new ShortReadStream([1]), [], (i, input, output) => 0));
    }

    [Fact]
    public void OutputPositionsUseInt64WithoutAllocatingEarlierBlocks()
    {
        using MemoryStream source = new([1, 2, 3, 4]);
        CompressedBlock[] blocks = [new(0, 1, int.MaxValue), new(1, 1, int.MaxValue), new(2, 2, 2)];
        using BlockDecompressionStream reader = new(source, blocks, (i, input, output) =>
        {
            input.CopyTo(output);
            return input.Length;
        }, true);
        Assert.Equal(4294967296L, reader.Length);
        reader.Seek(-1, SeekOrigin.End);
        Assert.Equal(4, reader.ReadByte());
        Assert.Equal(4294967296L, reader.Position);
    }

    [Fact]
    public async Task CompressedOffsetsUseInt64()
    {
        using SparseReadStream source = new();
        source.Position = 12;
        using BlockDecompressionStream reader = new(source, [new(4294967297L, 2, 2)], (i, input, output) =>
        {
            input.CopyTo(output);
            return input.Length;
        }, true);
        byte[] decoded = new byte[2];
        await reader.ReadExactlyAsync(decoded);
        Assert.Equal([0xA5, 0xA5], decoded);
        Assert.Equal(4294967311L, source.Position);
    }
}
