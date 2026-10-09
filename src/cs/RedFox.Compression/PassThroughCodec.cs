namespace RedFox.Compression;

/// <summary>
/// Copies input without applying compression.
/// </summary>
public sealed class PassThroughCodec : CompressionCodec
{
    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.Compression | CompressionCodecFlags.Decompression | CompressionCodecFlags.CompressionStreams | CompressionCodecFlags.DecompressionStreams | CompressionCodecFlags.SizeInspection;

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < source.Length)
            throw new CompressionException("Destination buffer is not large enough to store the source data.");

        source.CopyTo(destination);
        return source.Length;
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination) => Compress(source, destination);

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputSize);
        return inputSize;
    }

    /// <inheritdoc/>
    public override bool TryGetDecompressedSize(ReadOnlySpan<byte> source, out long size)
    {
        size = source.Length;
        return true;
    }

    /// <inheritdoc/>
    public override Stream CreateCompressionStream(Stream destination, bool leaveOpen, long? uncompressedSize) => new OwnedStream(destination, leaveOpen, null);

    /// <inheritdoc/>
    public override Stream CreateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize) => new OwnedStream(source, leaveOpen, compressedSize);
}
