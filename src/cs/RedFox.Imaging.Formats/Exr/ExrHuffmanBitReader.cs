using System;
using System.IO;

namespace RedFox.Imaging.Formats.Exr;

/// <summary>
/// Maintains a forward-only bitstream cursor over an MSB-first PIZ Huffman payload.
/// </summary>
/// <param name="data">The payload bytes.</param>
/// <param name="totalBits">The number of meaningful bits in <paramref name="data"/>.</param>
internal ref struct ExrHuffmanBitReader(ReadOnlySpan<byte> data, int totalBits)
{
    private readonly ReadOnlySpan<byte> _data = data;

    private readonly int _totalBits = totalBits;

    private int _byteOffset;

    private int _bitsConsumed;

    private ulong _buffer;

    private int _bufferedBits;

    /// <summary>
    /// Gets the number of bits remaining in the logical payload.
    /// </summary>
    public readonly int BitsRemaining => _totalBits - _bitsConsumed;

    /// <summary>
    /// Reads a fixed number of bits from the stream.
    /// </summary>
    /// <param name="bitCount">The number of bits to read (0 to 58).</param>
    /// <returns>The bits read, MSB first.</returns>
    public ulong ReadBits(int bitCount)
    {
        if (!TryPeekBits(bitCount, out ulong value))
            throw new InvalidDataException("Unexpected end of Huffman data.");

        SkipBits(bitCount);
        return value;
    }

    /// <summary>
    /// Peeks ahead without consuming bits.
    /// </summary>
    /// <param name="bitCount">The number of bits to peek (0 to 58).</param>
    /// <param name="value">The peeked bits when available.</param>
    /// <returns><see langword="true"/> when enough bits remain; otherwise <see langword="false"/>.</returns>
    public bool TryPeekBits(int bitCount, out ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bitCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitCount, 58);

        if (bitCount > BitsRemaining)
        {
            value = 0;
            return false;
        }

        EnsureBits(bitCount);
        value = (_buffer >> (_bufferedBits - bitCount)) & ((1UL << bitCount) - 1);
        return true;
    }

    /// <summary>
    /// Consumes a fixed number of bits that were previously peeked.
    /// </summary>
    /// <param name="bitCount">The number of bits to consume.</param>
    public void SkipBits(int bitCount)
    {
        EnsureBits(bitCount);
        _bufferedBits -= bitCount;
        _bitsConsumed += bitCount;
        _buffer = _bufferedBits == 0 ? 0 : _buffer & ((1UL << _bufferedBits) - 1);
    }

    private void EnsureBits(int bitCount)
    {
        while (_bufferedBits < bitCount)
        {
            int streamBitsRemaining = _totalBits - (_bitsConsumed + _bufferedBits);

            if (streamBitsRemaining <= 0 || _byteOffset >= _data.Length)
                throw new InvalidDataException("Unexpected end of Huffman data.");

            int bitsToAppend = Math.Min(Math.Min(8, streamBitsRemaining), bitCount - _bufferedBits);
            byte nextByte = _data[_byteOffset++];

            if (bitsToAppend < 8)
                nextByte = (byte)(nextByte >> (8 - bitsToAppend));

            _buffer = (_buffer << bitsToAppend) | nextByte;
            _bufferedBits += bitsToAppend;
        }
    }
}
