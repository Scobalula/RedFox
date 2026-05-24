namespace RedFox.IO;

/// <summary>
/// Provides a stream view over a region of another stream.
/// </summary>
/// <param name="stream">The parent stream.</param>
/// <param name="offset">The absolute offset in the parent stream where this stream begins.</param>
/// <param name="size">The optional size of the stream view.</param>
/// <param name="leaveOpen">Whether to leave the parent stream open when disposing this stream.</param>
public sealed class SubStream(Stream stream, long offset, long? size, bool leaveOpen) : Stream
{
    private readonly Stream _stream = ValidateStream(stream);
    private long _position;
    private readonly bool _leaveOpen = leaveOpen;
    private bool _disposed;

    /// <summary>
    /// Gets the parent stream.
    /// </summary>
    public Stream BaseStream => _stream;

    /// <summary>
    /// Gets the absolute offset in the parent stream where this stream begins.
    /// </summary>
    public long Offset { get; } = ValidateOffset(offset);

    /// <summary>
    /// Gets the fixed size of this stream view, if one was provided.
    /// </summary>
    public long? Size { get; } = ValidateSize(size);

    /// <summary>
    /// Gets the absolute position in the parent stream.
    /// </summary>
    public long AbsolutePosition => Add(Offset, _position);

    /// <inheritdoc />
    public override bool CanRead => _stream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _stream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => _stream.CanWrite;

    /// <inheritdoc />
    public override long Length => Size ?? Math.Max(0, _stream.Length - Offset);

    /// <inheritdoc />
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    /// <inheritdoc />
    public override void Flush() => _stream.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        ValidateBufferArguments(buffer, offset, count);

        count = GetReadableCount(count);
        if (count == 0)
        {
            return 0;
        }

        _stream.Position = AbsolutePosition;
        int read = _stream.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();

        int count = GetReadableCount(buffer.Length);
        if (count == 0)
        {
            return 0;
        }

        _stream.Position = AbsolutePosition;
        int read = _stream.Read(buffer[..count]);
        _position += read;
        return read;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Add(_position, offset),
            SeekOrigin.End => Add(Length, offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };

        if (position < 0)
        {
            throw new IOException("An attempt was made to seek before the beginning of the sub-stream.");
        }

        _position = position;
        return _position;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        ThrowIfDisposed();

        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (Size is not null)
        {
            throw new NotSupportedException("Fixed-size sub-streams do not support changing length.");
        }

        _stream.SetLength(Add(Offset, value));
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();

        ValidateBufferArguments(buffer, offset, count);
        EnsureWritable(count);

        if (count == 0)
        {
            return;
        }

        _stream.Position = AbsolutePosition;
        _stream.Write(buffer, offset, count);
        _position += count;
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed();

        EnsureWritable(buffer.Length);

        if (buffer.Length == 0)
        {
            return;
        }

        _stream.Position = AbsolutePosition;
        _stream.Write(buffer);
        _position += buffer.Length;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && !_leaveOpen)
            {
                _stream.Close();
            }
            _disposed = true;
        }
    }

    private int GetReadableCount(int count)
    {
        if (Size is not { } streamSize)
        {
            return count;
        }

        long remaining = streamSize - _position;
        return remaining <= 0 ? 0 : (int)Math.Min(count, remaining);
    }

    private void EnsureWritable(int count)
    {
        if (Size is { } streamSize && count > streamSize - _position)
        {
            throw new IOException("Cannot write beyond the end of the sub-stream.");
        }
    }

    private static long ValidateOffset(long offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        return offset;
    }

    private static long? ValidateSize(long? size)
    {
        if (size is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(size));
        }

        return size;
    }

    private static Stream ValidateStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        StreamExceptions.ThrowIfUnseekable(stream);
        return stream;
    }

    private static long Add(long value, long offset)
    {
        try
        {
            return checked(value + offset);
        }
        catch (OverflowException exception)
        {
            throw new IOException("Sub-stream position is too large.", exception);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, nameof(SubStream));
}