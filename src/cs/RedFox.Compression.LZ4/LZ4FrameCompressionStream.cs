namespace RedFox.Compression.LZ4;

internal sealed class LZ4FrameCompressionStream : CompressionStream
{
    private readonly LZ4FrameContext _context;

    internal LZ4FrameCompressionStream(Stream stream, bool leaveOpen, long? size, bool compress, LZ4FramePreferences preferences, byte[] dictionary) : base(stream, leaveOpen, size, compress, (1 << (8 + 2 * preferences.FrameInfo.BlockSize)) + 65664)
    {
        preferences.FrameInfo.ContentSize = compress && size.HasValue ? (ulong)size.Value : 0;
        _context = new LZ4FrameContext(compress, preferences, dictionary);
    }

    /// <inheritdoc/>
    protected override bool Process(ReadOnlySpan<byte> source, Span<byte> destination, int operation, out int consumed, out int written) => _context.Process(source, destination, operation, out consumed, out written);

    /// <inheritdoc/>
    protected override void DisposeCodec() => _context.Dispose();
}
