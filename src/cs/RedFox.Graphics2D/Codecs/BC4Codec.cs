using System.Numerics;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for BC4 (single-channel) block-compressed format.
    /// Each 8-byte block encodes 16 single-channel values using interpolated endpoints.
    /// Decoded as (R, 0, 0, 1).
    /// </summary>
    public sealed class BC4Codec(ImageFormat format) : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format { get; } = format;

        /// <inheritdoc/>
        public int BytesPerPixel => 0;

        private bool IsSigned => Format == ImageFormat.BC4Snorm;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            bool signed = IsSigned;
            BCHelper.DecodeBlocks(source, destination, width, height, 8, (ReadOnlySpan<byte> block, Span<Vector4> pixels) =>
            {
                DecodeBlockInner(block, pixels, signed);
            });
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            bool signed = IsSigned;
            BCHelper.EncodeBlocks(source, destination, width, height, 8, (ReadOnlySpan<Vector4> pixels, Span<byte> block) =>
            {
                float minVal = float.MaxValue;
                float maxVal = float.MinValue;

                for (int i = 0; i < 16; i++)
                {
                    float v = signed ? Math.Clamp(pixels[i].X, -1f, 1f) : Math.Clamp(pixels[i].X, 0f, 1f);
                    minVal = Math.Min(minVal, v);
                    maxVal = Math.Max(maxVal, v);
                }

                if (signed)
                {
                    block[0] = (byte)(sbyte)(maxVal * 127f);
                    block[1] = (byte)(sbyte)(minVal * 127f);
                }
                else
                {
                    block[0] = (byte)(maxVal * 255f + 0.5f);
                    block[1] = (byte)(minVal * 255f + 0.5f);
                }

                float a0 = signed ? (sbyte)block[0] / 127.0f : block[0] / 255.0f;
                float a1 = signed ? (sbyte)block[1] / 127.0f : block[1] / 255.0f;

                Span<float> palette = stackalloc float[8];
                palette[0] = a0;
                palette[1] = a1;

                if (block[0] > block[1])
                {
                    for (int i = 1; i <= 6; i++)
                        palette[1 + i] = ((7 - i) * a0 + i * a1) / 7.0f;
                }
                else
                {
                    for (int i = 1; i <= 4; i++)
                        palette[1 + i] = ((5 - i) * a0 + i * a1) / 5.0f;
                    palette[6] = signed ? -1.0f : 0.0f;
                    palette[7] = 1.0f;
                }

                ulong indices = 0;
                for (int i = 0; i < 16; i++)
                {
                    float v = signed ? Math.Clamp(pixels[i].X, -1f, 1f) : Math.Clamp(pixels[i].X, 0f, 1f);
                    int bestIdx = 0;
                    float bestDist = float.MaxValue;
                    for (int j = 0; j < 8; j++)
                    {
                        float dist = MathF.Abs(v - palette[j]);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestIdx = j;
                        }
                    }
                    indices |= (ulong)bestIdx << (3 * i);
                }

                for (int i = 0; i < 6; i++)
                    block[2 + i] = (byte)((indices >> (8 * i)) & 0xFF);
            });
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
            DecodeBlockInner(source.Slice(blockOffset, 8), blockPixels, IsSigned);
            return blockPixels[(y % 4) * 4 + (x % 4)];
        }

        /// <inheritdoc/>
        public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height)
        {
            bool signed = IsSigned;
            BCHelper.DecodeRows(source, destination, startRow, rowCount, width, height, 8, (ReadOnlySpan<byte> block, Span<Vector4> pixels) =>
            {
                DecodeBlockInner(block, pixels, signed);
            });
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

        /// <inheritdoc/>
        public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            bool signed = IsSigned;
            BCHelper.DecodeBlocksTo(source, targetCodec, destination, width, height, 8, (ReadOnlySpan<byte> block, Span<Vector4> pixels) =>
            {
                DecodeBlockInner(block, pixels, signed);
            });
        }

        private static void DecodeBlockInner(ReadOnlySpan<byte> block, Span<Vector4> pixels, bool signed)
        {
            Span<float> values = stackalloc float[16];
            BCHelper.DecodeAlphaBlock(block, values, signed);

            for (int i = 0; i < 16; i++)
                pixels[i] = new Vector4(values[i], 0, 0, 1);
        }
    }
}
