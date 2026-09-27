using System;
using System.Buffers;
using System.IO;

namespace RedFox.Imaging.Formats.Jpeg;

/// <summary>
/// Reads individual bits from a JPEG entropy-coded data stream, handling byte-stuffing and marker detection.
/// </summary>
internal sealed class JpegBitReader : IDisposable
{
    private readonly Stream _stream;
    private readonly MemoryStream? _memoryStream;
    private readonly byte[]? _memoryBuffer;
    private readonly int _memoryOffset;
    private readonly int _memoryEnd;
    private byte[]? _streamBuffer;
    private int _streamBufferPosition;
    private int _streamBufferLength;
    private int _memoryPosition;
    private int _bitBuffer;
    private int _bitsRemaining;
    private bool _hitMarker;
    private JpegMarker _pendingMarker;

    /// <summary>
    /// Initializes a new reader for the specified JPEG entropy stream.
    /// </summary>
    /// <param name="stream">The JPEG entropy-coded stream to read.</param>
    public JpegBitReader(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (stream.GetType() == typeof(MemoryStream) && stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out ArraySegment<byte> segment))
        {
            _memoryStream = memoryStream;
            _memoryBuffer = segment.Array!;
            _memoryOffset = segment.Offset;
            _memoryPosition = checked(segment.Offset + (int)memoryStream.Position);
            _memoryEnd = checked(segment.Offset + segment.Count);
        }
        else if (stream.CanSeek && (stream.GetType() == typeof(FileStream) || stream.GetType() == typeof(MemoryStream)))
        {
            _streamBuffer = ArrayPool<byte>.Shared.Rent(4096);
        }
    }

    /// <summary>Gets a value indicating whether a JPEG marker was encountered during reading.</summary>
    public bool HitMarker => _hitMarker;

    /// <summary>Gets the marker byte that was encountered, if <see cref="HitMarker"/> is <c>true</c>.</summary>
    public JpegMarker PendingMarker => _pendingMarker;

    /// <summary>
    /// Reads one unprocessed byte from the entropy stream.
    /// </summary>
    /// <returns>The byte value, or -1 when the stream ends.</returns>
    public int ReadRawByte() => ReadSourceByte();

    /// <summary>
    /// Synchronizes the underlying stream position with the bytes consumed by this reader.
    /// </summary>
    public void Dispose()
    {
        if (_memoryStream is not null)
            _memoryStream.Position = _memoryPosition - _memoryOffset;

        if (_streamBuffer is { } buffer)
        {
            try
            {
                int unread = _streamBufferLength - _streamBufferPosition;
                if (unread > 0)
                    _stream.Seek(-unread, SeekOrigin.Current);
            }
            finally
            {
                _streamBuffer = null;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>Resets the bit buffer and clears any pending marker state.</summary>
    public void Reset()
    {
        _bitBuffer = 0;
        _bitsRemaining = 0;
        _hitMarker = false;
        _pendingMarker = default;
    }

    /// <summary>Attempts to read the specified number of bits from the stream.</summary>
    /// <param name="count">The number of bits to read (1–16).</param>
    /// <param name="value">When this method returns <c>true</c>, contains the decoded bit value.</param>
    /// <returns><c>true</c> if the bits were read successfully; <c>false</c> if a marker was hit.</returns>
    public bool TryReadBits(int count, out int value)
    {
        value = 0;

        while (_bitsRemaining < count)
        {
            if (!TryReadByteStuffed(out int nextByte))
            {
                return false;
            }

            _bitBuffer = (_bitBuffer << 8) | nextByte;
            _bitsRemaining += 8;
        }

        _bitsRemaining -= count;
        value = (_bitBuffer >> _bitsRemaining) & ((1 << count) - 1);
        return true;
    }

    /// <summary>
    /// Attempts to inspect the next bits without consuming them.
    /// </summary>
    /// <param name="count">The number of bits to inspect.</param>
    /// <param name="value">When this method returns <c>true</c>, contains the next bit value.</param>
    /// <returns><c>true</c> if the bits were read successfully; otherwise, <c>false</c> if a marker was hit.</returns>
    public bool TryPeekBits(int count, out int value)
    {
        value = 0;

        while (_bitsRemaining < count)
        {
            if (!TryReadByteStuffed(out int nextByte))
                return false;

            _bitBuffer = (_bitBuffer << 8) | nextByte;
            _bitsRemaining += 8;
        }

        value = (_bitBuffer >> (_bitsRemaining - count)) & ((1 << count) - 1);
        return true;
    }

    /// <summary>Attempts to read a single bit from the stream.</summary>
    /// <param name="value">When this method returns <c>true</c>, contains 0 or 1.</param>
    /// <returns><c>true</c> if the bit was read successfully; <c>false</c> if a marker was hit.</returns>
    public bool TryReadBit(out int value)
    {
        value = 0;

        if (_bitsRemaining == 0)
        {
            if (!TryReadByteStuffed(out int nextByte))
            {
                return false;
            }

            _bitBuffer = (_bitBuffer << 8) | nextByte;
            _bitsRemaining = 8;
        }

        _bitsRemaining--;
        value = (_bitBuffer >> _bitsRemaining) & 1;
        return true;
    }

    /// <summary>Discards any remaining bits in the buffer, aligning the read position to the next byte boundary.</summary>
    public void AlignToByte()
    {
        _bitsRemaining = 0;
        _bitBuffer = 0;
    }

    private bool TryReadByteStuffed(out int value)
    {
        value = 0;

        if (_hitMarker)
        {
            return false;
        }

        int b = ReadSourceByte();
        if (b < 0)
            throw new InvalidDataException("Unexpected end of JPEG stream.");

        if (b != 0xFF)
        {
            value = b;
            return true;
        }

        int next = ReadSourceByte();

        if (next < 0)
            throw new InvalidDataException("Unexpected end of JPEG stream after 0xFF.");

        if (next == 0x00)
        {
            value = 0xFF;
            return true;
        }

        while (next == 0xFF)
        {
            next = ReadSourceByte();
            if (next < 0)
                throw new InvalidDataException("Unexpected end of JPEG stream.");
        }

        _hitMarker = true;
        _pendingMarker = (JpegMarker)next;
        return false;
    }

    private int ReadSourceByte()
    {
        if (_memoryBuffer is { } buffer)
        {
            if (_memoryPosition >= _memoryEnd)
                return -1;

            return buffer[_memoryPosition++];
        }

        if (_streamBuffer is { } streamBuffer)
        {
            if (_streamBufferPosition >= _streamBufferLength)
            {
                _streamBufferPosition = 0;
                _streamBufferLength = _stream.Read(streamBuffer, 0, streamBuffer.Length);
                if (_streamBufferLength == 0)
                    return -1;
            }

            return streamBuffer[_streamBufferPosition++];
        }

        return _stream.ReadByte();
    }
}
