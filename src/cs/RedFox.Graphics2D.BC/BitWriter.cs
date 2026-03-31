using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Lightweight sequential bit writer for block-compressed format encoding.
    /// Writes bits LSB-first into a byte span, maintaining a running position.
    /// </summary>
    public ref struct BitWriter
    {
        private readonly Span<byte> _data;
        private int _position;

        /// <summary>
        /// Initializes a new <see cref="BitWriter"/> over the specified data span.
        /// The span should be pre-zeroed before writing.
        /// </summary>
        /// <param name="data">The destination byte span to write bits into.</param>
        public BitWriter(Span<byte> data)
        {
            _data = data;
            _position = 0;
        }

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
        /// <param name="p">The zero-based bit position to write.</param>
        /// <param name="value">The bit value (0 or 1).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void SetBit(int p, int value) =>
            _data[p >> 3] |= (byte)((value & 1) << (p & 7));

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
}
