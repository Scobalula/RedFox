using System.Numerics;
using System.Runtime.CompilerServices;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Codec for BC7 (BPTC) block-compressed format.
    /// Each 16-byte (128-bit) block encodes a 4×4 pixel block using one of 8 modes
    /// with variable color/alpha precision, 1–3 subsets, optional rotation, and dual index sets.
    /// Supports both decoding and encoding.
    /// </summary>
    public sealed class BC7Codec : IPixelCodec
    {
        private const int BytesPerBlock = 16;

        private static readonly BC7ModeDescriptor[] Modes =
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

        /// <summary>
        /// Initializes a new instance of the <see cref="BC7Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format (BC7Typeless, BC7Unorm, or BC7UnormSrgb).</param>
        public BC7Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.BC7Typeless => format,
                ImageFormat.BC7Unorm => format,
                ImageFormat.BC7UnormSrgb => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "BC7Codec supports only BC7Typeless, BC7Unorm, and BC7UnormSrgb."),
            };
        }

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 0;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            BlockProcessor.DecodeBlocks(source, destination, width, height, BytesPerBlock, DecodeBlock);
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            BlockProcessor.EncodeBlocks(source, destination, width, height, BytesPerBlock, EncodeBlock);
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
            DecodeBlock(source.Slice(blockOffset, BytesPerBlock), blockPixels);
            return blockPixels[(y % 4) * 4 + (x % 4)];
        }

        /// <inheritdoc/>
        public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height)
        {
            BlockProcessor.DecodeRows(source, destination, startRow, rowCount, width, height, BytesPerBlock, DecodeBlock);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

        /// <inheritdoc/>
        public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            BlockProcessor.DecodeBlocksTo(source, targetCodec, destination, width, height, BytesPerBlock, DecodeBlock);
        }

        #region Decode Helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Unquantize(int value, int precision)
        {
            if (precision >= 8) return value;
            return (value << (8 - precision)) | (value >> (2 * precision - 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Interpolate(int e0, int e1, int weight) =>
            ((64 - weight) * e0 + weight * e1 + 32) >> 6;

        #endregion

        #region Block Decoding

        /// <summary>
        /// Decodes a single 16-byte BC7 block into 16 <see cref="Vector4"/> RGBA pixels.
        /// </summary>
        /// <param name="block">The 16-byte compressed block.</param>
        /// <param name="pixels">The destination span for 16 decoded RGBA pixels.</param>
        public static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
        {
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

            Span<int> r = stackalloc int[6];
            Span<int> g = stackalloc int[6];
            Span<int> b = stackalloc int[6];
            Span<int> a = stackalloc int[6];

            for (int i = 0; i < numEndpoints; i++) r[i] = (int)reader.Read(info.ColorBits);
            for (int i = 0; i < numEndpoints; i++) g[i] = (int)reader.Read(info.ColorBits);
            for (int i = 0; i < numEndpoints; i++) b[i] = (int)reader.Read(info.ColorBits);

            if (info.AlphaBits > 0)
                for (int i = 0; i < numEndpoints; i++) a[i] = (int)reader.Read(info.AlphaBits);

            if (info.EndpointPBits != 0)
            {
                for (int i = 0; i < numEndpoints; i++)
                {
                    int pbit = (int)reader.Read(1);
                    r[i] = (r[i] << 1) | pbit;
                    g[i] = (g[i] << 1) | pbit;
                    b[i] = (b[i] << 1) | pbit;
                    if (info.AlphaBits > 0) a[i] = (a[i] << 1) | pbit;
                }
            }
            else if (info.SharedPBits != 0)
            {
                for (int s = 0; s < info.NumSubsets; s++)
                {
                    int pbit = (int)reader.Read(1);
                    int i0 = s * 2, i1 = i0 + 1;
                    r[i0] = (r[i0] << 1) | pbit; g[i0] = (g[i0] << 1) | pbit; b[i0] = (b[i0] << 1) | pbit;
                    r[i1] = (r[i1] << 1) | pbit; g[i1] = (g[i1] << 1) | pbit; b[i1] = (b[i1] << 1) | pbit;
                }
            }

            int colorPrec = info.ColorBits + ((info.EndpointPBits | info.SharedPBits) != 0 ? 1 : 0);
            int alphaPrec = info.AlphaBits > 0 ? info.AlphaBits + (info.EndpointPBits != 0 ? 1 : 0) : 0;

            for (int i = 0; i < numEndpoints; i++)
            {
                r[i] = Unquantize(r[i], colorPrec);
                g[i] = Unquantize(g[i], colorPrec);
                b[i] = Unquantize(b[i], colorPrec);
                a[i] = alphaPrec > 0 ? Unquantize(a[i], alphaPrec) : 255;
            }

            Span<int> primaryIdx = stackalloc int[16];
            var primaryWeights = BC7PartitionTable.GetWeights(info.IndexBits);
            for (int i = 0; i < 16; i++)
            {
                bool anchor = BC7PartitionTable.IsAnchorIndex(info.NumSubsets, partition, i);
                primaryIdx[i] = (int)reader.Read(anchor ? info.IndexBits - 1 : info.IndexBits);
            }

            Span<int> secondaryIdx = stackalloc int[16];
            ReadOnlySpan<byte> secondaryWeights = default;
            bool hasDualIndices = info.SecondaryIndexBits > 0;

            if (hasDualIndices)
            {
                secondaryWeights = BC7PartitionTable.GetWeights(info.SecondaryIndexBits);
                for (int i = 0; i < 16; i++)
                    secondaryIdx[i] = (int)reader.Read(i == 0 ? info.SecondaryIndexBits - 1 : info.SecondaryIndexBits);
            }

            for (int i = 0; i < 16; i++)
            {
                int subset = BC7PartitionTable.GetSubset(info.NumSubsets, partition, i);
                int e0 = subset * 2, e1 = e0 + 1;

                int pr, pg, pb, pa;

                if (hasDualIndices)
                {
                    int cIdx, aIdx;
                    ReadOnlySpan<byte> cw, aw;

                    if (indexSelection != 0)
                    { cIdx = secondaryIdx[i]; aIdx = primaryIdx[i]; cw = secondaryWeights; aw = primaryWeights; }
                    else
                    { cIdx = primaryIdx[i]; aIdx = secondaryIdx[i]; cw = primaryWeights; aw = secondaryWeights; }

                    int cwt = cw[cIdx], awt = aw[aIdx];
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

        #endregion

        #region Block Encoding

        /// <summary>
        /// Encodes 16 <see cref="Vector4"/> RGBA pixels into a single 16-byte BC7 block.
        /// Evaluates mode 6 (single subset, 7+7 color+alpha, 4-bit indices, endpoint P-bits)
        /// and mode 1 (2 subsets, 6-bit color, shared P-bits, 3-bit indices), selecting the one
        /// with the lowest error.
        /// </summary>
        /// <param name="pixels">The 16 source RGBA pixels.</param>
        /// <param name="block">The destination 16-byte span for the compressed block.</param>
        public static void EncodeBlock(ReadOnlySpan<Vector4> pixels, Span<byte> block)
        {
            // Convert to 8-bit RGBA.
            Span<int> rPix = stackalloc int[16];
            Span<int> gPix = stackalloc int[16];
            Span<int> bPix = stackalloc int[16];
            Span<int> aPix = stackalloc int[16];

            for (int i = 0; i < 16; i++)
            {
                rPix[i] = Math.Clamp((int)(pixels[i].X * 255f + 0.5f), 0, 255);
                gPix[i] = Math.Clamp((int)(pixels[i].Y * 255f + 0.5f), 0, 255);
                bPix[i] = Math.Clamp((int)(pixels[i].Z * 255f + 0.5f), 0, 255);
                aPix[i] = Math.Clamp((int)(pixels[i].W * 255f + 0.5f), 0, 255);
            }

            Span<byte> bestBlock = stackalloc byte[16];
            float bestError = float.MaxValue;

            // Try Mode 6: 1 subset, 7-bit color + 7-bit alpha + endpoint P-bits, 4-bit indices.
            Span<byte> candidate = stackalloc byte[16];
            float error = TryEncodeMode6(rPix, gPix, bPix, aPix, candidate);
            if (error < bestError)
            {
                bestError = error;
                candidate.CopyTo(bestBlock);
            }

            // Try Mode 1: 2 subsets, 6-bit color, shared P-bits, 3-bit indices (best for opaque).
            // Test a few partition candidates.
            bool allOpaque = true;
            for (int i = 0; i < 16; i++)
            {
                if (aPix[i] < 255) { allOpaque = false; break; }
            }

            if (allOpaque)
            {
                // Try a subset of partitions for mode 1.
                for (int p = 0; p < 64; p++)
                {
                    candidate.Clear();
                    error = TryEncodeMode1(rPix, gPix, bPix, p, candidate);
                    if (error < bestError)
                    {
                        bestError = error;
                        candidate.CopyTo(bestBlock);
                    }
                }
            }

            bestBlock.CopyTo(block);
        }

        private static float TryEncodeMode6(
            ReadOnlySpan<int> rPix, ReadOnlySpan<int> gPix, ReadOnlySpan<int> bPix, ReadOnlySpan<int> aPix,
            Span<byte> block)
        {
            // Mode 6: 1 subset, 7-bit color, 7-bit alpha, endpoint P-bits, 4-bit indices.
            // Effective precision = 8 bits (7 + 1 P-bit).
            // Total bits: 7(mode) + 0(partition) + 0(rotation) + 0(idxSel) +
            //   7*2*4(color) = 56, 7*2(alpha) = 14, 2 P-bits, 16*4-1 = 63 indices.
            //   7 + 56 + 14 + 2 + 63 = 142? No wait, mode 6 marker is 7 bits (0000001).
            //   Actually the mode marker is (mode+1) bits = 7 bits for mode 6.
            //   End: endpoints 7*4*2 = 56(color) + 7*2 = 14(alpha) = 70 ep bits.
            //   + 2 P-bits + 63 index bits + 7 mode bits = 142. But block is 128 bits.
            //   Hmm... let me re-check. The mode 6 spec says:
            //   Subsets=1, PartBits=0, RotBits=0, IdxSelBits=0,
            //   ColorBits=7, AlphaBits=7, EndpointPBits=1 (per endpoint, so 2 total), SharedPBits=0,
            //   IndexBits=4, SecondaryIndexBits=0.
            //   Header = 7 bits (mode marker 0000001).
            //   Endpoints = 2 endpoints × (3 color × 7 + 1 alpha × 7) = 2 × 28 = 56 bits.
            //   P-bits = 2.
            //   Indices = 16 × 4 - 1 = 63.
            //   Total = 7 + 56 + 2 + 63 = 128 ✓

            FindMinMax(rPix, 16, out int rMin, out int rMax);
            FindMinMax(gPix, 16, out int gMin, out int gMax);
            FindMinMax(bPix, 16, out int bMin, out int bMax);
            FindMinMax(aPix, 16, out int aMin, out int aMax);

            // Quantize to 7 bits (P-bit gives the LSB).
            int rE0 = rMin >> 1, rE1 = rMax >> 1;
            int gE0 = gMin >> 1, gE1 = gMax >> 1;
            int bE0 = bMin >> 1, bE1 = bMax >> 1;
            int aE0 = aMin >> 1, aE1 = aMax >> 1;
            int pbit0 = rMin & 1, pbit1 = rMax & 1;

            // Reconstruct 8-bit endpoints.
            int r0 = Unquantize((rE0 << 1) | pbit0, 8);
            int r1 = Unquantize((rE1 << 1) | pbit1, 8);
            int g0 = Unquantize((gE0 << 1) | pbit0, 8);
            int g1 = Unquantize((gE1 << 1) | pbit1, 8);
            int b0 = Unquantize((bE0 << 1) | pbit0, 8);
            int b1 = Unquantize((bE1 << 1) | pbit1, 8);
            int a0 = Unquantize((aE0 << 1) | pbit0, 8);
            int a1 = Unquantize((aE1 << 1) | pbit1, 8);

            var wt = BC7PartitionTable.Weights4;
            Span<int> indices = stackalloc int[16];
            float totalError = FindBestIndices4Channel(rPix, gPix, bPix, aPix, r0, r1, g0, g1, b0, b1, a0, a1, wt, indices);

            // Ensure anchor (pixel 0) MSB is 0.
            int maxIdx = 15;
            if (indices[0] >= 8)
            {
                (rE0, rE1) = (rE1, rE0);
                (gE0, gE1) = (gE1, gE0);
                (bE0, bE1) = (bE1, bE0);
                (aE0, aE1) = (aE1, aE0);
                (pbit0, pbit1) = (pbit1, pbit0);
                for (int i = 0; i < 16; i++)
                    indices[i] = maxIdx - indices[i];
            }

            block.Clear();
            var writer = new BitWriter(block);

            // Mode 6: marker is 0000001 (7 bits).
            writer.Write(1u << 6, 7);

            // No partition, rotation, or index selection bits.
            // Endpoints: R0, R1, G0, G1, B0, B1, A0, A1 (7 bits each).
            writer.Write((uint)rE0, 7);
            writer.Write((uint)rE1, 7);
            writer.Write((uint)gE0, 7);
            writer.Write((uint)gE1, 7);
            writer.Write((uint)bE0, 7);
            writer.Write((uint)bE1, 7);
            writer.Write((uint)aE0, 7);
            writer.Write((uint)aE1, 7);

            // P-bits (1 per endpoint).
            writer.Write((uint)pbit0, 1);
            writer.Write((uint)pbit1, 1);

            // Indices: pixel 0 = 3 bits, rest = 4 bits.
            writer.Write((uint)indices[0], 3);
            for (int i = 1; i < 16; i++)
                writer.Write((uint)indices[i], 4);

            return totalError;
        }

        private static float TryEncodeMode1(
            ReadOnlySpan<int> rPix, ReadOnlySpan<int> gPix, ReadOnlySpan<int> bPix,
            int partition, Span<byte> block)
        {
            // Mode 1: 2 subsets, 6-bit color, no alpha, shared P-bits, 3-bit indices.
            // Header: 2 bits (mode marker 00000010). Partition: 6 bits.
            // Endpoints: 4 endpoints × 3 channels × 6 = 72 bits. 2 shared P-bits.
            // Indices: 16 × 3 - 2 = 46. Total = 2 + 6 + 72 + 2 + 46 = 128 ✓

            // Classify pixels into subsets.
            Span<int> subsetIdx = stackalloc int[16];
            for (int i = 0; i < 16; i++)
                subsetIdx[i] = BC7PartitionTable.GetSubset(2, partition, i);

            // Find min/max per subset.
            Span<int> rMinS = stackalloc int[2];
            Span<int> rMaxS = stackalloc int[2];
            Span<int> gMinS = stackalloc int[2];
            Span<int> gMaxS = stackalloc int[2];
            Span<int> bMinS = stackalloc int[2];
            Span<int> bMaxS = stackalloc int[2];

            rMinS[0] = rMinS[1] = gMinS[0] = gMinS[1] = bMinS[0] = bMinS[1] = 255;
            rMaxS[0] = rMaxS[1] = gMaxS[0] = gMaxS[1] = bMaxS[0] = bMaxS[1] = 0;

            for (int i = 0; i < 16; i++)
            {
                int s = subsetIdx[i];
                if (rPix[i] < rMinS[s]) rMinS[s] = rPix[i];
                if (rPix[i] > rMaxS[s]) rMaxS[s] = rPix[i];
                if (gPix[i] < gMinS[s]) gMinS[s] = gPix[i];
                if (gPix[i] > gMaxS[s]) gMaxS[s] = gPix[i];
                if (bPix[i] < bMinS[s]) bMinS[s] = bPix[i];
                if (bPix[i] > bMaxS[s]) bMaxS[s] = bPix[i];
            }

            // Quantize to 6 bits + shared P-bit (effective 7 bits).
            // Shared P-bits apply to both endpoints in a subset, so evaluate both choices.
            Span<int> rE = stackalloc int[4];
            Span<int> gE = stackalloc int[4];
            Span<int> bE = stackalloc int[4];
            Span<int> pbits = stackalloc int[2];
            Span<int> candidateRE = stackalloc int[4];
            Span<int> candidateGE = stackalloc int[4];
            Span<int> candidateBE = stackalloc int[4];
            Span<int> candidatePbits = stackalloc int[2];
            Span<int> candidateIndices = stackalloc int[16];
            Span<int> rU = stackalloc int[4];
            Span<int> gU = stackalloc int[4];
            Span<int> bU = stackalloc int[4];
            var wt = BC7PartitionTable.Weights3;
            float totalError = float.MaxValue;

            for (int p0 = 0; p0 <= 1; p0++)
            {
                for (int p1 = 0; p1 <= 1; p1++)
                {
                    candidatePbits[0] = p0;
                    candidatePbits[1] = p1;

                    for (int s = 0; s < 2; s++)
                    {
                        int pbit = candidatePbits[s];
                        int baseIndex = s * 2;

                        candidateRE[baseIndex] = QuantizeSharedPBitEndpoint(rMinS[s], pbit);
                        candidateRE[baseIndex + 1] = QuantizeSharedPBitEndpoint(rMaxS[s], pbit);
                        candidateGE[baseIndex] = QuantizeSharedPBitEndpoint(gMinS[s], pbit);
                        candidateGE[baseIndex + 1] = QuantizeSharedPBitEndpoint(gMaxS[s], pbit);
                        candidateBE[baseIndex] = QuantizeSharedPBitEndpoint(bMinS[s], pbit);
                        candidateBE[baseIndex + 1] = QuantizeSharedPBitEndpoint(bMaxS[s], pbit);

                        for (int ep = 0; ep < 2; ep++)
                        {
                            int index = baseIndex + ep;
                            rU[index] = Unquantize((candidateRE[index] << 1) | pbit, 7);
                            gU[index] = Unquantize((candidateGE[index] << 1) | pbit, 7);
                            bU[index] = Unquantize((candidateBE[index] << 1) | pbit, 7);
                        }
                    }

                    float candidateError = 0f;

                    for (int i = 0; i < 16; i++)
                    {
                        int s = subsetIdx[i];
                        int e0 = s * 2;
                        int e1 = e0 + 1;

                        float bestErr = float.MaxValue;
                        int bestIdx = 0;

                        for (int j = 0; j < wt.Length; j++)
                        {
                            int w = wt[j];
                            int ri = Interpolate(rU[e0], rU[e1], w);
                            int gi = Interpolate(gU[e0], gU[e1], w);
                            int bi = Interpolate(bU[e0], bU[e1], w);

                            float dr = ri - rPix[i];
                            float dg = gi - gPix[i];
                            float db = bi - bPix[i];
                            float err = dr * dr + dg * dg + db * db;

                            if (err < bestErr)
                            {
                                bestErr = err;
                                bestIdx = j;
                            }
                        }

                        candidateIndices[i] = bestIdx;
                        candidateError += bestErr;
                    }

                    if (candidateError < totalError)
                    {
                        totalError = candidateError;
                        candidateRE.CopyTo(rE);
                        candidateGE.CopyTo(gE);
                        candidateBE.CopyTo(bE);
                        candidatePbits.CopyTo(pbits);
                    }
                }
            }

            Span<int> indices = stackalloc int[16];
            for (int i = 0; i < 16; i++)
            {
                int s = subsetIdx[i];
                int e0 = s * 2;
                int e1 = e0 + 1;
                int pbit = pbits[s];

                rU[e0] = Unquantize((rE[e0] << 1) | pbit, 7);
                rU[e1] = Unquantize((rE[e1] << 1) | pbit, 7);
                gU[e0] = Unquantize((gE[e0] << 1) | pbit, 7);
                gU[e1] = Unquantize((gE[e1] << 1) | pbit, 7);
                bU[e0] = Unquantize((bE[e0] << 1) | pbit, 7);
                bU[e1] = Unquantize((bE[e1] << 1) | pbit, 7);

                float bestErr = float.MaxValue;
                int bestIdx = 0;

                for (int j = 0; j < wt.Length; j++)
                {
                    int w = wt[j];
                    int ri = Interpolate(rU[e0], rU[e1], w);
                    int gi = Interpolate(gU[e0], gU[e1], w);
                    int bi = Interpolate(bU[e0], bU[e1], w);

                    float dr = ri - rPix[i];
                    float dg = gi - gPix[i];
                    float db = bi - bPix[i];
                    float err = dr * dr + dg * dg + db * db;

                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestIdx = j;
                    }
                }

                indices[i] = bestIdx;
            }

            // Fix anchor indices for both subsets.
            int anchor0 = 0;
            int anchor1 = BC7PartitionTable.AnchorTable2[partition];
            int maxIdxVal = 7;

            if (indices[anchor0] >= 4)
            {
                // Swap subset 0 endpoints.
                (rE[0], rE[1]) = (rE[1], rE[0]);
                (gE[0], gE[1]) = (gE[1], gE[0]);
                (bE[0], bE[1]) = (bE[1], bE[0]);
                for (int i = 0; i < 16; i++)
                    if (subsetIdx[i] == 0) indices[i] = maxIdxVal - indices[i];
            }
            if (indices[anchor1] >= 4)
            {
                // Swap subset 1 endpoints.
                (rE[2], rE[3]) = (rE[3], rE[2]);
                (gE[2], gE[3]) = (gE[3], gE[2]);
                (bE[2], bE[3]) = (bE[3], bE[2]);
                for (int i = 0; i < 16; i++)
                    if (subsetIdx[i] == 1) indices[i] = maxIdxVal - indices[i];
            }

            block.Clear();
            var writer = new BitWriter(block);

            // Mode 1 marker: 00000010 → bit 1 set = 0x02.
            writer.Write(0b10, 2);

            // Partition: 6 bits.
            writer.Write((uint)partition, 6);

            // Color endpoints: R0, R1, R2, R3, G0..G3, B0..B3 (6 bits each).
            for (int i = 0; i < 4; i++) writer.Write((uint)(rE[i] & 0x3F), 6);
            for (int i = 0; i < 4; i++) writer.Write((uint)(gE[i] & 0x3F), 6);
            for (int i = 0; i < 4; i++) writer.Write((uint)(bE[i] & 0x3F), 6);

            // Shared P-bits: 1 per subset.
            writer.Write((uint)pbits[0], 1);
            writer.Write((uint)pbits[1], 1);

            // Indices: anchors get (indexBits - 1) = 2 bits, others get 3 bits.
            for (int i = 0; i < 16; i++)
            {
                bool anchor = BC7PartitionTable.IsAnchorIndex(2, partition, i);
                writer.Write((uint)indices[i], anchor ? 2 : 3);
            }

            return totalError;
        }

        #endregion

        #region Encoding Helpers

        private static void FindMinMax(ReadOnlySpan<int> values, int count, out int min, out int max)
        {
            min = int.MaxValue;
            max = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                if (values[i] < min) min = values[i];
                if (values[i] > max) max = values[i];
            }
        }

        private static float FindBestIndices4Channel(
            ReadOnlySpan<int> rPix, ReadOnlySpan<int> gPix, ReadOnlySpan<int> bPix, ReadOnlySpan<int> aPix,
            int r0, int r1, int g0, int g1, int b0, int b1, int a0, int a1,
            ReadOnlySpan<byte> weights, Span<int> indices)
        {
            float totalError = 0;
            for (int i = 0; i < 16; i++)
            {
                float bestErr = float.MaxValue;
                int bestIdx = 0;
                for (int j = 0; j < weights.Length; j++)
                {
                    int w = weights[j];
                    float dr = Interpolate(r0, r1, w) - rPix[i];
                    float dg = Interpolate(g0, g1, w) - gPix[i];
                    float db = Interpolate(b0, b1, w) - bPix[i];
                    float da = Interpolate(a0, a1, w) - aPix[i];
                    float err = dr * dr + dg * dg + db * db + da * da;
                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestIdx = j;
                    }
                }
                indices[i] = bestIdx;
                totalError += bestErr;
            }
            return totalError;
        }

        private static int QuantizeSharedPBitEndpoint(int value, int pbit)
        {
            int bestValue = 0;
            int bestError = int.MaxValue;
            int estimated = Math.Clamp((value - pbit + 1) >> 1, 0, 63);

            for (int candidate = Math.Max(0, estimated - 1); candidate <= Math.Min(63, estimated + 1); candidate++)
            {
                int reconstructed = Unquantize((candidate << 1) | pbit, 7);
                int error = Math.Abs(reconstructed - value);
                if (error < bestError)
                {
                    bestError = error;
                    bestValue = candidate;
                }
            }

            return bestValue;
        }

        #endregion
    }
}
