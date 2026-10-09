namespace RedFox.Compression;

/// <summary>
/// Provides seekable decompression over application-indexed compressed blocks.
/// </summary>
public class BlockDecompressionStream : Stream
{
    private readonly Stream _source;
    private readonly CompressedBlock[] _blocks;
    private readonly long[] _offsets;
    private readonly BlockDecoder _decoder;
    private readonly bool _leaveOpen;
    private readonly long _start;
    private byte[] _compressed = [];
    private byte[] _decoded = [];
    private int _cachedBlock = -1;
    private long _position;
    private bool _disposed;

    /// <inheritdoc/>
    public override bool CanRead => !_disposed;

    /// <inheritdoc/>
    public override bool CanSeek => !_disposed;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _offsets[^1];

    /// <inheritdoc/>
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    /// <summary>
    /// Initializes a block decompression stream and takes ownership of the source stream.
    /// </summary>
    /// <param name="source">The seekable compressed input stream.</param>
    /// <param name="blocks">The compressed block index in decompressed order.</param>
    /// <param name="decoder">The delegate used to decode each block.</param>
    public BlockDecompressionStream(Stream source, IReadOnlyList<CompressedBlock> blocks, BlockDecoder decoder) : this(source, blocks, decoder, false) { }

    /// <summary>
    /// Initializes a block decompression stream.
    /// </summary>
    /// <param name="source">The seekable compressed input stream.</param>
    /// <param name="blocks">The compressed block index in decompressed order.</param>
    /// <param name="decoder">The delegate used to decode each block.</param>
    /// <param name="leaveOpen">Whether to leave the source stream open when this stream is disposed.</param>
    public BlockDecompressionStream(Stream source, IReadOnlyList<CompressedBlock> blocks, BlockDecoder decoder, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(decoder);
        if (!source.CanRead || !source.CanSeek)
            throw new ArgumentException("Block decompression requires readable, seekable input.", nameof(source));

        _source = source;
        _start = source.Position;
        _blocks = [.. blocks];
        _offsets = new long[_blocks.Length + 1];
        _decoder = decoder;
        _leaveOpen = leaveOpen;

        long available = source.Length - _start;
        for (int i = 0; i < _blocks.Length; i++)
        {
            CompressedBlock block = _blocks[i];
            if (block.CompressedOffset < 0 || block.CompressedLength < 0 || block.DecompressedLength < 0 || block.CompressedOffset > available - block.CompressedLength)
                throw new ArgumentException("The block index contains invalid offsets or lengths.", nameof(blocks));

            _offsets[i + 1] = checked(_offsets[i] + block.DecompressedLength);
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty || _position == Length)
            return 0;

        int index = FindBlock();
        if (_cachedBlock != index)
        {
            PrepareBlock(index);
            _source.ReadExactly(_compressed.AsSpan(0, _blocks[index].CompressedLength));
            DecodeBlock(index);
        }

        return CopyBlock(index, buffer);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || _position == Length)
            return 0;

        int index = FindBlock();
        if (_cachedBlock != index)
        {
            PrepareBlock(index);
            await _source.ReadExactlyAsync(_compressed.AsMemory(0, _blocks[index].CompressedLength), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            DecodeBlock(index);
        }

        return CopyBlock(index, buffer.Span);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(_position + offset),
            SeekOrigin.End => checked(Length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (position < 0 || position > Length)
            throw new IOException("Seek is outside the decompressed stream.");

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
        if (!_disposed && !_leaveOpen)
            await _source.DisposeAsync().ConfigureAwait(false);

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing && !_leaveOpen)
            _source.Dispose();

        _disposed = true;
        base.Dispose(disposing);
    }

    private int FindBlock()
    {
        int index = Array.BinarySearch(_offsets, _position);
        index = index >= 0 ? index : ~index - 1;
        while (_offsets[index + 1] <= _position)
            index++;

        return index;
    }

    private void PrepareBlock(int index)
    {
        CompressedBlock block = _blocks[index];
        if (_compressed.Length < block.CompressedLength)
            _compressed = new byte[block.CompressedLength];
        if (_decoded.Length < block.DecompressedLength)
            _decoded = new byte[block.DecompressedLength];

        _source.Position = checked(_start + block.CompressedOffset);
        _cachedBlock = -1;
    }

    private void DecodeBlock(int index)
    {
        CompressedBlock block = _blocks[index];
        int written = _decoder(index, _compressed.AsSpan(0, block.CompressedLength), _decoded.AsSpan(0, block.DecompressedLength));
        if (written != block.DecompressedLength)
            throw new CompressionException($"Block {index} produced {written} bytes; expected {block.DecompressedLength}.");

        _cachedBlock = index;
    }

    private int CopyBlock(int index, Span<byte> buffer)
    {
        int offset = (int)(_position - _offsets[index]);
        int count = Math.Min(buffer.Length, _blocks[index].DecompressedLength - offset);
        _decoded.AsSpan(offset, count).CopyTo(buffer);
        _position += count;
        return count;
    }
}
