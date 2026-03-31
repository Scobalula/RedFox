namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Provides partition, anchor, and interpolation weight tables for BC7 (BPTC) block compression.
    /// BC7 supports 1, 2, or 3 subsets with up to 64 partition shapes per subset count.
    /// </summary>
    internal static class BC7PartitionTable
    {
        /// <summary>
        /// Two-subset partition table (64 entries). Each <see cref="ushort"/> encodes 16 pixels
        /// as a bitmask where bit <c>i</c> indicates the subset assignment (0 or 1) for pixel <c>i</c>.
        /// </summary>
        public static ReadOnlySpan<ushort> Partitions2 =>
        [
            0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80,
            0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000,
            0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE,
            0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C,
            0xAAAA, 0xF0F0, 0x5A5A, 0x33CC, 0x3C3C, 0x55AA, 0x9696, 0xA55A,
            0x73CE, 0x13C8, 0x324C, 0x3BDC, 0x6996, 0xC33C, 0x9966, 0x0660,
            0x0272, 0x04E4, 0x4E40, 0x2720, 0xC936, 0x936C, 0x39C6, 0x639C,
            0x9336, 0x9CC6, 0x817E, 0xE718, 0xCCF0, 0x0FCC, 0x7744, 0xEE22,
        ];

        /// <summary>
        /// Three-subset partition table (64 entries). Each <see cref="uint"/> encodes 16 pixels
        /// with 2 bits per pixel (packed LSB = pixel 0), giving subset indices 0, 1, or 2.
        /// </summary>
        public static ReadOnlySpan<uint> Partitions3 =>
        [
            0xAA685050, 0x6A5A5040, 0x5A5A4200, 0x5450A0A8,
            0xA5A50000, 0xA0A05050, 0x5555A0A0, 0x5A5A5050,
            0xAA550000, 0xAA555500, 0xAAAA5500, 0x90909090,
            0x94949494, 0xA4A4A4A4, 0xA9A59450, 0x2A0A4250,
            0xA5945040, 0x0A425054, 0xA5A5A500, 0x55A0A0A0,
            0xA8A85454, 0x6A6A4040, 0xA4A45000, 0x1A1A0500,
            0x0050A4A4, 0xAAA59090, 0x14696914, 0x69691400,
            0xA08585A0, 0xAA821414, 0x50A4A450, 0x6A5A0200,
            0xA9A58000, 0x5090A0A8, 0xA8A09050, 0x24242424,
            0x00AA5500, 0x24924924, 0x24499224, 0x50A50A50,
            0x500AA550, 0xAAAA4444, 0x66660000, 0xA5A0A5A0,
            0x50A050A0, 0x69286928, 0x44AAAA44, 0x66666600,
            0xAA444444, 0x54A854A8, 0x95809580, 0x96969600,
            0xA85454A8, 0x80959580, 0xAA141414, 0x96960000,
            0xAAAA1414, 0xA05050A0, 0xA0A0A0A0, 0x96000000,
            0x40804080, 0xA9A8A9A8, 0xAAAAAA44, 0x2A4A5254,
        ];

        /// <summary>
        /// Anchor index for subset 1 in two-subset partitions (64 entries).
        /// Pixel 0 is always the anchor for subset 0.
        /// </summary>
        public static ReadOnlySpan<byte> AnchorTable2 =>
        [
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15,  2,  8,  2,  2,  8,  8, 15,
             2,  8,  2,  2,  8,  8,  2,  2,
            15, 15,  6,  8,  2,  8, 15, 15,
             2,  8,  2,  2,  2, 15, 15,  6,
             6,  2,  6,  8, 15, 15,  2,  2,
            15, 15, 15, 15, 15,  2,  2, 15,
        ];

        /// <summary>
        /// Anchor index for subset 1 in three-subset partitions (64 entries).
        /// </summary>
        public static ReadOnlySpan<byte> AnchorTable3a =>
        [
             3,  3, 15, 15,  8,  3, 15, 15,
             8,  8,  6,  6,  6,  5,  3,  3,
             3,  3,  8, 15,  3,  3,  6, 10,
             5,  8,  8,  6,  8,  5, 15, 15,
             8, 15,  3,  5,  6, 10,  8, 15,
            15,  3, 15,  5, 15, 15, 15, 15,
             3, 15,  5,  5,  5,  8,  5, 10,
             5, 10,  8, 13, 15, 12,  3,  3,
        ];

        /// <summary>
        /// Anchor index for subset 2 in three-subset partitions (64 entries).
        /// </summary>
        public static ReadOnlySpan<byte> AnchorTable3b =>
        [
            15,  8,  8,  3, 15, 15,  3,  8,
            15, 15, 15, 15, 15, 15, 15,  8,
            15,  8, 15,  3, 15,  8, 15,  8,
             3, 15,  6, 10, 15, 15, 10,  8,
            15,  3, 15, 10, 10,  8,  9, 10,
             6, 15,  8, 15,  3,  6,  6,  8,
            15,  3, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15,  3, 15, 15,  8,
        ];

        /// <summary>
        /// Interpolation weights for 2-bit indices (4 entries, range 0–64).
        /// </summary>
        public static ReadOnlySpan<byte> Weights2 => [0, 21, 43, 64];

        /// <summary>
        /// Interpolation weights for 3-bit indices (8 entries, range 0–64).
        /// </summary>
        public static ReadOnlySpan<byte> Weights3 => [0, 9, 18, 27, 37, 46, 55, 64];

        /// <summary>
        /// Interpolation weights for 4-bit indices (16 entries, range 0–64).
        /// </summary>
        public static ReadOnlySpan<byte> Weights4 => [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];

        /// <summary>
        /// Returns the interpolation weight table for the specified index bit width.
        /// </summary>
        /// <param name="indexBits">The number of bits per index (2, 3, or 4).</param>
        /// <returns>The corresponding weight table.</returns>
        public static ReadOnlySpan<byte> GetWeights(int indexBits) => indexBits switch
        {
            2 => Weights2,
            3 => Weights3,
            4 => Weights4,
            _ => throw new ArgumentOutOfRangeException(nameof(indexBits), indexBits, "Supported index bit widths are 2, 3, and 4."),
        };

        /// <summary>
        /// Returns the subset index for a given pixel, partition, and subset count.
        /// </summary>
        /// <param name="numSubsets">The number of subsets (1, 2, or 3).</param>
        /// <param name="partition">The partition shape index.</param>
        /// <param name="pixelIndex">The pixel index (0–15).</param>
        /// <returns>The subset index for the pixel.</returns>
        public static int GetSubset(int numSubsets, int partition, int pixelIndex)
        {
            if (numSubsets == 1) return 0;
            if (numSubsets == 2) return (Partitions2[partition] >> pixelIndex) & 1;
            return (int)(Partitions3[partition] >> (pixelIndex * 2)) & 3;
        }

        /// <summary>
        /// Determines whether a pixel is an anchor index that requires one fewer index bit.
        /// </summary>
        /// <param name="numSubsets">The number of subsets (1, 2, or 3).</param>
        /// <param name="partition">The partition shape index.</param>
        /// <param name="pixelIndex">The pixel index (0–15).</param>
        /// <returns>True if the pixel is an anchor index, false otherwise.</returns>
        public static bool IsAnchorIndex(int numSubsets, int partition, int pixelIndex)
        {
            if (pixelIndex == 0) return true;
            if (numSubsets == 1) return false;
            if (numSubsets == 2) return pixelIndex == AnchorTable2[partition];
            return pixelIndex == AnchorTable3a[partition] || pixelIndex == AnchorTable3b[partition];
        }
    }
}
