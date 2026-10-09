namespace RedFox.Compression.Deflate;

internal sealed class ZLibCompressionStream : CompressionStream
{
    private readonly ZLibContext _context;

    internal ZLibCompressionStream(Stream stream, bool leaveOpen, long? size, bool compress, int windowBits, DeflateLevel level, byte[] dictionary) : base(stream, leaveOpen, size, compress)
    {
        _context = new ZLibContext(compress, windowBits, level, dictionary);
    }

    /// <inheritdoc/>
    protected override bool Process(ReadOnlySpan<byte> source, Span<byte> destination, int operation, out int consumed, out int written) => _context.Process(source, destination, operation, out consumed, out written);

    /// <inheritdoc/>
    protected override void DisposeCodec() => _context.Dispose();
}
