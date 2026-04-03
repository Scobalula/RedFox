using System.Numerics;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Codec for BC4 (single-channel) block-compressed format.
    /// Each 8-byte block encodes 16 single-channel values using two interpolated endpoints
    /// and 3-bit indices per pixel. Decoded as (R, 0, 0, 1).
    /// Supports both unsigned (Unorm) and signed (Snorm) modes.
    /// </summary>
    public sealed class BC4Codec : IPixelCodec
    {
        private const int BytesPerBlock = 8;

        /// <summary>
        /// Initializes a new instance of the <see cref="BC4Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec instance handles.</param>
        public BC4Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.BC4Typeless => format,
                ImageFormat.BC4Unorm => format,
                ImageFormat.BC4Snorm => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "BC4Codec supports only BC4Typeless, BC4Unorm, and BC4Snorm."),
            };
        }

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 0;

        private bool IsSigned => Format == ImageFormat.BC4Snorm;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.DecodeBlocks(source, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<byte> block, Span<Vector4> pixels) => DecodeBlockInner(block, pixels, signed));
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.EncodeBlocks(source, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<Vector4> pixels, Span<byte> block) => EncodeBlockInner(pixels, block, signed));
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
            DecodeBlockInner(source.Slice(blockOffset, BytesPerBlock), blockPixels, IsSigned);
            return blockPixels[(y % 4) * 4 + (x % 4)];
        }

        /// <inheritdoc/>
        public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.DecodeRows(source, destination, startRow, rowCount, width, height, BytesPerBlock,
                (ReadOnlySpan<byte> block, Span<Vector4> pixels) => DecodeBlockInner(block, pixels, signed));
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

        /// <inheritdoc/>
        public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            bool signed = IsSigned;
            BlockProcessor.DecodeBlocksTo(source, targetCodec, destination, width, height, BytesPerBlock,
                (ReadOnlySpan<byte> block, Span<Vector4> pixels) => DecodeBlockInner(block, pixels, signed));
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
                (ReadOnlySpan<Vector4> pixels, Span<byte> block) => EncodeBlockInner(pixels, block, signed));
        }

        private static void DecodeBlockInner(ReadOnlySpan<byte> block, Span<Vector4> pixels, bool signed)
        {
            Span<float> values = stackalloc float[16];
            BlockColorOperations.DecodeAlphaBlock(block, values, signed);

            for (int i = 0; i < 16; i++)
                pixels[i] = new Vector4(values[i], 0, 0, 1);
        }

        private static void EncodeBlockInner(ReadOnlySpan<Vector4> pixels, Span<byte> block, bool signed)
        {
            Span<float> values = stackalloc float[16];
            for (int i = 0; i < 16; i++)
                values[i] = pixels[i].X;

            BlockColorOperations.EncodeAlphaBlock(values, block, signed);
        }
    }
}
