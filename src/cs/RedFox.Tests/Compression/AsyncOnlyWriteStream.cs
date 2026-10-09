namespace RedFox.Tests.Compression;

internal sealed class AsyncOnlyWriteStream : Stream
{
    private readonly MemoryStream _stream = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    internal byte[] ToArray() => _stream.ToArray();

    public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Expected asynchronous underlying I/O.");

    public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Expected asynchronous underlying I/O.");

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => _stream.WriteAsync(buffer, cancellationToken);

    public override void Flush() => throw new InvalidOperationException("Expected asynchronous underlying I/O.");

    public override Task FlushAsync(CancellationToken cancellationToken) => _stream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _stream.Dispose();

        base.Dispose(disposing);
    }
}
