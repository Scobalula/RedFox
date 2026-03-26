namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Writes individual bits into a JPEG entropy-coded data stream, handling byte-stuffing for 0xFF values.
/// </summary>
public sealed class JpegBitWriter(Stream stream)
{
    private readonly Stream _stream = stream;
    private int _bitBuffer;
    private int _bitsInBuffer;

    /// <summary>Writes the specified number of least-significant bits to the stream.</summary>
    /// <param name="value">The bit value to write.</param>
    /// <param name="count">The number of bits to write (1–16).</param>
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

    /// <summary>Pads the remaining bits with ones and flushes the buffer to the underlying stream.</summary>
    public void Flush()
    {
        if (_bitsInBuffer > 0)
        {
            int padBits = 8 - _bitsInBuffer;
            WriteBits((1 << padBits) - 1, padBits);
        }
    }
}
