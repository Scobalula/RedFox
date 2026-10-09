using System.Buffers.Binary;

namespace RedFox.Compression.GDeflate;

internal sealed class GDeflateCompressionStream : Stream
{
    private readonly Stream _destination;
    private readonly bool _leaveOpen;
    private readonly GDeflateCodec _codec;
    private readonly long _expectedSize;
    private readonly long _start;
    private readonly byte[] _tile = new byte[GDeflateCodec.TileSize];
    private readonly byte[] _compressed;
    private readonly byte[] _table;
    private int _buffered;
    private int _tileIndex;
    private long _payloadSize;
    private long _position;
    private bool _disposed;

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => !_disposed;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    internal GDeflateCompressionStream(Stream destination, bool leaveOpen, long size, int level)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite || !destination.CanSeek)
            throw new ArgumentException("GDeflate writing requires writable, seekable output.", nameof(destination));

        byte[] header = new byte[8];
        int count = GDeflateCodec.WriteHeader(header, size);
        _table = new byte[count * 4];
        _destination = destination;
        _leaveOpen = leaveOpen;
        _expectedSize = size;
        _start = destination.Position;
        _codec = new GDeflateCodec(level);
        _compressed = new byte[(int)GDeflateInterop.CompressBound(0, GDeflateCodec.TileSize, out _)];
        destination.Write(header);
        destination.Write(_table);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Check(buffer.Length);
        while (!buffer.IsEmpty)
        {
            buffer = buffer[Fill(buffer)..];
            if (_buffered == _tile.Length)
                _destination.Write(_compressed.AsSpan(0, CompressTile()));
        }
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Check(buffer.Length);
        cancellationToken.ThrowIfCancellationRequested();
        while (!buffer.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            buffer = buffer[Fill(buffer.Span)..];
            if (_buffered == _tile.Length)
            {
                int written = CompressTile();
                await _destination.WriteAsync(_compressed.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _destination.Flush();
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _destination.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            CheckFinalSize();
            if (_buffered != 0)
            {
                int written = CompressTile();
                await _destination.WriteAsync(_compressed.AsMemory(0, written)).ConfigureAwait(false);
            }

            long end = _destination.Position;
            _destination.Position = _start + 8;
            await _destination.WriteAsync(_table).ConfigureAwait(false);
            _destination.Position = end;
        }
        finally
        {
            _disposed = true;
            _codec.Dispose();
            if (!_leaveOpen)
                await _destination.DisposeAsync().ConfigureAwait(false);

            GC.SuppressFinalize(this);
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                CheckFinalSize();
                if (_buffered != 0)
                    _destination.Write(_compressed.AsSpan(0, CompressTile()));

                long end = _destination.Position;
                _destination.Position = _start + 8;
                _destination.Write(_table);
                _destination.Position = end;
            }
            finally
            {
                _disposed = true;
                _codec.Dispose();
                if (!_leaveOpen)
                    _destination.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void Check(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count > _expectedSize - _position)
            throw new CompressionException("Input exceeds the declared GDeflate size.");
    }

    private int Fill(ReadOnlySpan<byte> buffer)
    {
        int count = Math.Min(buffer.Length, _tile.Length - _buffered);
        buffer[..count].CopyTo(_tile.AsSpan(_buffered));
        _buffered += count;
        _position += count;
        return count;
    }

    private int CompressTile()
    {
        int written = _codec.CompressTile(_tile.AsSpan(0, _buffered), _compressed);
        BinaryPrimitives.WriteUInt32LittleEndian(_table.AsSpan(_tileIndex * 4), checked((uint)_payloadSize));
        if (_tileIndex == _table.Length / 4 - 1)
            BinaryPrimitives.WriteUInt32LittleEndian(_table, (uint)written);

        _payloadSize += written;
        _tileIndex++;
        _buffered = 0;
        return written;
    }

    private void CheckFinalSize()
    {
        if (_position != _expectedSize)
            throw new CompressionException("Input does not match the declared GDeflate size.");
    }
}
