using System.Numerics;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Shared helper methods for block-compressed format decoding.
    /// All BC formats operate on 4×4 pixel blocks.
    /// </summary>
    internal static class BCHelper
    {
        /// <summary>
        /// Iterates over 4×4 blocks in the image and invokes <paramref name="decodeBlock"/>
        /// for each block, writing decoded pixels into the destination span row by row.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DecodeBlocks(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height, int blockSize, DecodeBlockDelegate decodeBlock)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blocksY = Math.Max(1, (height + 3) / 4);
            int blockOffset = 0;

            Span<Vector4> blockPixels = stackalloc Vector4[16];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    decodeBlock(source.Slice(blockOffset, blockSize), blockPixels);

                    int baseX = bx * 4;
                    int baseY = by * 4;

                    for (int py = 0; py < 4; py++)
                    {
                        int destY = baseY + py;
                        if (destY >= height) break;

                        for (int px = 0; px < 4; px++)
                        {
                            int destX = baseX + px;
                            if (destX >= width) break;

                            destination[destY * width + destX] = blockPixels[py * 4 + px];
                        }
                    }

                    blockOffset += blockSize;
                }
            }
        }

        /// <summary>
        /// Iterates over 4×4 blocks and decodes each block, writing pixels directly
        /// into the target codec's byte layout via <see cref="IPixelCodec.WritePixel"/>.
        /// This avoids any heap-allocated <see cref="Vector4"/> array — only a
        /// 16-element stackalloc buffer is used per block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DecodeBlocksTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height, int blockSize, DecodeBlockDelegate decodeBlock)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blocksY = Math.Max(1, (height + 3) / 4);
            int blockOffset = 0;

            Span<Vector4> blockPixels = stackalloc Vector4[16];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    decodeBlock(source.Slice(blockOffset, blockSize), blockPixels);

                    int baseX = bx * 4;
                    int baseY = by * 4;

                    for (int py = 0; py < 4; py++)
                    {
                        int destY = baseY + py;
                        if (destY >= height) break;

                        int count = Math.Min(4, width - baseX);
                        targetCodec.WritePixels(blockPixels.Slice(py * 4, count), destination, destY * width + baseX);
                    }

                    blockOffset += blockSize;
                }
            }
        }

        /// <summary>
        /// Decodes block-compressed pixel rows in strips, writing decoded <see cref="Vector4"/> pixels
        /// into the destination span. Only the block-rows overlapping [startRow, startRow+rowCount) are decoded.
        /// Each block is decoded exactly once.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height, int blockSize, DecodeBlockDelegate decodeBlock)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blockRowStart = startRow / 4;
            int blockRowEnd = Math.Min((startRow + rowCount + 3) / 4, Math.Max(1, (height + 3) / 4));

            Span<Vector4> blockPixels = stackalloc Vector4[16];

            for (int by = blockRowStart; by < blockRowEnd; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int blockOffset = (by * blocksX + bx) * blockSize;
                    decodeBlock(source.Slice(blockOffset, blockSize), blockPixels);

                    int baseX = bx * 4;
                    int baseY = by * 4;

                    for (int py = 0; py < 4; py++)
                    {
                        int destY = baseY + py;
                        if (destY < startRow || destY >= startRow + rowCount || destY >= height) continue;

                        for (int px = 0; px < 4; px++)
                        {
                            int destX = baseX + px;
                            if (destX >= width) break;

                            destination[(destY - startRow) * width + destX] = blockPixels[py * 4 + px];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Iterates over 4×4 blocks in the image and invokes <paramref name="encodeBlock"/>
        /// for each block, reading source pixels and writing encoded data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void EncodeBlocks(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height, int blockSize, EncodeBlockDelegate encodeBlock)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blocksY = Math.Max(1, (height + 3) / 4);
            int blockOffset = 0;

            Span<Vector4> blockPixels = stackalloc Vector4[16];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int baseX = bx * 4;
                    int baseY = by * 4;

                    blockPixels.Clear();

                    for (int py = 0; py < 4; py++)
                    {
                        int srcY = Math.Min(baseY + py, height - 1);

                        for (int px = 0; px < 4; px++)
                        {
                            int srcX = Math.Min(baseX + px, width - 1);
                            blockPixels[py * 4 + px] = source[srcY * width + srcX];
                        }
                    }

                    encodeBlock(blockPixels, destination.Slice(blockOffset, blockSize));
                    blockOffset += blockSize;
                }
            }
        }

        /// <summary>
        /// Decodes a 5:6:5 RGB565 packed color into a <see cref="Vector4"/> with alpha = 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector4 DecodeRgb565(ushort packed)
        {
            float r = ((packed >> 11) & 0x1F) / 31.0f;
            float g = ((packed >> 5) & 0x3F) / 63.0f;
            float b = (packed & 0x1F) / 31.0f;
            return new Vector4(r, g, b, 1.0f);
        }

        /// <summary>
        /// Encodes a <see cref="Vector4"/> color to a 5:6:5 RGB565 packed value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort EncodeRgb565(Vector4 color)
        {
            int r = (int)(Math.Clamp(color.X, 0f, 1f) * 31f + 0.5f);
            int g = (int)(Math.Clamp(color.Y, 0f, 1f) * 63f + 0.5f);
            int b = (int)(Math.Clamp(color.Z, 0f, 1f) * 31f + 0.5f);
            return (ushort)((r << 11) | (g << 5) | b);
        }

        /// <summary>
        /// Linearly interpolates between two <see cref="Vector4"/> values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector4 Lerp(Vector4 a, Vector4 b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Decodes BC4/BC5 style alpha block (8 bytes) into 16 values.
        /// </summary>
        internal static void DecodeAlphaBlock(ReadOnlySpan<byte> block, Span<float> output, bool signed)
        {
            float alpha0, alpha1;

            if (signed)
            {
                alpha0 = (sbyte)block[0] / 127.0f;
                alpha1 = (sbyte)block[1] / 127.0f;
            }
            else
            {
                alpha0 = block[0] / 255.0f;
                alpha1 = block[1] / 255.0f;
            }

            Span<float> palette = stackalloc float[8];
            palette[0] = alpha0;
            palette[1] = alpha1;

            if (block[0] > block[1])
            {
                palette[2] = (6 * alpha0 + 1 * alpha1) / 7.0f;
                palette[3] = (5 * alpha0 + 2 * alpha1) / 7.0f;
                palette[4] = (4 * alpha0 + 3 * alpha1) / 7.0f;
                palette[5] = (3 * alpha0 + 4 * alpha1) / 7.0f;
                palette[6] = (2 * alpha0 + 5 * alpha1) / 7.0f;
                palette[7] = (1 * alpha0 + 6 * alpha1) / 7.0f;
            }
            else
            {
                palette[2] = (4 * alpha0 + 1 * alpha1) / 5.0f;
                palette[3] = (3 * alpha0 + 2 * alpha1) / 5.0f;
                palette[4] = (2 * alpha0 + 3 * alpha1) / 5.0f;
                palette[5] = (1 * alpha0 + 4 * alpha1) / 5.0f;
                palette[6] = signed ? -1.0f : 0.0f;
                palette[7] = 1.0f;
            }

            // 48 bits of indices packed into 6 bytes (block[2..7])
            ulong indices = 0;
            for (int i = 0; i < 6; i++)
            {
                indices |= (ulong)block[2 + i] << (8 * i);
            }

            for (int i = 0; i < 16; i++)
            {
                int idx = (int)((indices >> (3 * i)) & 0x7);
                output[i] = palette[idx];
            }
        }
    }

    internal delegate void DecodeBlockDelegate(ReadOnlySpan<byte> block, Span<Vector4> pixels);
    internal delegate void EncodeBlockDelegate(ReadOnlySpan<Vector4> pixels, Span<byte> block);
}
