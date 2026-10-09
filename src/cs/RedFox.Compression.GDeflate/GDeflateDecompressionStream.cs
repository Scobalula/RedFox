namespace RedFox.Compression.GDeflate;

internal sealed class GDeflateDecompressionStream : Stream
{
    private readonly Stream _source;
    private readonly GDeflateCodec _codec = new();
    private readonly CompressedBlock[] _tiles;
    private readonly byte[] _decoded = new byte[GDeflateCodec.TileSize];
    private byte[] _compressed = [];
    private readonly long _length;
    private readonly long _payloadStart;
    private long _position;
    private int _cachedTile = -1;
    private bool _disposed;

    /// <inheritdoc/>
    public override bool CanRead => !_disposed;

    /// <inheritdoc/>
    public override bool CanSeek => !_disposed && _source.CanSeek;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _length;

    /// <inheritdoc/>
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    internal GDeflateDecompressionStream(Stream source, bool leaveOpen, long? compressedSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("GDeflate requires readable input.", nameof(source));

        _source = new PassThroughCodec().CreateDecompressionStream(source, leaveOpen, compressedSize);
        try
        {
            byte[] header = new byte[8];
            _source.ReadExactly(header);
            _length = GDeflateCodec.ReadHeader(header, out int count);
            byte[] table = new byte[count * 4];
            _source.ReadExactly(table);
            _payloadStart = 8 + table.Length;
            long? available = null;
            if (compressedSize.HasValue)
                available = compressedSize.Value - _payloadStart;
            else if (_source.CanSeek)
                available = _source.Length - _payloadStart;

            _tiles = GDeflateCodec.ReadTiles(table, _length, available);
        }
        catch (EndOfStreamException exception)
        {
            throw new CompressionException("The GDeflate metadata is truncated.", exception);
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty || _position == _length)
            return 0;

        int index = (int)(_position / GDeflateCodec.TileSize);
        bool decode = _cachedTile != index;
        if (decode)
        {
            PrepareTile(index);
            try
            {
                _source.ReadExactly(_compressed.AsSpan(0, _tiles[index].CompressedLength));
            }
            catch (EndOfStreamException exception)
            {
                throw new CompressionException("The GDeflate tile is truncated.", exception);
            }
        }

        return DecodeAndCopy(index, buffer, decode);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || _position == _length)
            return 0;

        int index = (int)(_position / GDeflateCodec.TileSize);
        bool decode = _cachedTile != index;
        if (decode)
        {
            PrepareTile(index);
            try
            {
                await _source.ReadExactlyAsync(_compressed.AsMemory(0, _tiles[index].CompressedLength), cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException exception)
            {
                throw new CompressionException("The GDeflate tile is truncated.", exception);
            }
        }

        return DecodeAndCopy(index, buffer.Span, decode);
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
            SeekOrigin.End => checked(_length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (position < 0 || position > _length)
            throw new IOException("Seek is outside the GDeflate stream.");

        return _position = position;
    }

    /// <inheritdoc/>
    public override void Flush() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        Flush();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _codec.Dispose();
        await _source.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            _codec.Dispose();
            _source.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PrepareTile(int index)
    {
        if (_compressed.Length < _tiles[index].CompressedLength)
            _compressed = new byte[_tiles[index].CompressedLength];
        if (_source.CanSeek)
            _source.Position = _payloadStart + _tiles[index].CompressedOffset;

        _cachedTile = -1;
    }

    private int DecodeAndCopy(int index, Span<byte> buffer, bool decode)
    {
        if (decode)
        {
            _codec.DecompressTile(_compressed.AsSpan(0, _tiles[index].CompressedLength), _decoded.AsSpan(0, _tiles[index].DecompressedLength));
            _cachedTile = index;
        }

        int offset = (int)(_position % GDeflateCodec.TileSize);
        int count = Math.Min(buffer.Length, _tiles[index].DecompressedLength - offset);
        _decoded.AsSpan(offset, count).CopyTo(buffer);
        _position += count;
        return count;
    }
}
