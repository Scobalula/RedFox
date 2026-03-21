namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegHuffmanTable
{
    private readonly int[] _minCode = new int[17];
    private readonly int[] _maxCode = new int[17];
    private readonly int[] _valPtr = new int[17];
    private readonly byte[] _values;

    public JpegHuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values)
    {
        _values = values.ToArray();

        int code = 0;
        int valueIndex = 0;

        for (int bits = 1; bits <= 16; bits++)
        {
            int count = codeLengths[bits - 1];

            if (count == 0)
            {
                _minCode[bits] = -1;
                _maxCode[bits] = -1;
                _valPtr[bits] = 0;
            }
            else
            {
                _valPtr[bits] = valueIndex;
                _minCode[bits] = code;
                code += count;
                _maxCode[bits] = code - 1;
                valueIndex += count;
            }

            code <<= 1;
        }
    }

    public int Decode(JpegBitReader reader)
    {
        int code = 0;

        for (int bits = 1; bits <= 16; bits++)
        {
            code = (code << 1) | reader.ReadBit();

            if (_maxCode[bits] >= 0 && code <= _maxCode[bits])
            {
                int index = _valPtr[bits] + (code - _minCode[bits]);
                return _values[index];
            }
        }

        throw new InvalidDataException("Invalid Huffman code in JPEG stream.");
    }

    public static int Extend(int value, int bits)
    {
        if (bits == 0)
            return 0;

        int threshold = 1 << (bits - 1);
        return value >= threshold ? value : value - (2 * threshold - 1);
    }
}
