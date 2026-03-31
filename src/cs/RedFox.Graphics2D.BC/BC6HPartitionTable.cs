namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Provides partition, anchor, and interpolation weight tables for BC6H block compression.
    /// BC6H uses 32 two-subset partitions and two weight table sizes (3-bit and 4-bit indices).
    /// </summary>
    public static class BC6HPartitionTable
    {
        /// <summary>
        /// Two-subset partition table (32 entries). Each <see cref="ushort"/> encodes 16 pixels
        /// as a bitmask where bit <c>i</c> indicates the subset assignment (0 or 1) for pixel <c>i</c>.
        /// </summary>
        public static ReadOnlySpan<ushort> Partitions2 =>
        [
            0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80,
            0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000,
            0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE,
            0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C,
        ];

        /// <summary>
        /// Anchor index for the second subset in each of the 32 two-subset partitions.
        /// The first subset anchor is always pixel 0.
        /// </summary>
        public static ReadOnlySpan<byte> AnchorTable =>
        [
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15,  2,  8,  2,  2,  8,  8, 15,
             2,  8,  2,  2,  8,  8,  2,  2,
        ];

        /// <summary>
        /// Interpolation weights for 3-bit indices (8 entries, range 0–64).
        /// </summary>
        public static ReadOnlySpan<byte> Weights3 => [0, 9, 18, 27, 37, 46, 55, 64];

        /// <summary>
        /// Interpolation weights for 4-bit indices (16 entries, range 0–64).
        /// </summary>
        public static ReadOnlySpan<byte> Weights4 => [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];
    }
}
