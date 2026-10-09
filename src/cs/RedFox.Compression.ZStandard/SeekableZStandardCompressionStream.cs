using System.IO.Hashing;

namespace RedFox.Compression.ZStandard;

internal sealed class SeekableZStandardCompressionStream : Stream
{
    private readonly Stream _destination;
    private readonly bool _leaveOpen;
    private readonly long? _expectedSize;
    private readonly bool _checksums;
    private readonly ZStandardCodec _codec;
    private readonly byte[] _frame;
    private readonly byte[] _compressed;
    private readonly MemoryStream _table = new();
    private int _frameCount;
    private int _buffered;
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

    internal SeekableZStandardCompressionStream(Stream destination, bool leaveOpen, long? size, int level, int frameSize, bool checksums, byte[] dictionary)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("Seekable Zstandard requires writable output.", nameof(destination));
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        _destination = destination;
        _leaveOpen = leaveOpen;
        _expectedSize = size;
        _checksums = checksums;
        _codec = new ZStandardCodec(level, dictionary);
        _frame = new byte[frameSize];
        _compressed = new byte[_codec.GetMaxCompressedSize(frameSize)];
        _table.SetLength(8);
        _table.Position = 8;
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
            if (_buffered == _frame.Length)
                _destination.Write(_compressed.AsSpan(0, CompressFrame()));
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
            if (_buffered == _frame.Length)
            {
                int written = CompressFrame();
                await _destination.WriteAsync(_compressed.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_buffered != 0)
            _destination.Write(_compressed.AsSpan(0, CompressFrame()));

        _destination.Flush();
    }

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_buffered != 0)
        {
            int written = CompressFrame();
            await _destination.WriteAsync(_compressed.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
        }

        await _destination.FlushAsync(cancellationToken).ConfigureAwait(false);
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
                int written = CompressFrame();
                await _destination.WriteAsync(_compressed.AsMemory(0, written)).ConfigureAwait(false);
            }

            await _destination.WriteAsync(FinishTable()).ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            _codec.Dispose();
            _table.Dispose();
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
                    _destination.Write(_compressed.AsSpan(0, CompressFrame()));

                _destination.Write(FinishTable().Span);
            }
            finally
            {
                _disposed = true;
                _codec.Dispose();
                _table.Dispose();
                if (!_leaveOpen)
                    _destination.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void Check(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_expectedSize.HasValue && count > _expectedSize.Value - _position)
            throw new CompressionException("Input exceeds the declared uncompressed size.");
    }

    private int Fill(ReadOnlySpan<byte> buffer)
    {
        int count = Math.Min(buffer.Length, _frame.Length - _buffered);
        buffer[..count].CopyTo(_frame.AsSpan(_buffered));
        _buffered += count;
        _position += count;
        return count;
    }

    private int CompressFrame()
    {
        int written = _codec.Compress(_frame.AsSpan(0, _buffered), _compressed);
        uint checksum = _checksums ? (uint)XxHash64.HashToUInt64(_frame.AsSpan(0, _buffered)) : 0;
        Span<byte> entry = stackalloc byte[12];
        SeekableZStandardCodec.WriteEntry(entry, written, _buffered, checksum, _checksums);
        _table.Write(entry[..(_checksums ? 12 : 8)]);
        _frameCount++;
        _buffered = 0;
        return written;
    }

    private void CheckFinalSize()
    {
        if (_expectedSize.HasValue && _position != _expectedSize.Value)
            throw new CompressionException("Input does not match the declared uncompressed size.");
    }

    private ReadOnlyMemory<byte> FinishTable()
    {
        Span<byte> footer = stackalloc byte[9];
        SeekableZStandardCodec.WriteTableFooter(footer, _frameCount, _checksums);
        _table.Write(footer);
        SeekableZStandardCodec.WriteTableHeader(_table.GetBuffer(), _frameCount, _checksums);
        return _table.GetBuffer().AsMemory(0, (int)_table.Length);
    }
}
