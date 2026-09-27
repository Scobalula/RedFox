namespace RedFox.GameExtraction.Template;

internal sealed class ZipEntryStream(Stream stream, SemaphoreSlim archiveLock) : Stream
{
    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly SemaphoreSlim _archiveLock = archiveLock ?? throw new ArgumentNullException(nameof(archiveLock));
    private int _disposed;

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush() => _stream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _stream.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer) => _stream.ReadAsync(buffer, CancellationToken.None);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => _stream.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    public override void SetLength(long value) => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _archiveLock.Release();
            }
        }

        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeStream();

        base.Dispose(disposing);
    }

    private void DisposeStream()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                _stream.Dispose();
            }
            finally
            {
                _archiveLock.Release();
            }
        }
    }
}
