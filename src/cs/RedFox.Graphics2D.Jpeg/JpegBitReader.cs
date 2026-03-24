namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegBitReader(Stream stream)
{
    private readonly Stream _stream = stream;
    private int _bitBuffer;
    private int _bitsRemaining;
    private bool _hitMarker;
    private JpegMarker _pendingMarker;

    public bool HitMarker => _hitMarker;
    public JpegMarker PendingMarker => _pendingMarker;

    public void Reset()
    {
        _bitBuffer = 0;
        _bitsRemaining = 0;
        _hitMarker = false;
        _pendingMarker = default;
    }

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

    public void AlignToByte()
    {
        _bitsRemaining = 0;
        _bitBuffer = 0;
    }
}
