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
    }

    public int ReadBits(int count)
    {
        while (_bitsRemaining < count)
        {
            int b = ReadByteStuffed();
            _bitBuffer = (_bitBuffer << 8) | b;
            _bitsRemaining += 8;
        }

        _bitsRemaining -= count;
        return (_bitBuffer >> _bitsRemaining) & ((1 << count) - 1);
    }

    public int ReadBit()
    {
        if (_bitsRemaining == 0)
        {
            int b = ReadByteStuffed();
            _bitBuffer = (_bitBuffer << 8) | b;
            _bitsRemaining = 8;
        }

        _bitsRemaining--;
        return (_bitBuffer >> _bitsRemaining) & 1;
    }

    private int ReadByteStuffed()
    {
        int b = _stream.ReadByte();
        if (b < 0)
            throw new InvalidDataException("Unexpected end of JPEG stream.");

        if (b != 0xFF)
            return b;

        int next = _stream.ReadByte();

        if (next < 0)
            throw new InvalidDataException("Unexpected end of JPEG stream after 0xFF.");

        if (next == 0x00)
            return 0xFF;

        while (next == 0xFF)
        {
            next = _stream.ReadByte();
            if (next < 0)
                throw new InvalidDataException("Unexpected end of JPEG stream.");
        }

        _hitMarker = true;
        _pendingMarker = (JpegMarker)next;
        throw new JpegEndOfScanException(_pendingMarker);
    }

    public void AlignToByte()
    {
        _bitsRemaining = 0;
        _bitBuffer = 0;
    }
}
