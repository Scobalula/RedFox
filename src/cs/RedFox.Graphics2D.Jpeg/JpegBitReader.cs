namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Reads individual bits from a JPEG entropy-coded data stream, handling byte-stuffing and marker detection.
/// </summary>
public sealed class JpegBitReader(Stream stream)
{
    private readonly Stream _stream = stream;
    private int _bitBuffer;
    private int _bitsRemaining;
    private bool _hitMarker;
    private JpegMarker _pendingMarker;

    /// <summary>Gets a value indicating whether a JPEG marker was encountered during reading.</summary>
    public bool HitMarker => _hitMarker;

    /// <summary>Gets the marker byte that was encountered, if <see cref="HitMarker"/> is <c>true</c>.</summary>
    public JpegMarker PendingMarker => _pendingMarker;

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

    private bool TryReadByteStuffed(out int value)
    {
        value = 0;

        if (_hitMarker)
        {
            return false;
        }

        int b = _stream.ReadByte();
        if (b < 0)
            throw new InvalidDataException("Unexpected end of JPEG stream.");

        if (b != 0xFF)
        {
            value = b;
            return true;
        }

        int next = _stream.ReadByte();

        if (next < 0)
            throw new InvalidDataException("Unexpected end of JPEG stream after 0xFF.");

        if (next == 0x00)
        {
            value = 0xFF;
            return true;
        }

        while (next == 0xFF)
        {
            next = _stream.ReadByte();
            if (next < 0)
                throw new InvalidDataException("Unexpected end of JPEG stream.");
        }

        _hitMarker = true;
        _pendingMarker = (JpegMarker)next;
        return false;
    }

    /// <summary>Discards any remaining bits in the buffer, aligning the read position to the next byte boundary.</summary>
    public void AlignToByte()
    {
        _bitsRemaining = 0;
        _bitBuffer = 0;
    }
}
