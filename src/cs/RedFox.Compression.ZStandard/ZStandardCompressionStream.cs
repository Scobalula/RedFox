namespace RedFox.Compression.ZStandard;

internal sealed class ZStandardCompressionStream : CompressionStream
{
    private readonly ZStandardContext _context;

    internal ZStandardCompressionStream(Stream stream, bool leaveOpen, long? size, bool compress, int level, byte[] dictionary) : base(stream, leaveOpen, size, compress)
    {
        _context = new ZStandardContext(compress, level, dictionary, compress ? size : null);
    }

    /// <inheritdoc/>
    protected override bool Process(ReadOnlySpan<byte> source, Span<byte> destination, int operation, out int consumed, out int written) => _context.Process(source, destination, operation, out consumed, out written);

    /// <inheritdoc/>
    protected override void DisposeCodec() => _context.Dispose();
}
