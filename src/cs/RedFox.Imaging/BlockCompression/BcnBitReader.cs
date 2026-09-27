using System;
using System.Runtime.CompilerServices;

namespace RedFox.Imaging.BlockCompression;

/// <summary>
/// Lightweight sequential bit reader for block-compressed format decoding.
/// Reads bits LSB-first from a byte span, maintaining a running position.
/// </summary>
/// <param name="data">The source byte span to read bits from.</param>
internal ref struct BcnBitReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private int _position;

    /// <summary>
    /// Gets the current bit position within the data.
    /// </summary>
    public readonly int Position => _position;

    /// <summary>
    /// Reads <paramref name="numBits"/> bits from the current position and advances.
    /// Bits are read LSB-first: bit 0 of the result corresponds to the first bit read.
    /// </summary>
    /// <param name="numBits">The number of bits to read (0–32).</param>
    /// <returns>The value composed from the read bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Read(int numBits)
    {
        uint value = 0;
        for (int i = 0; i < numBits; i++)
        {
            int byteIndex = _position >> 3;
            int bitIndex = _position & 7;
            value |= (uint)((_data[byteIndex] >> bitIndex) & 1) << i;
            _position++;
        }
        return value;
    }

    /// <summary>
    /// Reads a single bit at position <paramref name="bitPosition"/> without advancing the reader position.
    /// </summary>
    /// <param name="bitPosition">The zero-based bit position to read.</param>
    /// <returns>0 or 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int Bit(int bitPosition) => (_data[bitPosition >> 3] >> (bitPosition & 7)) & 1;

    /// <summary>
    /// Reads <paramref name="count"/> bits starting at position <paramref name="start"/>
    /// without advancing the reader position. Bits are packed LSB-first.
    /// </summary>
    /// <param name="start">The zero-based starting bit position.</param>
    /// <param name="count">The number of bits to read.</param>
    /// <returns>The value composed from the read bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int Bits(int start, int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++)
            value |= Bit(start + i) << i;
        return value;
    }

    /// <summary>
    /// Advances the bit position by <paramref name="numBits"/> without reading.
    /// </summary>
    /// <param name="numBits">The number of bits to skip.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int numBits) => _position += numBits;
}
