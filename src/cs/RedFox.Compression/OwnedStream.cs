namespace RedFox.Compression;

internal sealed class OwnedStream : Stream
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly long? _length;
    private readonly long _start;
    private long _position;
    private bool _disposed;

    /// <inheritdoc/>
    public override bool CanRead => !_disposed && _stream.CanRead;

    /// <inheritdoc/>
    public override bool CanWrite => !_disposed && !_length.HasValue && _stream.CanWrite;

    /// <inheritdoc/>
    public override bool CanSeek => !_disposed && _stream.CanSeek;

    /// <inheritdoc/>
    public override long Length => _length ?? _stream.Length - _start;

    /// <inheritdoc/>
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    internal OwnedStream(Stream stream, bool leaveOpen, long? length)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _stream = stream;
        _leaveOpen = leaveOpen;
        _length = length;
        _start = stream.CanSeek ? stream.Position : 0;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        int read = _stream.Read(buffer[..Limit(buffer.Length)]);
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int read = await _stream.ReadAsync(buffer[..Limit(buffer.Length)], cancellationToken).ConfigureAwait(false);
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanWrite)
            throw new NotSupportedException();

        _stream.Write(buffer);
        _position += buffer.Length;
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanWrite)
            throw new NotSupportedException();

        await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _position += buffer.Length;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanSeek)
            throw new NotSupportedException();

        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(_position + offset),
            SeekOrigin.End => checked(Length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (position < 0 || (_length.HasValue && position > _length.Value))
            throw new IOException("Seek is outside the stream bounds.");

        _stream.Position = checked(_start + position);
        return _position = position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        if (!CanWrite)
            throw new NotSupportedException();

        _stream.SetLength(checked(_start + value));
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _stream.Flush();
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed && !_leaveOpen)
            await _stream.DisposeAsync().ConfigureAwait(false);

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing && !_leaveOpen)
            _stream.Dispose();

        _disposed = true;
        base.Dispose(disposing);
    }

    private int Limit(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _length.HasValue ? (int)Math.Min(count, Math.Max(0, _length.Value - _position)) : count;
    }
}
