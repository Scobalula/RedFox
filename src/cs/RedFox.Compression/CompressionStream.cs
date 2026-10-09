namespace RedFox.Compression;

/// <summary>
/// Provides sequential compression or decompression over an underlying stream.
/// </summary>
public abstract class CompressionStream : Stream
{
    private readonly OwnedStream _stream;
    private readonly bool _compress;
    private readonly long? _expectedSize;
    private readonly byte[] _buffer;
    private int _offset;
    private int _available;
    private bool _complete;
    private bool _eof;
    private bool _disposed;
    private long _position;

    /// <inheritdoc/>
    public override bool CanRead => !_disposed && !_compress;

    /// <inheritdoc/>
    public override bool CanWrite => !_disposed && _compress;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    /// <summary>
    /// Initializes a compression or decompression stream with the standard buffer size.
    /// </summary>
    /// <param name="stream">The underlying stream.</param>
    /// <param name="leaveOpen">Whether to leave the underlying stream open when this stream is disposed.</param>
    /// <param name="size">The expected uncompressed or compressed length, when known.</param>
    /// <param name="compress">Whether this stream compresses data.</param>
    protected CompressionStream(Stream stream, bool leaveOpen, long? size, bool compress) : this(stream, leaveOpen, size, compress, 131072) { }

    /// <summary>
    /// Initializes a compression or decompression stream with a specified buffer size.
    /// </summary>
    /// <param name="stream">The underlying stream.</param>
    /// <param name="leaveOpen">Whether to leave the underlying stream open when this stream is disposed.</param>
    /// <param name="size">The expected uncompressed or compressed length, when known.</param>
    /// <param name="compress">Whether this stream compresses data.</param>
    /// <param name="bufferSize">The size of the intermediate buffer.</param>
    protected CompressionStream(Stream stream, bool leaveOpen, long? size, bool compress, int bufferSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (compress)
        {
            if (!stream.CanWrite)
                throw new ArgumentException("The underlying stream does not support the required operation.", nameof(stream));
        }
        else if (!stream.CanRead)
        {
            throw new ArgumentException("The underlying stream does not support the required operation.", nameof(stream));
        }

        _buffer = new byte[bufferSize];
        long? compressedSize = null;
        if (!compress)
            compressedSize = size;

        _stream = new OwnedStream(stream, leaveOpen, compressedSize);
        _compress = compress;
        if (compress)
            _expectedSize = size;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        Check(false);
        if (buffer.IsEmpty)
            return 0;

        while (true)
        {
            int written = Decode(buffer);
            if (written != 0)
                return written;
            if (_eof)
            {
                if (!_complete)
                    throw new CompressionException("The compressed stream is truncated.");

                return 0;
            }
            if (_available == 0)
            {
                _offset = 0;
                _available = _stream.Read(_buffer);
                _eof = _available == 0;
            }
        }
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Check(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty)
            return 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int written = Decode(buffer.Span);
            if (written != 0)
                return written;
            if (_eof)
            {
                if (!_complete)
                    throw new CompressionException("The compressed stream is truncated.");

                return 0;
            }
            if (_available == 0)
            {
                _offset = 0;
                _available = await _stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
                _eof = _available == 0;
            }
        }
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Check(true);
        CheckSize(buffer.Length);
        while (!buffer.IsEmpty)
        {
            Process(buffer[..Math.Min(buffer.Length, 65536)], _buffer, 0, out int consumed, out int written);
            if (consumed == 0 && written == 0)
                throw new CompressionException("The encoder made no progress.");

            _stream.Write(_buffer.AsSpan(0, written));
            buffer = buffer[consumed..];
            _position += consumed;
        }
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Check(true);
        CheckSize(buffer.Length);
        cancellationToken.ThrowIfCancellationRequested();
        while (!buffer.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Process(buffer.Span[..Math.Min(buffer.Length, 65536)], _buffer, 0, out int consumed, out int written);
            if (consumed == 0 && written == 0)
                throw new CompressionException("The encoder made no progress.");

            await _stream.WriteAsync(_buffer.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
            buffer = buffer[consumed..];
            _position += consumed;
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_compress)
        {
            Drain(1);
            _stream.Flush();
        }
    }

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_compress)
        {
            await DrainAsync(1, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            if (_compress)
            {
                if (_expectedSize.HasValue && _position != _expectedSize.Value)
                    throw new CompressionException("Input does not match the declared uncompressed size.");

                await DrainAsync(2, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _disposed = true;
            DisposeCodec();
            await _stream.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Processes stream data, flushes pending data, or completes compression.
    /// </summary>
    /// <param name="source">The input data.</param>
    /// <param name="destination">The output buffer.</param>
    /// <param name="operation">Zero to process data, one to flush, or two to finish compression.</param>
    /// <param name="consumed">Receives the number of input bytes consumed.</param>
    /// <param name="written">Receives the number of output bytes written.</param>
    /// <returns>Whether the requested operation is complete.</returns>
    protected abstract bool Process(ReadOnlySpan<byte> source, Span<byte> destination, int operation, out int consumed, out int written);

    /// <summary>
    /// Releases resources held by the codec implementation.
    /// </summary>
    protected abstract void DisposeCodec();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                if (_compress)
                {
                    if (_expectedSize.HasValue && _position != _expectedSize.Value)
                        throw new CompressionException("Input does not match the declared uncompressed size.");

                    Drain(2);
                }
            }
            finally
            {
                _disposed = true;
                DisposeCodec();
                _stream.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void Check(bool writing)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (writing != _compress)
            throw new NotSupportedException();
    }

    private int Decode(Span<byte> buffer)
    {
        bool complete = Process(_buffer.AsSpan(_offset, _available), buffer, 0, out int consumed, out int written);
        if (consumed != 0 || written != 0)
            _complete = complete;

        _offset += consumed;
        _available -= consumed;
        _position += written;
        if (consumed == 0 && written == 0 && _available != 0)
            throw new CompressionException("The decoder made no progress.");

        return written;
    }

    private void CheckSize(int count)
    {
        if (_expectedSize.HasValue && count > _expectedSize.Value - _position)
            throw new CompressionException("Input exceeds the declared uncompressed size.");
    }

    private void Drain(int operation)
    {
        bool complete;
        do
        {
            complete = Process([], _buffer, operation, out _, out int written);
            _stream.Write(_buffer.AsSpan(0, written));
        } while (!complete);
    }

    private async ValueTask DrainAsync(int operation, CancellationToken cancellationToken)
    {
        bool complete;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            complete = Process([], _buffer, operation, out _, out int written);
            await _stream.WriteAsync(_buffer.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
        } while (!complete);
    }
}
