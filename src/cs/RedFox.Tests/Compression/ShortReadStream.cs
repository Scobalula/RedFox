namespace RedFox.Tests.Compression;

internal sealed class ShortReadStream : Stream
{
    private readonly MemoryStream _stream;

    internal bool AsyncRead { get; private set; }
    internal long BytesRead => _stream.Position;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    internal ShortReadStream(byte[] data) => _stream = new MemoryStream(data);

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) => _stream.Read(buffer[..Math.Min(3, buffer.Length)]);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AsyncRead = true;
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _stream.Dispose();

        base.Dispose(disposing);
    }
}
