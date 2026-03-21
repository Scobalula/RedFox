using System.Buffers.Binary;
using System.Numerics;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for BC3 (DXT5) block-compressed format.
    /// Each 16-byte block contains an 8-byte interpolated alpha block followed by
    /// an 8-byte BC1-style color block.
    /// </summary>
    public sealed class BC3Codec(ImageFormat format) : IPixelCodec
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
            BCHelper.EncodeBlocks(source, destination, width, height, 16, EncodeBlock);
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

        private static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
        {
            // First 8 bytes: interpolated alpha block
            Span<float> alphas = stackalloc float[16];
            BCHelper.DecodeAlphaBlock(block, alphas, signed: false);

            // Next 8 bytes: BC1 color block
            var colorBlock = block[8..];
            ushort c0Raw = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock);
            ushort c1Raw = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock[2..]);
            uint indices = BinaryPrimitives.ReadUInt32LittleEndian(colorBlock[4..]);

            var c0 = BCHelper.DecodeRgb565(c0Raw);
            var c1 = BCHelper.DecodeRgb565(c1Raw);

            Span<Vector4> palette =
            [
                c0,
                c1,
                BCHelper.Lerp(c0, c1, 1.0f / 3.0f),
                BCHelper.Lerp(c0, c1, 2.0f / 3.0f),
            ];
            for (int i = 0; i < 16; i++)
            {
                int idx = (int)((indices >> (i * 2)) & 0x3);
                var color = palette[idx];
                pixels[i] = new Vector4(color.X, color.Y, color.Z, alphas[i]);
            }
        }

        private static void EncodeBlock(ReadOnlySpan<Vector4> pixels, Span<byte> block)
        {
            // Find min/max alpha
            float minAlpha = float.MaxValue;
            float maxAlpha = float.MinValue;

            for (int i = 0; i < 16; i++)
            {
                float a = Math.Clamp(pixels[i].W, 0f, 1f);
                minAlpha = Math.Min(minAlpha, a);
                maxAlpha = Math.Max(maxAlpha, a);
            }

            byte a0 = (byte)(maxAlpha * 255f + 0.5f);
            byte a1 = (byte)(minAlpha * 255f + 0.5f);

            block[0] = a0;
            block[1] = a1;

            // Build alpha palette for index selection
            Span<float> alphaPalette = stackalloc float[8];
            alphaPalette[0] = a0 / 255.0f;
            alphaPalette[1] = a1 / 255.0f;

            if (a0 > a1)
            {
                for (int i = 1; i <= 6; i++)
                    alphaPalette[1 + i] = ((7 - i) * alphaPalette[0] + i * alphaPalette[1]) / 7.0f;
            }
            else
            {
                for (int i = 1; i <= 4; i++)
                    alphaPalette[1 + i] = ((5 - i) * alphaPalette[0] + i * alphaPalette[1]) / 5.0f;
                alphaPalette[6] = 0.0f;
                alphaPalette[7] = 1.0f;
            }

            // Pack 3-bit indices into 6 bytes
            ulong alphaIndices = 0;
            for (int i = 0; i < 16; i++)
            {
                float a = Math.Clamp(pixels[i].W, 0f, 1f);
                int bestIdx = 0;
                float bestDist = float.MaxValue;
                for (int j = 0; j < 8; j++)
                {
                    float dist = MathF.Abs(a - alphaPalette[j]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = j;
                    }
                }
                alphaIndices |= (ulong)bestIdx << (3 * i);
            }

            for (int i = 0; i < 6; i++)
                block[2 + i] = (byte)((alphaIndices >> (8 * i)) & 0xFF);

            // BC1-style color block
            var colorBlock = block[8..];

            var min = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 1);
            var max = new Vector4(float.MinValue, float.MinValue, float.MinValue, 1);

            for (int i = 0; i < 16; i++)
            {
                min = Vector4.Min(min, pixels[i]);
                max = Vector4.Max(max, pixels[i]);
            }

            ushort c0Raw = BCHelper.EncodeRgb565(max);
            ushort c1Raw = BCHelper.EncodeRgb565(min);

            if (c0Raw < c1Raw)
                (c0Raw, c1Raw) = (c1Raw, c0Raw);
            if (c0Raw == c1Raw && c0Raw < 0xFFFF)
                c0Raw++;

            var c0 = BCHelper.DecodeRgb565(c0Raw);
            var c1 = BCHelper.DecodeRgb565(c1Raw);

            Span<Vector4> palette = stackalloc Vector4[4];
            palette[0] = c0;
            palette[1] = c1;
            palette[2] = BCHelper.Lerp(c0, c1, 1.0f / 3.0f);
            palette[3] = BCHelper.Lerp(c0, c1, 2.0f / 3.0f);

            uint colorIndices = 0;
            for (int i = 0; i < 16; i++)
            {
                int bestIdx = 0;
                float bestDist = float.MaxValue;
                for (int j = 0; j < 4; j++)
                {
                    var diff = pixels[i] - palette[j];
                    float dist = diff.X * diff.X + diff.Y * diff.Y + diff.Z * diff.Z;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = j;
                    }
                }
                colorIndices |= (uint)bestIdx << (i * 2);
            }

            BinaryPrimitives.WriteUInt16LittleEndian(colorBlock, c0Raw);
            BinaryPrimitives.WriteUInt16LittleEndian(colorBlock[2..], c1Raw);
            BinaryPrimitives.WriteUInt32LittleEndian(colorBlock[4..], colorIndices);
        }
    }
}
