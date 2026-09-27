using System;
using System.Runtime.CompilerServices;

namespace RedFox.Imaging.BlockCompression;

/// <summary>
/// Lightweight sequential bit writer for block-compressed format encoding.
/// Writes bits LSB-first into a byte span, maintaining a running position.
/// </summary>
/// <param name="data">The destination byte span to write bits into.</param>
internal ref struct BcnBitWriter(Span<byte> data)
{
    private readonly Span<byte> _data = data;
    private int _position;

    /// <summary>
    /// Gets the current bit position within the data.
    /// </summary>
    public readonly int Position => _position;

    /// <summary>
    /// Writes <paramref name="numBits"/> bits of <paramref name="value"/> at the current position and advances.
    /// Bits are written LSB-first.
    /// </summary>
    /// <param name="value">The value whose lower bits will be written.</param>
    /// <param name="numBits">The number of bits to write (0–32).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(uint value, int numBits)
    {
        for (int i = 0; i < numBits; i++)
        {
            int byteIndex = _position >> 3;
            int bitIndex = _position & 7;
            _data[byteIndex] |= (byte)(((value >> i) & 1) << bitIndex);
            _position++;
        }
    }

    /// <summary>
    /// Writes a single bit at the specified absolute position without advancing the writer position.
    /// </summary>
    /// <param name="bitPosition">The zero-based bit position to write.</param>
    /// <param name="value">The bit value (0 or 1).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void SetBit(int bitPosition, int value) => _data[bitPosition >> 3] |= (byte)((value & 1) << (bitPosition & 7));

    /// <summary>
    /// Writes <paramref name="count"/> bits of <paramref name="value"/> starting at position <paramref name="start"/>
    /// without advancing the writer position. Bits are packed LSB-first.
    /// </summary>
    /// <param name="start">The zero-based starting bit position.</param>
    /// <param name="value">The value whose lower bits will be written.</param>
    /// <param name="count">The number of bits to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void SetBits(int start, int value, int count)
    {
        for (int i = 0; i < count; i++)
            SetBit(start + i, (value >> i) & 1);
    }

    /// <summary>
    /// Advances the bit position by <paramref name="numBits"/> without writing.
    /// </summary>
    /// <param name="numBits">The number of bits to skip.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int numBits) => _position += numBits;
}
