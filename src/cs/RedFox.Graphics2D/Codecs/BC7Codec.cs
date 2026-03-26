using System.Numerics;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for BC7 (BPTC) block-compressed format.
    /// Each 16-byte (128-bit) block encodes a 4×4 pixel block using one of 8 modes
    /// with variable color/alpha precision, 1–3 subsets, and optional rotation.
    /// </summary>
    public sealed class BC7Codec(ImageFormat format) : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format { get; } = format;

        /// <inheritdoc/>
        public int BytesPerPixel => 0;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            BCHelper.DecodeBlocks(source, destination, width, height, 16, DecodeBlock);
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            throw new NotSupportedException("BC7 encoding is not implemented.");
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel reads.");

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int x, int y, int width)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blockOffset = ((y / 4) * blocksX + (x / 4)) * 16;
            Span<Vector4> blockPixels = stackalloc Vector4[16];
            DecodeBlock(source.Slice(blockOffset, 16), blockPixels);
            return blockPixels[(y % 4) * 4 + (x % 4)];
        }

        public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height)
        {
            BCHelper.DecodeRows(source, destination, startRow, rowCount, width, height, 16, DecodeBlock);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

        /// <inheritdoc/>
        public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            BCHelper.DecodeBlocksTo(source, targetCodec, destination, width, height, 16, DecodeBlock);
        }

        #region Tables

        // Mode parameters: NumSubsets, PartitionBits, RotationBits, IndexSelectionBits,
        //                   ColorBits, AlphaBits, EndpointPBits, SharedPBits,
        //                   IndexBits, SecondaryIndexBits
        private static readonly ModeInfo[] Modes =
        [
            new(3, 4, 0, 0, 4, 0, 1, 0, 3, 0), // Mode 0
            new(2, 6, 0, 0, 6, 0, 0, 1, 3, 0), // Mode 1
            new(3, 6, 0, 0, 5, 0, 0, 0, 2, 0), // Mode 2
            new(2, 6, 0, 0, 7, 0, 1, 0, 2, 0), // Mode 3
            new(1, 0, 2, 1, 5, 6, 0, 0, 2, 3), // Mode 4
            new(1, 0, 2, 0, 7, 8, 0, 0, 2, 2), // Mode 5
            new(1, 0, 0, 0, 7, 7, 1, 0, 4, 0), // Mode 6
            new(2, 6, 0, 0, 5, 5, 1, 0, 2, 0), // Mode 7
        ];

        private readonly record struct ModeInfo(int NumSubsets, int PartitionBits, int RotationBits, int IndexSelectionBits, int ColorBits, int AlphaBits, int EndpointPBits, int SharedPBits, int IndexBits, int SecondaryIndexBits);

        // 2-subset partition table: bit i = subset index for pixel i.
        private static ReadOnlySpan<ushort> Partitions2 =>
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

        // 3-subset partition table: 2 bits per pixel, packed LSB = pixel 0.
        private static ReadOnlySpan<uint> Partitions3 =>
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

        // Anchor index for subset 1 in 2-subset partitions.
        private static ReadOnlySpan<byte> AnchorTable2 =>
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

        // Anchor index for subset 1 in 3-subset partitions.
        private static ReadOnlySpan<byte> AnchorTable3a =>
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

        // Anchor index for subset 2 in 3-subset partitions.
        private static ReadOnlySpan<byte> AnchorTable3b =>
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

        // Interpolation weight tables indexed by decoded index value.
        private static ReadOnlySpan<byte> Weights2 => [0, 21, 43, 64];
        private static ReadOnlySpan<byte> Weights3 => [0, 9, 18, 27, 37, 46, 55, 64];
        private static ReadOnlySpan<byte> Weights4 => [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetSubset(int numSubsets, int partition, int pixelIndex)
        {
            if (numSubsets == 1) return 0;
            if (numSubsets == 2) return (Partitions2[partition] >> pixelIndex) & 1;
            return (int)(Partitions3[partition] >> (pixelIndex * 2)) & 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAnchorIndex(int numSubsets, int partition, int pixelIndex)
        {
            if (pixelIndex == 0) return true;
            if (numSubsets == 1) return false;
            if (numSubsets == 2) return pixelIndex == AnchorTable2[partition];
            return pixelIndex == AnchorTable3a[partition] || pixelIndex == AnchorTable3b[partition];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Unquantize(int value, int precision)
        {
            if (precision >= 8) return value;
            return (value << (8 - precision)) | (value >> (2 * precision - 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Interpolate(int e0, int e1, int weight)
        {
            return ((64 - weight) * e0 + weight * e1 + 32) >> 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<byte> GetWeights(int indexBits) => indexBits switch
        {
            2 => Weights2,
            3 => Weights3,
            4 => Weights4,
            _ => throw new InvalidOperationException($"Unsupported index bit count: {indexBits}"),
        };

        private ref struct BitReader
        {
            private readonly ReadOnlySpan<byte> _data;
            private int _position;

            public BitReader(ReadOnlySpan<byte> data) { _data = data; _position = 0; }

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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Skip(int numBits) => _position += numBits;
        }

        private static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
        {
            // Determine mode from position of first set bit in the first byte.
            int mode = 0;
            while (mode < 8 && (block[0] & (1 << mode)) == 0)
                mode++;

            if (mode >= 8)
            {
                pixels.Clear();
                return;
            }

            var info = Modes[mode];
            var reader = new BitReader(block);
            reader.Skip(mode + 1);

            int partition = (int)reader.Read(info.PartitionBits);
            int rotation = (int)reader.Read(info.RotationBits);
            int indexSelection = (int)reader.Read(info.IndexSelectionBits);

            int numEndpoints = info.NumSubsets * 2;

            // Read color and alpha endpoints (max 6 endpoints for 3 subsets).
            Span<int> r = stackalloc int[6];
            Span<int> g = stackalloc int[6];
            Span<int> b = stackalloc int[6];
            Span<int> a = stackalloc int[6];

            for (int i = 0; i < numEndpoints; i++) r[i] = (int)reader.Read(info.ColorBits);
            for (int i = 0; i < numEndpoints; i++) g[i] = (int)reader.Read(info.ColorBits);
            for (int i = 0; i < numEndpoints; i++) b[i] = (int)reader.Read(info.ColorBits);

            if (info.AlphaBits > 0)
            {
                for (int i = 0; i < numEndpoints; i++) a[i] = (int)reader.Read(info.AlphaBits);
            }

            // Apply P-bits.
            if (info.EndpointPBits != 0)
            {
                for (int i = 0; i < numEndpoints; i++)
                {
                    int pbit = (int)reader.Read(1);
                    r[i] = (r[i] << 1) | pbit;
                    g[i] = (g[i] << 1) | pbit;
                    b[i] = (b[i] << 1) | pbit;
                    if (info.AlphaBits > 0)
                        a[i] = (a[i] << 1) | pbit;
                }
            }
            else if (info.SharedPBits != 0)
            {
                for (int s = 0; s < info.NumSubsets; s++)
                {
                    int pbit = (int)reader.Read(1);
                    int i0 = s * 2;
                    int i1 = i0 + 1;
                    r[i0] = (r[i0] << 1) | pbit;
                    g[i0] = (g[i0] << 1) | pbit;
                    b[i0] = (b[i0] << 1) | pbit;
                    r[i1] = (r[i1] << 1) | pbit;
                    g[i1] = (g[i1] << 1) | pbit;
                    b[i1] = (b[i1] << 1) | pbit;
                }
            }

            // Unquantize endpoints to 8-bit precision.
            int colorPrec = info.ColorBits + ((info.EndpointPBits | info.SharedPBits) != 0 ? 1 : 0);
            int alphaPrec = info.AlphaBits > 0
                ? info.AlphaBits + (info.EndpointPBits != 0 ? 1 : 0)
                : 0;

            for (int i = 0; i < numEndpoints; i++)
            {
                r[i] = Unquantize(r[i], colorPrec);
                g[i] = Unquantize(g[i], colorPrec);
                b[i] = Unquantize(b[i], colorPrec);
                a[i] = alphaPrec > 0 ? Unquantize(a[i], alphaPrec) : 255;
            }

            // Read primary index set.
            Span<int> primaryIdx = stackalloc int[16];
            var primaryWeights = GetWeights(info.IndexBits);

            for (int i = 0; i < 16; i++)
            {
                bool anchor = IsAnchorIndex(info.NumSubsets, partition, i);
                primaryIdx[i] = (int)reader.Read(anchor ? info.IndexBits - 1 : info.IndexBits);
            }

            // Read secondary index set (modes 4, 5 only).
            Span<int> secondaryIdx = stackalloc int[16];
            ReadOnlySpan<byte> secondaryWeights = default;
            bool hasDualIndices = info.SecondaryIndexBits > 0;

            if (hasDualIndices)
            {
                secondaryWeights = GetWeights(info.SecondaryIndexBits);
                for (int i = 0; i < 16; i++)
                {
                    // Single subset → only pixel 0 is an anchor.
                    secondaryIdx[i] = (int)reader.Read(i == 0 ? info.SecondaryIndexBits - 1 : info.SecondaryIndexBits);
                }
            }

            // Interpolate and emit pixels.
            for (int i = 0; i < 16; i++)
            {
                int subset = GetSubset(info.NumSubsets, partition, i);
                int e0 = subset * 2;
                int e1 = e0 + 1;

                int pr, pg, pb, pa;

                if (hasDualIndices)
                {
                    // indexSelection swaps which index set drives color vs alpha.
                    int cIdx, aIdx;
                    ReadOnlySpan<byte> cw, aw;

                    if (indexSelection != 0)
                    {
                        cIdx = secondaryIdx[i];
                        aIdx = primaryIdx[i];
                        cw = secondaryWeights;
                        aw = primaryWeights;
                    }
                    else
                    {
                        cIdx = primaryIdx[i];
                        aIdx = secondaryIdx[i];
                        cw = primaryWeights;
                        aw = secondaryWeights;
                    }

                    int cwt = cw[cIdx];
                    int awt = aw[aIdx];
                    pr = Interpolate(r[e0], r[e1], cwt);
                    pg = Interpolate(g[e0], g[e1], cwt);
                    pb = Interpolate(b[e0], b[e1], cwt);
                    pa = Interpolate(a[e0], a[e1], awt);
                }
                else
                {
                    int w = primaryWeights[primaryIdx[i]];
                    pr = Interpolate(r[e0], r[e1], w);
                    pg = Interpolate(g[e0], g[e1], w);
                    pb = Interpolate(b[e0], b[e1], w);
                    pa = Interpolate(a[e0], a[e1], w);
                }

                switch (rotation)
                {
                    case 1: (pa, pr) = (pr, pa); break;
                    case 2: (pa, pg) = (pg, pa); break;
                    case 3: (pa, pb) = (pb, pa); break;
                }

                pixels[i] = new Vector4(pr / 255f, pg / 255f, pb / 255f, pa / 255f);
            }
        }
    }
}
