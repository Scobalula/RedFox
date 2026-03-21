namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegBitWriter(Stream stream)
{
    private readonly Stream _stream = stream;
    private int _bitBuffer;
    private int _bitsInBuffer;

    public void WriteBits(int value, int count)
    {
        _bitBuffer = (_bitBuffer << count) | (value & ((1 << count) - 1));
        _bitsInBuffer += count;

        while (_bitsInBuffer >= 8)
        {
            _bitsInBuffer -= 8;
            int b = (_bitBuffer >> _bitsInBuffer) & 0xFF;
            _stream.WriteByte((byte)b);

            if (b == 0xFF)
                _stream.WriteByte(0x00);
        }
    }

    public void Flush()
    {
        if (_bitsInBuffer > 0)
        {
            int padBits = 8 - _bitsInBuffer;
            WriteBits((1 << padBits) - 1, padBits);
        }
    }
}
