using System;
using System.IO;

namespace RedFox.Imaging.Formats.Jpeg;

/// <summary>
/// A JPEG Huffman decoding table built from code-length and value arrays. Supports variable-length symbol decoding from a <see cref="JpegBitReader"/>.
/// </summary>
internal sealed class JpegHuffmanTable
{
    private readonly int[] _minCode = new int[17];
    private readonly int[] _maxCode = new int[17];
    private readonly int[] _valPtr = new int[17];
    private readonly byte[] _values;
    private readonly byte[] _fastBitCounts = new byte[256];
    private readonly byte[] _fastValues = new byte[256];

    /// <summary>Creates a Huffman decoding table from JPEG DHT segment data.</summary>
    /// <param name="codeLengths">A 16-element span giving the count of codes at each bit length (1–16).</param>
    /// <param name="values">The Huffman symbol values ordered by code length.</param>
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

        for (int bits = 1; bits <= 8; bits++)
        {
            if (_maxCode[bits] < 0)
                continue;

            for (int codeValue = _minCode[bits]; codeValue <= _maxCode[bits]; codeValue++)
            {
                int firstIndex = codeValue << (8 - bits);
                int fillCount = 1 << (8 - bits);
                byte symbol = _values[_valPtr[bits] + codeValue - _minCode[bits]];
                Array.Fill(_fastBitCounts, (byte)bits, firstIndex, fillCount);
                Array.Fill(_fastValues, symbol, firstIndex, fillCount);
            }
        }
    }

    /// <summary>Attempts to decode the next Huffman symbol from the bit reader.</summary>
    /// <param name="reader">The bit reader to consume bits from.</param>
    /// <param name="value">When this method returns <c>true</c>, contains the decoded symbol value.</param>
    /// <returns><c>true</c> if a symbol was decoded; <c>false</c> if reading was interrupted by a marker.</returns>
    public bool TryDecode(JpegBitReader reader, out int value)
    {
        value = 0;

        if (reader.TryPeekBits(8, out int prefix))
        {
            byte fastBitCount = _fastBitCounts[prefix];
            if (fastBitCount > 0)
            {
                reader.TryReadBits(fastBitCount, out _);
                value = _fastValues[prefix];
                return true;
            }
        }

        int code = 0;

        for (int bits = 1; bits <= 16; bits++)
        {
            if (!reader.TryReadBit(out int bit))
            {
                return false;
            }

            code = (code << 1) | bit;

            if (_maxCode[bits] >= 0 && code >= _minCode[bits] && code <= _maxCode[bits])
            {
                int index = _valPtr[bits] + (code - _minCode[bits]);
                value = _values[index];
                return true;
            }
        }

        throw new InvalidDataException("Invalid Huffman code in JPEG stream.");
    }

    /// <summary>Extends a received Huffman value to its signed representation using the JPEG sign-extension rule.</summary>
    /// <param name="value">The unsigned value read from the bitstream.</param>
    /// <param name="bits">The number of additional bits (category).</param>
    /// <returns>The sign-extended coefficient value.</returns>
    public static int Extend(int value, int bits)
    {
        if (bits == 0)
            return 0;

        int threshold = 1 << (bits - 1);
        return value >= threshold ? value : value - (2 * threshold - 1);
    }
}
