using System.Numerics;
using System.Runtime.CompilerServices;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Codec for BC6H (BPTC float) block-compressed HDR format.
    /// Each 16-byte (128-bit) block encodes a 4×4 pixel block of unsigned or signed
    /// half-precision RGB values using one of 14 modes with 1–2 subsets.
    /// Supports both decoding and encoding.
    /// </summary>
    public sealed class BC6HCodec : IPixelCodec
    {
        private const int BytesPerBlock = 16;

        // 14 modes (0-indexed). Based on the D3D11/DirectXTex BC6H specification.
        // Modes 0-9: 2 subsets, 3-bit indices.
        // Modes 10-13: 1 subset, 4-bit indices.
        private static readonly BC6HModeDescriptor[] Modes =
        [
            new(2, true,  10, 5, 5, 5, 3), //  0: bits=00    (2-bit mode)
            new(2, true,   7, 6, 6, 6, 3), //  1: bits=01    (2-bit mode)
            new(2, true,  11, 5, 4, 4, 3), //  2: bits=00010
            new(2, true,  11, 4, 5, 4, 3), //  3: bits=00110 (was 00101)
            new(2, true,  11, 4, 4, 5, 3), //  4: bits=01010 (was 01001)
            new(2, true,   9, 5, 5, 5, 3), //  5: bits=01110 (was 01101)
            new(2, true,   8, 6, 5, 5, 3), //  6: bits=10010 (was 10001)
            new(2, true,   8, 5, 6, 5, 3), //  7: bits=10110 (was 10101)
            new(2, true,   8, 5, 5, 6, 3), //  8: bits=11010 (was 11001)
            new(2, false,  6, 6, 6, 6, 3), //  9: bits=11110 (was 11101, non-transformed)
            new(1, false, 10,10,10,10, 4), // 10: bits=00011 (was 00010, 1 subset, non-transformed)
            new(1, true,  11, 9, 9, 9, 4), // 11: bits=00111 (was 00110)
            new(1, true,  12, 8, 8, 8, 4), // 12: bits=01011 (was 01010)
            new(1, true,  16, 4, 4, 4, 4), // 13: bits=01111 (was 01110)
        ];

        /// <summary>
        /// Initializes a new instance of the <see cref="BC6HCodec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format (BC6HTypeless, BC6HUF16, or BC6HSF16).</param>
        public BC6HCodec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.BC6HTypeless => format,
                ImageFormat.BC6HUF16 => format,
                ImageFormat.BC6HSF16 => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "BC6HCodec supports only BC6HTypeless, BC6HUF16, and BC6HSF16."),
            };
        }

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 0;

        private bool IsSigned => Format == ImageFormat.BC6HSF16;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.DecodeBlocks(source, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<byte> block, Span<Vector4> pixels) => DecodeBlock(block, pixels, signed));
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.EncodeBlocks(source, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<Vector4> pixels, Span<byte> block) => EncodeBlock(pixels, block, signed));
        }

        /// <summary>
        /// Encodes pixels into BC6H using a reduced CPU search intended to favor throughput over quality.
        /// The fast path currently restricts encoding to the stable single-subset mode 10 candidate.
        /// </summary>
        /// <param name="source">The source HDR RGBA pixels to encode. Alpha is ignored.</param>
        /// <param name="destination">The destination span that receives BC6H blocks.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        public void EncodeFast(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.EncodeBlocks(source, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<Vector4> pixels, Span<byte> block) => EncodeBlockFast(pixels, block, signed));
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel reads by flat index.");

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int x, int y, int width)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blockOffset = ((y / 4) * blocksX + (x / 4)) * BytesPerBlock;
            Span<Vector4> blockPixels = stackalloc Vector4[16];
            DecodeBlock(source.Slice(blockOffset, BytesPerBlock), blockPixels, IsSigned);
            return blockPixels[(y % 4) * 4 + (x % 4)];
        }

        /// <inheritdoc/>
        public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.DecodeRows(source, destination, startRow, rowCount, width, height, BytesPerBlock,
                (ReadOnlySpan<byte> block, Span<Vector4> pixels) => DecodeBlock(block, pixels, signed));
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

        /// <inheritdoc/>
        public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.DecodeBlocksTo(source, targetCodec, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<byte> block, Span<Vector4> pixels) => DecodeBlock(block, pixels, signed));
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            // Decode source pixels to Vector4 buffer
            Vector4[] pixels = new Vector4[width * height];
            sourceCodec.Decode(source, pixels, width, height);

            // Encode to BC blocks
            bool signed = IsSigned;
            BlockProcessor.EncodeBlocks(pixels, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<Vector4> pixels, Span<byte> block) => EncodeBlock(pixels, block, signed));
        }

        #region Decode Helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SignExtend(int v, int bits) => (v << (32 - bits)) >> (32 - bits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Unquantize(int v, int epBits, bool signed)
        {
            if (epBits >= 15) return v;
            if (signed)
            {
                bool neg = v < 0;
                int m = neg ? -v : v;
                if (m == 0) return 0;
                int u = m >= ((1 << (epBits - 1)) - 1)
                    ? 0x7FFF
                    : ((m << 15) + 0x4000) >> (epBits - 1);
                return neg ? -u : u;
            }
            if (v == 0) return 0;
            if (v >= ((1 << epBits) - 1)) return 0xFFFF;
            return ((v << 15) + 0x4000) >> (epBits - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FinishUnquantize(int v, bool signed)
        {
            if (signed)
            {
                int s = v < 0 ? 0x8000 : 0;
                int m = v < 0 ? -v : v;
                return s | ((m * 31) >> 5);
            }
            return (v * 31) >> 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int InterpolateHalf(int e0, int e1, int w) =>
            ((64 - w) * e0 + w * e1 + 32) >> 6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HalfToFloat(ushort h)
        {
            int s = (h >> 15) & 1;
            int e = (h >> 10) & 0x1F;
            int m = h & 0x3FF;
            if (e == 0)
            {
                if (m == 0) return BitConverter.Int32BitsToSingle(s << 31);
                while ((m & 0x400) == 0) { m <<= 1; e--; }
                e++; m &= 0x3FF;
                return BitConverter.Int32BitsToSingle((s << 31) | ((e + 112) << 23) | (m << 13));
            }
            if (e == 31)
                return BitConverter.Int32BitsToSingle((s << 31) | 0x7F800000 | (m << 13));
            return BitConverter.Int32BitsToSingle((s << 31) | ((e + 112) << 23) | (m << 13));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort FloatToHalf(float f)
        {
            uint bits = BitConverter.SingleToUInt32Bits(f);
            uint s = (bits >> 16) & 0x8000;
            int exp = (int)((bits >> 23) & 0xFF) - 127;
            uint mantissa = bits & 0x7FFFFF;
            if (exp > 15) return (ushort)(s | 0x7C00);
            if (exp < -14) return (ushort)s;
            return (ushort)(s | (uint)((exp + 15) << 10) | (mantissa >> 13));
        }

        private static int DetermineMode(ReadOnlySpan<byte> block)
        {
            int b0 = block[0];
            if ((b0 & 2) == 0)
                return b0 & 1;
            int high3 = (b0 >> 2) & 7;
            if ((b0 & 1) == 0)
                return high3 + 2;
            return high3 <= 3 ? high3 + 10 : -1;
        }

        #endregion

        #region Block Decoding

        /// <summary>
        /// Decodes a single 16-byte BC6H block into 16 HDR <see cref="Vector4"/> pixels.
        /// </summary>
        /// <param name="block">The 16-byte compressed block.</param>
        /// <param name="pixels">The destination span for 16 decoded pixels (RGBA, alpha = 1).</param>
        /// <param name="signed">True for signed half-float (BC6H_SF16), false for unsigned (BC6H_UF16).</param>
        public static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels, bool signed)
        {
            int mode = DetermineMode(block);

            if (mode < 0 || mode > 13)
            {
                for (int i = 0; i < 16; i++) pixels[i] = new Vector4(0, 0, 0, 1);
                return;
            }

            var info = Modes[mode];

            Span<int> r = stackalloc int[4];
            Span<int> g = stackalloc int[4];
            Span<int> b = stackalloc int[4];
            int partition = ExtractEndpoints(block, mode, r, g, b);

            int epCount = info.NumSubsets * 2;
            if (info.Transformed)
            {
                r[1] = SignExtend(r[1], info.DeltaBitsR);
                g[1] = SignExtend(g[1], info.DeltaBitsG);
                b[1] = SignExtend(b[1], info.DeltaBitsB);
                if (epCount > 2)
                {
                    r[2] = SignExtend(r[2], info.DeltaBitsR);
                    g[2] = SignExtend(g[2], info.DeltaBitsG);
                    b[2] = SignExtend(b[2], info.DeltaBitsB);
                    r[3] = SignExtend(r[3], info.DeltaBitsR);
                    g[3] = SignExtend(g[3], info.DeltaBitsG);
                    b[3] = SignExtend(b[3], info.DeltaBitsB);
                }
                int mask = (1 << info.EndpointBits) - 1;
                for (int i = 1; i < epCount; i++)
                {
                    r[i] = (r[i] + r[0]) & mask;
                    g[i] = (g[i] + g[0]) & mask;
                    b[i] = (b[i] + b[0]) & mask;
                }
                if (signed)
                    for (int i = 0; i < epCount; i++)
                    {
                        r[i] = SignExtend(r[i], info.EndpointBits);
                        g[i] = SignExtend(g[i], info.EndpointBits);
                        b[i] = SignExtend(b[i], info.EndpointBits);
                    }
            }
            else if (signed)
            {
                for (int i = 0; i < epCount; i++)
                {
                    r[i] = SignExtend(r[i], info.EndpointBits);
                    g[i] = SignExtend(g[i], info.EndpointBits);
                    b[i] = SignExtend(b[i], info.EndpointBits);
                }
            }

            for (int i = 0; i < epCount; i++)
            {
                r[i] = Unquantize(r[i], info.EndpointBits, signed);
                g[i] = Unquantize(g[i], info.EndpointBits, signed);
                b[i] = Unquantize(b[i], info.EndpointBits, signed);
            }

            var reader = new BitReader(block);
            int totalIdxBits = info.IndexBits * 16 - (info.NumSubsets == 1 ? 1 : 2);
            int idxStart = 128 - totalIdxBits;
            var wt = info.IndexBits == 3 ? BC6HPartitionTable.Weights3 : BC6HPartitionTable.Weights4;
            Span<float> rSubset0 = stackalloc float[16];
            Span<float> gSubset0 = stackalloc float[16];
            Span<float> bSubset0 = stackalloc float[16];
            Span<float> rSubset1 = stackalloc float[16];
            Span<float> gSubset1 = stackalloc float[16];
            Span<float> bSubset1 = stackalloc float[16];

            for (int j = 0; j < wt.Length; j++)
            {
                int w = wt[j];
                rSubset0[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(r[0], r[1], w), signed));
                gSubset0[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(g[0], g[1], w), signed));
                bSubset0[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(b[0], b[1], w), signed));

                if (info.NumSubsets > 1)
                {
                    rSubset1[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(r[2], r[3], w), signed));
                    gSubset1[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(g[2], g[3], w), signed));
                    bSubset1[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(b[2], b[3], w), signed));
                }
            }

            Span<float> outR = stackalloc float[16];
            Span<float> outG = stackalloc float[16];
            Span<float> outB = stackalloc float[16];

            int bp = idxStart;
            for (int i = 0; i < 16; i++)
            {
                bool anchor = info.NumSubsets == 1
                    ? i == 0
                    : i == 0 || i == BC6HPartitionTable.AnchorTable[partition];
                int nbits = anchor ? info.IndexBits - 1 : info.IndexBits;
                int idx = reader.Bits(bp, nbits);
                bp += nbits;

                int subset = info.NumSubsets == 1 ? 0 : (BC6HPartitionTable.Partitions2[partition] >> i) & 1;
                if (subset == 0)
                {
                    outR[i] = rSubset0[idx];
                    outG[i] = gSubset0[idx];
                    outB[i] = bSubset0[idx];
                }
                else
                {
                    outR[i] = rSubset1[idx];
                    outG[i] = gSubset1[idx];
                    outB[i] = bSubset1[idx];
                }
            }

            BcSimd.StoreRgbFloatWithAlphaOne(outR, outG, outB, pixels);
        }

        #endregion

        #region Per-Mode Endpoint Extraction

        private static int ExtractEndpoints(ReadOnlySpan<byte> block, int mode,
            Span<int> r, Span<int> g, Span<int> b)
        {
            var reader = new BitReader(block);
            return mode switch
            {
                 0 => Extract0(reader, r, g, b),
                 1 => Extract1(reader, r, g, b),
                 2 => Extract2(reader, r, g, b),
                 3 => Extract3(reader, r, g, b),
                 4 => Extract4(reader, r, g, b),
                 5 => Extract5(reader, r, g, b),
                 6 => Extract6(reader, r, g, b),
                 7 => Extract7(reader, r, g, b),
                 8 => Extract8(reader, r, g, b),
                 9 => Extract9(reader, r, g, b),
                10 => Extract10(reader, r, g, b),
                11 => Extract11(reader, r, g, b),
                12 => Extract12(reader, r, g, b),
                13 => Extract13(reader, r, g, b),
                 _ => 0,
            };
        }

        // Mode 0: 2-bit mode (00), 10:5:5:5, 2 subsets, transformed
        private static int Extract0(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10);
            r[1] = d.Bits(35, 5);
            r[2] = d.Bits(65, 5);
            r[3] = d.Bits(71, 5);
            g[0] = d.Bits(15, 10);
            g[1] = d.Bits(45, 5);
            g[2] = d.Bits(41, 4) | (d.Bit(2) << 4);
            g[3] = d.Bits(51, 4) | (d.Bit(40) << 4);
            b[0] = d.Bits(25, 10);
            b[1] = d.Bits(55, 5);
            b[2] = d.Bits(61, 4) | (d.Bit(3) << 4);
            b[3] = d.Bit(50) | (d.Bit(60) << 1) | (d.Bit(70) << 2) | (d.Bit(76) << 3) | (d.Bit(4) << 4);
            return d.Bits(77, 5);
        }

        // Mode 1: 2-bit mode (01), 7:6:6:6, 2 subsets, transformed
        private static int Extract1(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 7);
            r[1] = d.Bits(35, 6);
            r[2] = d.Bits(65, 6);
            r[3] = d.Bits(71, 6);
            g[0] = d.Bits(15, 7);
            g[1] = d.Bits(45, 6);
            g[2] = d.Bits(41, 4) | (d.Bit(24) << 4) | (d.Bit(2) << 5);
            g[3] = d.Bits(51, 4) | (d.Bit(3) << 4) | (d.Bit(4) << 5);
            b[0] = d.Bits(25, 7);
            b[1] = d.Bits(55, 6);
            b[2] = d.Bits(61, 4) | (d.Bit(14) << 4) | (d.Bit(22) << 5);
            b[3] = d.Bit(12) | (d.Bit(13) << 1) | (d.Bit(23) << 2) | (d.Bit(32) << 3) | (d.Bit(34) << 4) | (d.Bit(33) << 5);
            return d.Bits(77, 5);
        }

        // Mode 2: 5-bit mode (00010), 11:5:4:4, 2 subsets, transformed
        private static int Extract2(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10) | (d.Bit(40) << 10);
            r[1] = d.Bits(35, 5);
            r[2] = d.Bits(65, 5);
            r[3] = d.Bits(71, 5);
            g[0] = d.Bits(15, 10) | (d.Bit(49) << 10);
            g[1] = d.Bits(45, 4);
            g[2] = d.Bits(41, 4);
            g[3] = d.Bits(51, 4);
            b[0] = d.Bits(25, 10) | (d.Bit(59) << 10);
            b[1] = d.Bits(55, 4);
            b[2] = d.Bits(61, 4);
            b[3] = d.Bit(50) | (d.Bit(60) << 1) | (d.Bit(70) << 2) | (d.Bit(76) << 3);
            return d.Bits(77, 5);
        }

        // Mode 3: 5-bit mode (00110), 11:4:5:4, 2 subsets, transformed
        private static int Extract3(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10) | (d.Bit(39) << 10);
            r[1] = d.Bits(35, 4);
            r[2] = d.Bits(65, 4);
            r[3] = d.Bits(71, 4);
            g[0] = d.Bits(15, 10) | (d.Bit(50) << 10);
            g[1] = d.Bits(45, 5);
            g[2] = d.Bits(41, 4) | (d.Bit(75) << 4);
            g[3] = d.Bits(51, 4) | (d.Bit(40) << 4);
            b[0] = d.Bits(25, 10) | (d.Bit(59) << 10);
            b[1] = d.Bits(55, 4);
            b[2] = d.Bits(61, 4);
            b[3] = d.Bit(69) | (d.Bit(60) << 1) | (d.Bit(70) << 2) | (d.Bit(76) << 3);
            return d.Bits(77, 5);
        }

        // Mode 4: 5-bit mode (01010), 11:4:4:5, 2 subsets, transformed
        private static int Extract4(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10) | (d.Bit(39) << 10);
            r[1] = d.Bits(35, 4);
            r[2] = d.Bits(65, 4);
            r[3] = d.Bits(71, 4);
            g[0] = d.Bits(15, 10) | (d.Bit(49) << 10);
            g[1] = d.Bits(45, 4);
            g[2] = d.Bits(41, 4);
            g[3] = d.Bits(51, 4);
            b[0] = d.Bits(25, 10) | (d.Bit(60) << 10);
            b[1] = d.Bits(55, 5);
            b[2] = d.Bits(61, 4) | (d.Bit(40) << 4);
            b[3] = d.Bit(50) | (d.Bit(69) << 1) | (d.Bit(70) << 2) | (d.Bit(76) << 3) | (d.Bit(75) << 4);
            return d.Bits(77, 5);
        }

        // Mode 5: 5-bit mode (01110), 9:5:5:5, 2 subsets, transformed
        private static int Extract5(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 9);
            r[1] = d.Bits(35, 5);
            r[2] = d.Bits(65, 5);
            r[3] = d.Bits(71, 5);
            g[0] = d.Bits(15, 9);
            g[1] = d.Bits(45, 5);
            g[2] = d.Bits(41, 4) | (d.Bit(24) << 4);
            g[3] = d.Bits(51, 4) | (d.Bit(40) << 4);
            b[0] = d.Bits(25, 9);
            b[1] = d.Bits(55, 5);
            b[2] = d.Bits(61, 4) | (d.Bit(14) << 4);
            b[3] = d.Bit(50) | (d.Bit(60) << 1) | (d.Bit(70) << 2) | (d.Bit(76) << 3) | (d.Bit(34) << 4);
            return d.Bits(77, 5);
        }

        // Mode 6: 5-bit mode (10010), 8:6:5:5, 2 subsets, transformed
        private static int Extract6(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 8);
            r[1] = d.Bits(35, 6);
            r[2] = d.Bits(65, 6);
            r[3] = d.Bits(71, 6);
            g[0] = d.Bits(15, 8);
            g[1] = d.Bits(45, 5);
            g[2] = d.Bits(41, 4) | (d.Bit(24) << 4);
            g[3] = d.Bits(51, 4) | (d.Bit(13) << 4);
            b[0] = d.Bits(25, 8);
            b[1] = d.Bits(55, 5);
            b[2] = d.Bits(61, 4) | (d.Bit(14) << 4);
            b[3] = d.Bit(50) | (d.Bit(60) << 1) | (d.Bit(23) << 2) | (d.Bit(33) << 3) | (d.Bit(34) << 4);
            return d.Bits(77, 5);
        }

        // Mode 7: 5-bit mode (10110), 8:5:6:5, 2 subsets, transformed
        private static int Extract7(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 8);
            r[1] = d.Bits(35, 5);
            r[2] = d.Bits(65, 5);
            r[3] = d.Bits(71, 5);
            g[0] = d.Bits(15, 8);
            g[1] = d.Bits(45, 6);
            g[2] = d.Bits(41, 4) | (d.Bit(24) << 4) | (d.Bit(23) << 5);
            g[3] = d.Bits(51, 4) | (d.Bit(40) << 4) | (d.Bit(33) << 5);
            b[0] = d.Bits(25, 8);
            b[1] = d.Bits(55, 5);
            b[2] = d.Bits(61, 4) | (d.Bit(14) << 4);
            b[3] = d.Bit(13) | (d.Bit(60) << 1) | (d.Bit(70) << 2) | (d.Bit(76) << 3) | (d.Bit(34) << 4);
            return d.Bits(77, 5);
        }

        // Mode 8: 5-bit mode (11010), 8:5:5:6, 2 subsets, transformed
        private static int Extract8(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 8);
            r[1] = d.Bits(35, 5);
            r[2] = d.Bits(65, 5);
            r[3] = d.Bits(71, 5);
            g[0] = d.Bits(15, 8);
            g[1] = d.Bits(45, 5);
            g[2] = d.Bits(41, 4) | (d.Bit(24) << 4);
            g[3] = d.Bits(51, 4) | (d.Bit(40) << 4);
            b[0] = d.Bits(25, 8);
            b[1] = d.Bits(55, 6);
            b[2] = d.Bits(61, 4) | (d.Bit(14) << 4) | (d.Bit(23) << 5);
            b[3] = d.Bit(50) | (d.Bit(13) << 1) | (d.Bit(70) << 2) | (d.Bit(76) << 3) | (d.Bit(34) << 4) | (d.Bit(33) << 5);
            return d.Bits(77, 5);
        }

        // Mode 9: 5-bit mode (11110), 6:6:6:6, 2 subsets, NON-transformed
        private static int Extract9(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 6);
            r[1] = d.Bits(35, 6);
            r[2] = d.Bits(65, 6);
            r[3] = d.Bits(71, 6);
            g[0] = d.Bits(15, 6);
            g[1] = d.Bits(45, 6);
            g[2] = d.Bits(41, 4) | (d.Bit(24) << 4) | (d.Bit(21) << 5);
            g[3] = d.Bits(51, 4) | (d.Bit(11) << 4) | (d.Bit(31) << 5);
            b[0] = d.Bits(25, 6);
            b[1] = d.Bits(55, 6);
            b[2] = d.Bits(61, 4) | (d.Bit(14) << 4) | (d.Bit(22) << 5);
            b[3] = d.Bit(12) | (d.Bit(13) << 1) | (d.Bit(23) << 2) | (d.Bit(32) << 3) | (d.Bit(34) << 4) | (d.Bit(33) << 5);
            return d.Bits(77, 5);
        }

        // Mode 10: 5-bit mode (00011), 10:10:10:10, 1 subset, NON-transformed
        private static int Extract10(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10);
            r[1] = d.Bits(35, 10);
            g[0] = d.Bits(15, 10);
            g[1] = d.Bits(45, 10);
            b[0] = d.Bits(25, 10);
            b[1] = d.Bits(55, 10);
            return 0;
        }

        // Mode 11: 5-bit mode (00111), 11:9:9:9, 1 subset, transformed
        private static int Extract11(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10) | (d.Bit(44) << 10);
            r[1] = d.Bits(35, 9);
            g[0] = d.Bits(15, 10) | (d.Bit(54) << 10);
            g[1] = d.Bits(45, 9);
            b[0] = d.Bits(25, 10) | (d.Bit(64) << 10);
            b[1] = d.Bits(55, 9);
            return 0;
        }

        // Mode 12: 5-bit mode (01011), 12:8:8:8, 1 subset, transformed
        private static int Extract12(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10) | (d.Bit(44) << 10) | (d.Bit(43) << 11);
            r[1] = d.Bits(35, 8);
            g[0] = d.Bits(15, 10) | (d.Bit(54) << 10) | (d.Bit(53) << 11);
            g[1] = d.Bits(45, 8);
            b[0] = d.Bits(25, 10) | (d.Bit(64) << 10) | (d.Bit(63) << 11);
            b[1] = d.Bits(55, 8);
            return 0;
        }

        // Mode 13: 5-bit mode (01111), 16:4:4:4, 1 subset, transformed
        private static int Extract13(BitReader d, Span<int> r, Span<int> g, Span<int> b)
        {
            r[0] = d.Bits(5, 10) | (d.Bit(44) << 10) | (d.Bit(43) << 11) | (d.Bit(42) << 12) | (d.Bit(41) << 13) | (d.Bit(40) << 14) | (d.Bit(39) << 15);
            r[1] = d.Bits(35, 4);
            g[0] = d.Bits(15, 10) | (d.Bit(54) << 10) | (d.Bit(53) << 11) | (d.Bit(52) << 12) | (d.Bit(51) << 13) | (d.Bit(50) << 14) | (d.Bit(49) << 15);
            g[1] = d.Bits(45, 4);
            b[0] = d.Bits(25, 10) | (d.Bit(64) << 10) | (d.Bit(63) << 11) | (d.Bit(62) << 12) | (d.Bit(61) << 13) | (d.Bit(60) << 14) | (d.Bit(59) << 15);
            b[1] = d.Bits(55, 4);
            return 0;
        }

        #endregion

        #region Block Encoding

        /// <summary>
        /// Encodes 16 HDR <see cref="Vector4"/> pixels into a single 16-byte BC6H block.
        /// Uses mode 11 (11:9:9:9), mode 12 (12:8:8:8), and mode 10 (10:10:10:10) as candidates,
        /// selecting the mode that produces the lowest error.
        /// </summary>
        /// <param name="pixels">The 16 source RGBA pixels (alpha is ignored).</param>
        /// <param name="block">The destination 16-byte span for the compressed block.</param>
        /// <param name="signed">True for signed half-float encoding, false for unsigned.</param>
        public static void EncodeBlock(ReadOnlySpan<Vector4> pixels, Span<byte> block, bool signed)
        {
            EncodeBlockCore(pixels, block, signed, fastMode: false);
        }

        /// <summary>
        /// Encodes 16 HDR <see cref="Vector4"/> pixels into a single 16-byte BC6H block using a
        /// reduced CPU search intended to favor throughput over quality.
        /// </summary>
        /// <param name="pixels">The 16 source RGBA pixels (alpha is ignored).</param>
        /// <param name="block">The destination 16-byte span for the compressed block.</param>
        /// <param name="signed">True for signed half-float encoding, false for unsigned.</param>
        public static void EncodeBlockFast(ReadOnlySpan<Vector4> pixels, Span<byte> block, bool signed)
        {
            EncodeBlockCore(pixels, block, signed, fastMode: true);
        }

        private static void EncodeBlockCore(ReadOnlySpan<Vector4> pixels, Span<byte> block, bool signed, bool fastMode)
        {
            // Convert float pixels to 16-bit half values.
            Span<ushort> rHalf = stackalloc ushort[16];
            Span<ushort> gHalf = stackalloc ushort[16];
            Span<ushort> bHalf = stackalloc ushort[16];
            ConvertToHalf(pixels, rHalf, gHalf, bHalf, signed);

            // Try 1-subset modes and pick the lowest error.
            Span<byte> bestBlock = stackalloc byte[16];
            float bestError = float.MaxValue;
            Span<byte> candidate = stackalloc byte[16];

            // Mode 10 is the most stable option for the current encoder and avoids
            // transformed-endpoint wraparound on unsigned HDR gradients.
            float error = TryEncodeMode10(rHalf, gHalf, bHalf, candidate, signed);
            bestError = error;
            candidate.CopyTo(bestBlock);

            if (signed && !fastMode)
            {
                // The transformed 1-subset modes are still worthwhile for signed HDR data,
                // where negative ranges benefit from the higher endpoint precision.
                candidate.Clear();
                error = TryEncodeMode11(rHalf, gHalf, bHalf, candidate, signed);
                if (error < bestError)
                {
                    bestError = error;
                    candidate.CopyTo(bestBlock);
                }

                candidate.Clear();
                error = TryEncodeMode12(rHalf, gHalf, bHalf, candidate, signed);
                if (error < bestError)
                    candidate.CopyTo(bestBlock);
            }

            bestBlock.CopyTo(block);
        }

        private static void ConvertToHalf(ReadOnlySpan<Vector4> pixels,
            Span<ushort> rHalf, Span<ushort> gHalf, Span<ushort> bHalf, bool signed)
        {
            for (int i = 0; i < 16; i++)
            {
                float rv = pixels[i].X;
                float gv = pixels[i].Y;
                float bv = pixels[i].Z;

                if (!signed)
                {
                    rv = MathF.Max(0, rv);
                    gv = MathF.Max(0, gv);
                    bv = MathF.Max(0, bv);
                }

                rHalf[i] = FloatToHalf(rv);
                gHalf[i] = FloatToHalf(gv);
                bHalf[i] = FloatToHalf(bv);
            }
        }

        /// <summary>
        /// Quantizes a value to a given number of bits.
        /// For unsigned, input is a 16-bit value (0..0xFFFF for unsigned BC6H after unquantize).
        /// For signed, input is a signed 16-bit quantity.
        /// We need to reverse the Unquantize process.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Quantize(int value, int epBits, bool signed)
        {
            if (epBits >= 15) return value;
            if (signed)
            {
                bool neg = value < 0;
                int m = neg ? -value : value;
                int maxVal = (1 << (epBits - 1)) - 1;
                int q = (m * maxVal + 0x3FFF) >> 15;
                q = Math.Min(q, maxVal);
                return neg ? -q : q;
            }
            int umax = (1 << epBits) - 1;
            int result = (value * umax + 0x7FFF) >> 16;
            return Math.Clamp(result, 0, umax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HalfToQuantized(ushort h, int epBits, bool signed)
        {
            return Quantize(ExpandToInterpolationDomain(h, signed), epBits, signed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ExpandToInterpolationDomain(ushort halfValue, bool signed)
        {
            if (signed)
            {
                int magnitude = halfValue & 0x7FFF;
                int expandedMagnitude = (magnitude * 32 + 15) / 31;
                return (halfValue & 0x8000) != 0
                    ? -expandedMagnitude
                    : expandedMagnitude;
            }

            return (halfValue * 64 + 15) / 31;
        }

        private static void FindMinMaxEndpoints(ReadOnlySpan<ushort> channel, bool signed, out int minVal, out int maxVal)
        {
            int lo = int.MaxValue;
            int hi = int.MinValue;

            for (int i = 0; i < channel.Length; i++)
            {
                int value = ExpandToInterpolationDomain(channel[i], signed);
                if (value < lo) lo = value;
                if (value > hi) hi = value;
            }

            minVal = lo;
            maxVal = hi;
        }

        private static float MeasureBlockError(ReadOnlySpan<ushort> rHalf, ReadOnlySpan<ushort> gHalf, ReadOnlySpan<ushort> bHalf, int rE0, int rE1, int gE0, int gE1, int bE0, int bE1, int epBits, bool transformed, bool signed, ReadOnlySpan<byte> weights, Span<int> bestIndices)
        {
            // Unquantize endpoints to compare against pixel half values.
            int rU0 = Unquantize(rE0, epBits, signed);
            int rU1 = Unquantize(rE1, epBits, signed);
            int gU0 = Unquantize(gE0, epBits, signed);
            int gU1 = Unquantize(gE1, epBits, signed);
            int bU0 = Unquantize(bE0, epBits, signed);
            int bU1 = Unquantize(bE1, epBits, signed);

            Span<float> rOriginal = stackalloc float[16];
            Span<float> gOriginal = stackalloc float[16];
            Span<float> bOriginal = stackalloc float[16];

            for (int i = 0; i < 16; i++)
            {
                rOriginal[i] = HalfToFloat(rHalf[i]);
                gOriginal[i] = HalfToFloat(gHalf[i]);
                bOriginal[i] = HalfToFloat(bHalf[i]);
            }

            Span<float> rCandidates = stackalloc float[16];
            Span<float> gCandidates = stackalloc float[16];
            Span<float> bCandidates = stackalloc float[16];

            for (int j = 0; j < weights.Length; j++)
            {
                int w = weights[j];
                rCandidates[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(rU0, rU1, w), signed));
                gCandidates[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(gU0, gU1, w), signed));
                bCandidates[j] = HalfToFloat((ushort)FinishUnquantize(InterpolateHalf(bU0, bU1, w), signed));
            }

            return BcSimd.FindBestIndices3Channel(rOriginal, gOriginal, bOriginal, rCandidates[..weights.Length], gCandidates[..weights.Length], bCandidates[..weights.Length], bestIndices);
        }

        // Encodes using mode 11: 5-bit mode (00111), 11:9:9:9, 1 subset, transformed, 4-bit indices.
        // Layout: mode[4:0]=0..4, rw[9:0]=5..14, gw[9:0]=15..24, bw[9:0]=25..34,
        //   rx[8:0]=35..43, rw[10]=44, gx[8:0]=45..53, gw[10]=54, bx[8:0]=55..63, bw[10]=64
        //   Indices: 65..127 (63 bits = 16*4 - 1)
        private static float TryEncodeMode11(ReadOnlySpan<ushort> rH, ReadOnlySpan<ushort> gH, ReadOnlySpan<ushort> bH, Span<byte> block, bool signed)
        {
            const int epBits = 11;
            const int deltaBits = 9;

            FindMinMaxEndpoints(rH, signed, out int rMin, out int rMax);
            FindMinMaxEndpoints(gH, signed, out int gMin, out int gMax);
            FindMinMaxEndpoints(bH, signed, out int bMin, out int bMax);

            int rBase = Quantize(rMin, epBits, signed);
            int rDelta = Quantize(rMax, epBits, signed);
            int gBase = Quantize(gMin, epBits, signed);
            int gDelta = Quantize(gMax, epBits, signed);
            int bBase = Quantize(bMin, epBits, signed);
            int bDelta = Quantize(bMax, epBits, signed);

            // Compute delta and clamp to deltaBits range.
            int mask = (1 << epBits) - 1;
            int deltaMax = (1 << (deltaBits - 1)) - 1;
            int deltaMin = -(1 << (deltaBits - 1));

            int rdx = rDelta - rBase;
            int gdx = gDelta - gBase;
            int bdx = bDelta - bBase;

            rdx = Math.Clamp(rdx, deltaMin, deltaMax);
            gdx = Math.Clamp(gdx, deltaMin, deltaMax);
            bdx = Math.Clamp(bdx, deltaMin, deltaMax);

            // Reconstruct actual second endpoint.
            int rE1 = (rBase + rdx) & mask;
            int gE1 = (gBase + gdx) & mask;
            int bE1 = (bBase + bdx) & mask;

            if (signed)
            {
                rE1 = SignExtend(rE1, epBits);
                gE1 = SignExtend(gE1, epBits);
                bE1 = SignExtend(bE1, epBits);
            }

            Span<int> indices = stackalloc int[16];
            float error = MeasureBlockError(rH, gH, bH,
                rBase, rE1, gBase, gE1, bBase, bE1,
                epBits, true, signed, BC6HPartitionTable.Weights4, indices);

            // Ensure anchor index (pixel 0) MSB is 0; swap endpoints if needed.
            int maxIdx = (1 << 4) - 1;
            if (indices[0] >= (1 << 3))
            {
                (rBase, rE1) = (rE1, rBase);
                (gBase, gE1) = (gE1, gBase);
                (bBase, bE1) = (bE1, bBase);
                for (int i = 0; i < 16; i++)
                    indices[i] = maxIdx - indices[i];

                rdx = rE1 - rBase;
                gdx = gE1 - gBase;
                bdx = bE1 - bBase;
                rdx = Math.Clamp(rdx, deltaMin, deltaMax);
                gdx = Math.Clamp(gdx, deltaMin, deltaMax);
                bdx = Math.Clamp(bdx, deltaMin, deltaMax);
            }
            else
            {
                rdx = Math.Clamp(rdx, deltaMin, deltaMax);
                gdx = Math.Clamp(gdx, deltaMin, deltaMax);
                bdx = Math.Clamp(bdx, deltaMin, deltaMax);
            }

            // Write the block.
            block.Clear();
            var writer = new BitWriter(block);

            // Mode 11 = bit pattern 00111.
            writer.Write(0b00111, 5);

            // rw[9:0] at 5..14
            int rBaseMask = rBase & mask;
            writer.SetBits(5, rBaseMask, 10);
            // gw[9:0] at 15..24
            int gBaseMask = gBase & mask;
            writer.SetBits(15, gBaseMask, 10);
            // bw[9:0] at 25..34
            int bBaseMask = bBase & mask;
            writer.SetBits(25, bBaseMask, 10);
            // rx[8:0] at 35..43
            writer.SetBits(35, rdx & ((1 << deltaBits) - 1), 9);
            // rw[10] at 44
            writer.SetBit(44, (rBaseMask >> 10) & 1);
            // gx[8:0] at 45..53
            writer.SetBits(45, gdx & ((1 << deltaBits) - 1), 9);
            // gw[10] at 54
            writer.SetBit(54, (gBaseMask >> 10) & 1);
            // bx[8:0] at 55..63
            writer.SetBits(55, bdx & ((1 << deltaBits) - 1), 9);
            // bw[10] at 64
            writer.SetBit(64, (bBaseMask >> 10) & 1);

            // Indices: 65..127 (pixel 0 uses 3 bits, rest use 4 bits).
            int bp = 65;
            for (int i = 0; i < 16; i++)
            {
                int nbits = i == 0 ? 3 : 4;
                writer.SetBits(bp, indices[i], nbits);
                bp += nbits;
            }

            return error;
        }

        // Mode 12: 5-bit mode (01011), 12:8:8:8, 1 subset, transformed, 4-bit indices.
        // Layout: mode[4:0], rw[9:0]=5..14, gw[9:0]=15..24, bw[9:0]=25..34,
        //   rx[7:0]=35..42, rw[11:10]=43..44, gx[7:0]=45..52, gw[11:10]=53..54, bx[7:0]=55..62, bw[11:10]=63..64
        private static float TryEncodeMode12(ReadOnlySpan<ushort> rH, ReadOnlySpan<ushort> gH, ReadOnlySpan<ushort> bH, Span<byte> block, bool signed)
        {
            const int epBits = 12;
            const int deltaBits = 8;

            FindMinMaxEndpoints(rH, signed, out int rMin, out int rMax);
            FindMinMaxEndpoints(gH, signed, out int gMin, out int gMax);
            FindMinMaxEndpoints(bH, signed, out int bMin, out int bMax);

            int rBase = Quantize(rMin, epBits, signed);
            int rEnd = Quantize(rMax, epBits, signed);
            int gBase = Quantize(gMin, epBits, signed);
            int gEnd = Quantize(gMax, epBits, signed);
            int bBase = Quantize(bMin, epBits, signed);
            int bEnd = Quantize(bMax, epBits, signed);

            int mask = (1 << epBits) - 1;
            int deltaMax = (1 << (deltaBits - 1)) - 1;
            int deltaMin = -(1 << (deltaBits - 1));

            int rdx = Math.Clamp(rEnd - rBase, deltaMin, deltaMax);
            int gdx = Math.Clamp(gEnd - gBase, deltaMin, deltaMax);
            int bdx = Math.Clamp(bEnd - bBase, deltaMin, deltaMax);

            int rE1 = (rBase + rdx) & mask;
            int gE1 = (gBase + gdx) & mask;
            int bE1 = (bBase + bdx) & mask;

            if (signed)
            {
                rE1 = SignExtend(rE1, epBits);
                gE1 = SignExtend(gE1, epBits);
                bE1 = SignExtend(bE1, epBits);
            }

            Span<int> indices = stackalloc int[16];
            float error = MeasureBlockError(rH, gH, bH,
                rBase, rE1, gBase, gE1, bBase, bE1,
                epBits, true, signed, BC6HPartitionTable.Weights4, indices);

            int maxIdx = (1 << 4) - 1;
            if (indices[0] >= (1 << 3))
            {
                (rBase, rE1) = (rE1, rBase);
                (gBase, gE1) = (gE1, gBase);
                (bBase, bE1) = (bE1, bBase);
                for (int i = 0; i < 16; i++)
                    indices[i] = maxIdx - indices[i];
                rdx = rE1 - rBase;
                gdx = gE1 - gBase;
                bdx = bE1 - bBase;
            }
            rdx = Math.Clamp(rdx, deltaMin, deltaMax);
            gdx = Math.Clamp(gdx, deltaMin, deltaMax);
            bdx = Math.Clamp(bdx, deltaMin, deltaMax);

            block.Clear();
            var writer = new BitWriter(block);

            writer.Write(0b01011, 5);

            int rBaseMask = rBase & mask;
            int gBaseMask = gBase & mask;
            int bBaseMask = bBase & mask;

            writer.SetBits(5, rBaseMask, 10);
            writer.SetBits(15, gBaseMask, 10);
            writer.SetBits(25, bBaseMask, 10);
            writer.SetBits(35, rdx & ((1 << deltaBits) - 1), 8);
            writer.SetBit(43, (rBaseMask >> 11) & 1);
            writer.SetBit(44, (rBaseMask >> 10) & 1);
            writer.SetBits(45, gdx & ((1 << deltaBits) - 1), 8);
            writer.SetBit(53, (gBaseMask >> 11) & 1);
            writer.SetBit(54, (gBaseMask >> 10) & 1);
            writer.SetBits(55, bdx & ((1 << deltaBits) - 1), 8);
            writer.SetBit(63, (bBaseMask >> 11) & 1);
            writer.SetBit(64, (bBaseMask >> 10) & 1);

            int bp = 65;
            for (int i = 0; i < 16; i++)
            {
                int nbits = i == 0 ? 3 : 4;
                writer.SetBits(bp, indices[i], nbits);
                bp += nbits;
            }

            return error;
        }

        // Mode 10: 5-bit mode (00011), 10:10:10:10, 1 subset, non-transformed, 4-bit indices.
        // Layout: mode[4:0], rw[9:0]=5..14, gw[9:0]=15..24, bw[9:0]=25..34,
        //   rx[9:0]=35..44, gx[9:0]=45..54, bx[9:0]=55..64
        private static float TryEncodeMode10(ReadOnlySpan<ushort> rH, ReadOnlySpan<ushort> gH, ReadOnlySpan<ushort> bH, Span<byte> block, bool signed)
        {
            const int epBits = 10;

            FindMinMaxEndpoints(rH, signed, out int rMin, out int rMax);
            FindMinMaxEndpoints(gH, signed, out int gMin, out int gMax);
            FindMinMaxEndpoints(bH, signed, out int bMin, out int bMax);

            int rE0 = Quantize(rMin, epBits, signed);
            int rE1 = Quantize(rMax, epBits, signed);
            int gE0 = Quantize(gMin, epBits, signed);
            int gE1 = Quantize(gMax, epBits, signed);
            int bE0 = Quantize(bMin, epBits, signed);
            int bE1 = Quantize(bMax, epBits, signed);

            Span<int> indices = stackalloc int[16];
            float error = MeasureBlockError(rH, gH, bH,
                rE0, rE1, gE0, gE1, bE0, bE1,
                epBits, false, signed, BC6HPartitionTable.Weights4, indices);

            int maxIdx = (1 << 4) - 1;
            if (indices[0] >= (1 << 3))
            {
                (rE0, rE1) = (rE1, rE0);
                (gE0, gE1) = (gE1, gE0);
                (bE0, bE1) = (bE1, bE0);
                for (int i = 0; i < 16; i++)
                    indices[i] = maxIdx - indices[i];
            }

            block.Clear();
            var writer = new BitWriter(block);

            writer.Write(0b00011, 5);

            int mask = (1 << epBits) - 1;
            writer.SetBits(5, rE0 & mask, 10);
            writer.SetBits(15, gE0 & mask, 10);
            writer.SetBits(25, bE0 & mask, 10);
            writer.SetBits(35, rE1 & mask, 10);
            writer.SetBits(45, gE1 & mask, 10);
            writer.SetBits(55, bE1 & mask, 10);

            int bp = 65;
            for (int i = 0; i < 16; i++)
            {
                int nbits = i == 0 ? 3 : 4;
                writer.SetBits(bp, indices[i], nbits);
                bp += nbits;
            }

            return error;
        }

        #endregion
    }
}
