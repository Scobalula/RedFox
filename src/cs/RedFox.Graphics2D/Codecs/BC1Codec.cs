using System.Buffers.Binary;
using System.Numerics;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for BC1 (DXT1) block-compressed format.
    /// Each 8-byte block encodes a 4×4 pixel block with optional 1-bit alpha.
    /// </summary>
    public sealed class BC1Codec(ImageFormat format) : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format { get; } = format;

        /// <inheritdoc/>
        public int BytesPerPixel => 0;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            BCHelper.DecodeBlocks(source, destination, width, height, 8, DecodeBlock);
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            BCHelper.EncodeBlocks(source, destination, width, height, 8, EncodeBlock);
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel reads.");

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int x, int y, int width)
        {
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blockOffset = ((y / 4) * blocksX + (x / 4)) * 8;
            Span<Vector4> blockPixels = stackalloc Vector4[16];
            DecodeBlock(source.Slice(blockOffset, 8), blockPixels);
            return blockPixels[(y % 4) * 4 + (x % 4)];
        }

        /// <inheritdoc/>
        public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height)
        {
            BCHelper.DecodeRows(source, destination, startRow, rowCount, width, height, 8, DecodeBlock);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

        /// <inheritdoc/>
        public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            BCHelper.DecodeBlocksTo(source, targetCodec, destination, width, height, 8, DecodeBlock);
        }

        private static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
        {
            ushort c0Raw = BinaryPrimitives.ReadUInt16LittleEndian(block);
            ushort c1Raw = BinaryPrimitives.ReadUInt16LittleEndian(block[2..]);
            uint indices = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);

            var c0 = BCHelper.DecodeRgb565(c0Raw);
            var c1 = BCHelper.DecodeRgb565(c1Raw);

            Span<Vector4> palette = stackalloc Vector4[4];
            palette[0] = c0;
            palette[1] = c1;

            if (c0Raw > c1Raw)
            {
                palette[2] = BCHelper.Lerp(c0, c1, 1.0f / 3.0f);
                palette[3] = BCHelper.Lerp(c0, c1, 2.0f / 3.0f);
            }
            else
            {
                palette[2] = BCHelper.Lerp(c0, c1, 0.5f);
                palette[3] = new Vector4(0, 0, 0, 0); // transparent black
            }

            for (int i = 0; i < 16; i++)
            {
                int idx = (int)((indices >> (i * 2)) & 0x3);
                pixels[i] = palette[idx];
            }
        }

        private static void EncodeBlock(ReadOnlySpan<Vector4> pixels, Span<byte> block)
        {
            // Simple min/max endpoint selection
            var min = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 1);
            var max = new Vector4(float.MinValue, float.MinValue, float.MinValue, 1);

            for (int i = 0; i < 16; i++)
            {
                var p = pixels[i];
                min = Vector4.Min(min, p);
                max = Vector4.Max(max, p);
            }

            ushort c0Raw = BCHelper.EncodeRgb565(max);
            ushort c1Raw = BCHelper.EncodeRgb565(min);

            // Ensure c0 > c1 for 4-color mode
            if (c0Raw < c1Raw)
                (c0Raw, c1Raw) = (c1Raw, c0Raw);

            // If equal, just offset
            if (c0Raw == c1Raw && c0Raw < 0xFFFF)
                c0Raw++;

            var c0 = BCHelper.DecodeRgb565(c0Raw);
            var c1 = BCHelper.DecodeRgb565(c1Raw);

            Span<Vector4> palette = stackalloc Vector4[4];
            palette[0] = c0;
            palette[1] = c1;
            palette[2] = BCHelper.Lerp(c0, c1, 1.0f / 3.0f);
            palette[3] = BCHelper.Lerp(c0, c1, 2.0f / 3.0f);

            uint indices = 0;
            for (int i = 0; i < 16; i++)
            {
                int bestIdx = 0;
                float bestDist = float.MaxValue;

                for (int j = 0; j < 4; j++)
                {
                    var diff = pixels[i] - palette[j];
                    float dist = Vector3.Dot(
                        new Vector3(diff.X, diff.Y, diff.Z),
                        new Vector3(diff.X, diff.Y, diff.Z));

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = j;
                    }
                }

                indices |= (uint)bestIdx << (i * 2);
            }

            BinaryPrimitives.WriteUInt16LittleEndian(block, c0Raw);
            BinaryPrimitives.WriteUInt16LittleEndian(block[2..], c1Raw);
            BinaryPrimitives.WriteUInt32LittleEndian(block[4..], indices);
        }
    }
}
