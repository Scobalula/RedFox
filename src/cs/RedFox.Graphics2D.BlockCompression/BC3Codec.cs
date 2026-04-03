using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Codec for BC3 (DXT5) block-compressed format.
    /// Each 16-byte block contains an 8-byte interpolated alpha block followed by
    /// an 8-byte BC1-style color block. This provides smooth alpha gradients
    /// compared to BC2's explicit 4-bit alpha.
    /// </summary>
    public sealed class BC3Codec : IPixelCodec
    {
        private const int BytesPerBlock = 16;

        /// <summary>
        /// Initializes a new instance of the <see cref="BC3Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec instance handles.</param>
        public BC3Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.BC3Typeless => format,
                ImageFormat.BC3Unorm => format,
                ImageFormat.BC3UnormSrgb => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "BC3Codec supports only BC3Typeless, BC3Unorm, and BC3UnormSrgb."),
            };
        }

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 0;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height) =>
            BlockProcessor.DecodeBlocks(source, destination, width, height, BytesPerBlock, DecodeBlock);

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height) =>
            BlockProcessor.EncodeBlocks(source, destination, width, height, BytesPerBlock, EncodeBlock);

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
        public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height) =>
            BlockProcessor.DecodeRows(source, destination, startRow, rowCount, width, height, BytesPerBlock, DecodeBlock);

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) =>
            throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

        /// <inheritdoc/>
        public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height) =>
            BlockProcessor.DecodeBlocksTo(source, targetCodec, destination, width, height, BytesPerBlock, DecodeBlock);

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            // Decode source pixels to Vector4 buffer
            Vector4[] pixels = new Vector4[width * height];
            sourceCodec.Decode(source, pixels, width, height);

            // Encode to BC blocks
            BlockProcessor.EncodeBlocks(pixels, destination, width, height, BytesPerBlock, EncodeBlock);
        }

        /// <summary>
        /// Decodes a single 16-byte BC3 block into 16 RGBA <see cref="Vector4"/> pixels.
        /// </summary>
        /// <param name="block">The 16-byte compressed block.</param>
        /// <param name="pixels">The destination span receiving 16 decoded pixels.</param>
        public static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
        {
            Span<float> alphas = stackalloc float[16];
            BlockColorOperations.DecodeAlphaBlock(block, alphas, signed: false);

            var colorBlock = block[8..];
            ushort c0Raw = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock);
            ushort c1Raw = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock[2..]);
            uint indices = BinaryPrimitives.ReadUInt32LittleEndian(colorBlock[4..]);

            var c0 = BlockColorOperations.DecodeRgb565(c0Raw);
            var c1 = BlockColorOperations.DecodeRgb565(c1Raw);

            Span<Vector4> palette =
            [
                c0,
                c1,
                BlockColorOperations.Lerp(c0, c1, 1.0f / 3.0f),
                BlockColorOperations.Lerp(c0, c1, 2.0f / 3.0f),
            ];

            for (int i = 0; i < 16; i++)
            {
                int idx = (int)((indices >> (i * 2)) & 0x3);
                var color = palette[idx];
                pixels[i] = new Vector4(color.X, color.Y, color.Z, alphas[i]);
            }
        }

        /// <summary>
        /// Encodes 16 RGBA <see cref="Vector4"/> pixels into a single 16-byte BC3 block.
        /// Alpha uses 8-value interpolated palette; color uses BC1-style bounding-box compression.
        /// </summary>
        /// <param name="pixels">The 16 source pixels in 4×4 row-major order.</param>
        /// <param name="block">The destination 16-byte block.</param>
        public static void EncodeBlock(ReadOnlySpan<Vector4> pixels, Span<byte> block)
        {
            Span<float> alphaValues = stackalloc float[16];
            for (int i = 0; i < 16; i++)
                alphaValues[i] = Math.Clamp(pixels[i].W, 0f, 1f);

            BlockColorOperations.EncodeAlphaBlock(alphaValues, block[..8], signed: false);

            var colorBlock = block[8..];

            BlockColorOperations.FindMinMaxColorEndpoints(pixels, out var minColor, out var maxColor);
            Span<Vector4> palette = stackalloc Vector4[4];
            BlockColorOperations.BuildFourColorPalette(minColor, maxColor, out ushort c0Raw, out ushort c1Raw, palette);

            uint colorIndices = 0;
            for (int i = 0; i < 16; i++)
            {
                int bestIdx = BlockColorOperations.FindClosestColorIndex(pixels[i], palette);
                colorIndices |= (uint)bestIdx << (i * 2);
            }

            BinaryPrimitives.WriteUInt16LittleEndian(colorBlock, c0Raw);
            BinaryPrimitives.WriteUInt16LittleEndian(colorBlock[2..], c1Raw);
            BinaryPrimitives.WriteUInt32LittleEndian(colorBlock[4..], colorIndices);
        }
    }
}
