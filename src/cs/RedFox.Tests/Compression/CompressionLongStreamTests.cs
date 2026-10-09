using RedFox.Compression.Deflate;

namespace RedFox.Tests.Compression;

public sealed class CompressionLongStreamTests
{
    [Fact]
    public void ZlibStreamsCountBeyondNativeUInt32Totals()
    {
        using DeflateCodec codec = new(DeflateLevel.BestSpeed);
        byte[] chunk = new byte[1048576];
        long expected = 4097L * chunk.Length;
        using MemoryStream compressed = new();
        using (Stream writer = codec.CreateCompressionStream(compressed, true, expected))
        {
            for (int i = 0; i < 4097; i++)
                writer.Write(chunk);

            Assert.Equal(expected, writer.Position);
        }

        compressed.Position = 0;
        using Stream reader = codec.CreateDecompressionStream(compressed, true, compressed.Length);
        reader.CopyTo(Stream.Null);
        Assert.Equal(expected, reader.Position);
    }
}
