using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Lightweight sequential bit reader for block-compressed format decoding.
    /// Reads bits LSB-first from a byte span, maintaining a running position.
    /// </summary>
    public ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        /// <summary>
        /// Initializes a new <see cref="BitReader"/> over the specified data.
        /// </summary>
        /// <param name="data">The source byte span to read bits from.</param>
        public BitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

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
            uint result = 0;
            for (int i = 0; i < numBits; i++)
            {
                int byteIndex = _position >> 3;
                int bitIndex = _position & 7;
                result |= (uint)((_data[byteIndex] >> bitIndex) & 1) << i;
                _position++;
            }
            return result;
        }

        /// <summary>
        /// Reads a single bit at position <paramref name="p"/> without advancing the reader position.
        /// </summary>
        /// <param name="p">The zero-based bit position to read.</param>
        /// <returns>0 or 1.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int Bit(int p) => (_data[p >> 3] >> (p & 7)) & 1;

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
            int r = 0;
            for (int i = 0; i < count; i++)
                r |= Bit(start + i) << i;
            return r;
        }

        /// <summary>
        /// Advances the bit position by <paramref name="numBits"/> without reading.
        /// </summary>
        /// <param name="numBits">The number of bits to skip.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int numBits) => _position += numBits;
    }
}
