using System.Text;
using RedFox.Compression;
using RedFox.Compression.LZO;

namespace RedFox.Tests.Compression;

public sealed class LzoCodecTests
{
    [Theory]
    [MemberData(nameof(RoundTripPayloads))]
    public void LzoCodec_RoundTrip(byte[] source)
    {
        LzoCodec codec = new();
        byte[] compressed = new byte[codec.GetMaxCompressedSize(source.Length)];

        int compressedSize = codec.Compress(source, compressed);
        byte[] decompressed = new byte[source.Length];
        int decompressedSize = codec.Decompress(compressed.AsSpan(0, compressedSize), decompressed);

        Assert.Equal(source.Length, decompressedSize);
        Assert.Equal(source, decompressed);
        Assert.Equal(CompressionCodecFlags.None, codec.Flags);
    }

    [Fact]
    public void LzoCodec_CompressesRepeatedPayload()
    {
        LzoCodec codec = new();
        byte[] source = BuildRepeatedPayload(4096, 0x5A);
        byte[] compressed = new byte[codec.GetMaxCompressedSize(source.Length)];

        int compressedSize = codec.Compress(source, compressed);

        Assert.True(compressedSize < source.Length / 4);
    }

    [Fact]
    public void LzoCodec_DecompressesLiteralOnlyStream()
    {
        LzoCodec codec = new();
        byte[] compressed = [23, (byte)'R', (byte)'e', (byte)'d', (byte)'F', (byte)'o', (byte)'x', 17, 0, 0];
        byte[] destination = new byte[6];

        int decompressedSize = codec.Decompress(compressed, destination);

        Assert.Equal(destination.Length, decompressedSize);
        Assert.Equal("RedFox"u8.ToArray(), destination);
    }

    [Fact]
    public void LzoCodec_DecompressesShortStateMatch()
    {
        LzoCodec codec = new();
        byte[] compressed = [18, (byte)'A', 0, 0, 17, 0, 0];
        byte[] destination = new byte[3];

        int decompressedSize = codec.Decompress(compressed, destination);

        Assert.Equal(destination.Length, decompressedSize);
        Assert.Equal("AAA"u8.ToArray(), destination);
    }

    [Fact]
    public void LzoCodec_DecompressesLargeStateMatch()
    {
        LzoCodec codec = new();
        byte[] literals = BuildPatternedPayload(2049);
        byte[] compressed = CreateLongLiteralStream(literals, [0, 0, 17, 0, 0]);
        byte[] expected = new byte[literals.Length + 3];
        literals.CopyTo(expected.AsSpan());
        literals.AsSpan(0, 3).CopyTo(expected.AsSpan(literals.Length));
        byte[] destination = new byte[expected.Length];

        int decompressedSize = codec.Decompress(compressed, destination);

        Assert.Equal(expected.Length, decompressedSize);
        Assert.Equal(expected, destination);
    }

    [Fact]
    public void LzoCodec_DecompressesM3Match()
    {
        LzoCodec codec = new();
        byte[] compressed = [20, (byte)'a', (byte)'b', (byte)'c', 36, 8, 0, 17, 0, 0];
        byte[] destination = new byte[9];

        int decompressedSize = codec.Decompress(compressed, destination);

        Assert.Equal(destination.Length, decompressedSize);
        Assert.Equal("abcabcabc"u8.ToArray(), destination);
    }

    [Fact]
    public void LzoCodec_DecompressesM4Match()
    {
        LzoCodec codec = new();
        byte[] literals = BuildPatternedPayload(16 * 1024 + 1);
        byte[] compressed = CreateLongLiteralStream(literals, [17, 4, 0, 17, 0, 0]);
        byte[] expected = new byte[literals.Length + 3];
        literals.CopyTo(expected.AsSpan());
        literals.AsSpan(0, 3).CopyTo(expected.AsSpan(literals.Length));
        byte[] destination = new byte[expected.Length];

        int decompressedSize = codec.Decompress(compressed, destination);

        Assert.Equal(expected.Length, decompressedSize);
        Assert.Equal(expected, destination);
    }

    [Fact]
    public void LzoCodec_DecompressesVersionOneZeroRun()
    {
        LzoCodec codec = new();
        byte[] compressed = [17, 1, 18, (byte)'Z', 24, 252, 255, 1, 17, 0, 0];
        byte[] expected = new byte[13];
        expected[0] = (byte)'Z';
        byte[] destination = new byte[expected.Length];

        int decompressedSize = codec.Decompress(compressed, destination);

        Assert.Equal(expected.Length, decompressedSize);
        Assert.Equal(expected, destination);
    }

    [Fact]
    public void LzoCodec_UnsupportedMembersThrow()
    {
        LzoCodec codec = new();
        byte[] source = [1, 2, 3];
        byte[] destination = new byte[codec.GetMaxCompressedSize(source.Length)];

        Assert.Throws<NotSupportedException>(() => codec.Compress(source, destination, ReadOnlySpan<byte>.Empty));
        Assert.Throws<NotSupportedException>(() => codec.Decompress(source, destination, ReadOnlySpan<byte>.Empty));
        Assert.Throws<NotSupportedException>(() => codec.GetDecompressedSize(source));
    }

    public static TheoryData<byte[]> RoundTripPayloads => new()
    {
        Array.Empty<byte>(),
        Encoding.UTF8.GetBytes("R"),
        Encoding.UTF8.GetBytes("RedFox-LZO-Codec-Data"),
        BuildRepeatedPayload(1024, 0),
        BuildRepeatedPayload(4096, 0xA5),
        BuildPatternedPayload(12 * 1024),
        BuildPatternedPayload(64 * 1024),
    };

    private static byte[] BuildPatternedPayload(int length)
    {
        ReadOnlySpan<byte> pattern = "RedFox-LZO-pattern-"u8;
        byte[] payload = new byte[length];

        for (int index = 0; index < payload.Length; index++)
        {
            payload[index] = pattern[index % pattern.Length];
        }

        return payload;
    }

    private static byte[] BuildRepeatedPayload(int length, byte value)
    {
        byte[] payload = new byte[length];
        payload.AsSpan().Fill(value);
        return payload;
    }

    private static byte[] CreateLongLiteralStream(ReadOnlySpan<byte> literals, ReadOnlySpan<byte> suffix)
    {
        byte[] compressed = new byte[literals.Length + literals.Length / byte.MaxValue + suffix.Length + 8];
        int compressedIndex = 0;

        WriteLongLiteralLength(literals.Length, compressed, ref compressedIndex);
        literals.CopyTo(compressed.AsSpan(compressedIndex));
        compressedIndex += literals.Length;
        suffix.CopyTo(compressed.AsSpan(compressedIndex));
        compressedIndex += suffix.Length;

        return compressed.AsSpan(0, compressedIndex).ToArray();
    }

    private static void WriteLongLiteralLength(int length, Span<byte> destination, ref int destinationIndex)
    {
        if (length <= 18)
        {
            destination[destinationIndex++] = (byte)(length - 3);
            return;
        }

        destination[destinationIndex++] = 0;
        int remainder = length - 18;

        while (remainder > byte.MaxValue)
        {
            destination[destinationIndex++] = 0;
            remainder -= byte.MaxValue;
        }

        destination[destinationIndex++] = (byte)remainder;
    }
}