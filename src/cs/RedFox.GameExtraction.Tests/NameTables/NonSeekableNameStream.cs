namespace RedFox.GameExtraction.Tests.NameTables;

internal sealed class NonSeekableNameStream : Stream
{
    private readonly MemoryStream _inner;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public NonSeekableNameStream() => _inner = new MemoryStream();

    public NonSeekableNameStream(byte[] data) => _inner = new MemoryStream(data);

    public byte[] ToArray() => _inner.ToArray();

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, 3));

    public override int Read(Span<byte> buffer) => _inner.Read(buffer[..Math.Min(buffer.Length, 3)]);

    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

    public override void Flush() => _inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
