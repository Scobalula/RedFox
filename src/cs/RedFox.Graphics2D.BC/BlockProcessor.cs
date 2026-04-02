using System.Numerics;
using System.Runtime.CompilerServices;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Provides block-level iteration logic for encoding and decoding 4×4 block-compressed formats.
    /// All BC formats operate on 4×4 pixel blocks; this class centralizes the block walk,
    /// boundary clamping, and pixel scatter/gather operations.
    /// </summary>
    public static class BlockProcessor
    {
        /// <summary>
        /// Iterates over 4×4 blocks in the image, invoking <paramref name="decoder"/> for each block
        /// and scattering the decoded pixels into the contiguous <paramref name="destination"/> span.
        /// </summary>
        /// <param name="source">The source compressed data.</param>
        /// <param name="destination">The destination span receiving decoded RGBA <see cref="Vector4"/> pixels.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="bytesPerBlock">The number of bytes per compressed block (8 for BC1/BC4, 16 for others).</param>
        /// <param name="decoder">The per-block decode function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodeBlocks(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height, int bytesPerBlock, BlockDecoder decoder)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blocksY = Math.Max(1, (height + 3) / 4);
            int blockOffset = 0;

            Span<Vector4> blockPixels = stackalloc Vector4[16];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    decoder(source.Slice(blockOffset, bytesPerBlock), blockPixels);

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

                    blockOffset += bytesPerBlock;
                }
            }
        }

        /// <summary>
        /// Iterates over 4×4 blocks and decodes each block, writing pixels directly
        /// into the target codec's byte layout via <see cref="IPixelCodec.WritePixels"/>.
        /// Uses only a 16-element stackalloc <see cref="Vector4"/> buffer per block, achieving zero heap allocation.
        /// </summary>
        /// <param name="source">The source compressed data.</param>
        /// <param name="targetCodec">The target codec defining the output byte layout.</param>
        /// <param name="destination">The destination buffer in the target codec's native format.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="bytesPerBlock">The number of bytes per compressed block.</param>
        /// <param name="decoder">The per-block decode function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodeBlocksTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height, int bytesPerBlock, BlockDecoder decoder)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blocksY = Math.Max(1, (height + 3) / 4);
            int blockOffset = 0;

            Span<Vector4> blockPixels = stackalloc Vector4[16];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    decoder(source.Slice(blockOffset, bytesPerBlock), blockPixels);

                    int baseX = bx * 4;
                    int baseY = by * 4;

                    for (int py = 0; py < 4; py++)
                    {
                        int destY = baseY + py;
                        if (destY >= height) break;

                        int count = Math.Min(4, width - baseX);
                        targetCodec.WritePixels(blockPixels.Slice(py * 4, count), destination, destY * width + baseX);
                    }

                    blockOffset += bytesPerBlock;
                }
            }
        }

        /// <summary>
        /// Decodes block-compressed pixel rows in strips, writing decoded <see cref="Vector4"/> pixels
        /// into the destination span. Only the block-rows overlapping [<paramref name="startRow"/>,
        /// <paramref name="startRow"/> + <paramref name="rowCount"/>) are decoded.
        /// </summary>
        /// <param name="source">The source compressed data.</param>
        /// <param name="destination">The destination span receiving decoded RGBA pixels for the requested rows.</param>
        /// <param name="startRow">The first row to decode.</param>
        /// <param name="rowCount">The number of rows to decode.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="bytesPerBlock">The number of bytes per compressed block.</param>
        /// <param name="decoder">The per-block decode function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height, int bytesPerBlock, BlockDecoder decoder)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blockRowStart = startRow / 4;
            int blockRowEnd = Math.Min((startRow + rowCount + 3) / 4, Math.Max(1, (height + 3) / 4));

            Span<Vector4> blockPixels = stackalloc Vector4[16];

            for (int by = blockRowStart; by < blockRowEnd; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int blockOffset = (by * blocksX + bx) * bytesPerBlock;
                    decoder(source.Slice(blockOffset, bytesPerBlock), blockPixels);

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
        /// Iterates over 4×4 blocks in the image, gathering source pixels into each block
        /// and invoking <paramref name="encoder"/> to produce compressed block data.
        /// Edge pixels are clamped (repeated) when the image dimensions are not multiples of 4.
        /// </summary>
        /// <param name="source">The source RGBA pixels.</param>
        /// <param name="destination">The destination buffer receiving compressed block data.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="bytesPerBlock">The number of bytes per compressed block.</param>
        /// <param name="encoder">The per-block encode function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EncodeBlocks(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height, int bytesPerBlock, BlockEncoder encoder)
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

                    encoder(blockPixels, destination.Slice(blockOffset, bytesPerBlock));
                    blockOffset += bytesPerBlock;
                }
            }
        }
    }
}
