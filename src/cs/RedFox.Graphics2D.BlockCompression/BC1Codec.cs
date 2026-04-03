using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Codec for BC1 (DXT1) block-compressed format.
    /// Each 8-byte block encodes a 4×4 pixel region using two RGB565 endpoint colors
    /// and a 32-bit index table selecting from a 4-color interpolated palette.
    /// Supports optional 1-bit alpha via the c0 ≤ c1 transparent-black mode.
    /// </summary>
    public sealed class BC1Codec : IPixelCodec
    {
        private const int BytesPerBlock = 8;

        /// <summary>
        /// Initializes a new instance of the <see cref="BC1Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec instance handles (e.g. <see cref="ImageFormat.BC1Unorm"/>).</param>
        public BC1Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.BC1Typeless => format,
                ImageFormat.BC1Unorm => format,
                ImageFormat.BC1UnormSrgb => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "BC1Codec supports only BC1Typeless, BC1Unorm, and BC1UnormSrgb."),
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
        /// Decodes a single 8-byte BC1 block into 16 RGBA <see cref="Vector4"/> pixels.
        /// </summary>
        /// <param name="block">The 8-byte compressed block.</param>
        /// <param name="pixels">The destination span receiving 16 decoded pixels.</param>
        public static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
        {
            ushort c0Raw = BinaryPrimitives.ReadUInt16LittleEndian(block);
            ushort c1Raw = BinaryPrimitives.ReadUInt16LittleEndian(block[2..]);
            uint indices = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);

            var c0 = BlockColorOperations.DecodeRgb565(c0Raw);
            var c1 = BlockColorOperations.DecodeRgb565(c1Raw);

            Span<Vector4> palette = stackalloc Vector4[4];
            palette[0] = c0;
            palette[1] = c1;

            if (c0Raw > c1Raw)
            {
                palette[2] = BlockColorOperations.Lerp(c0, c1, 1.0f / 3.0f);
                palette[3] = BlockColorOperations.Lerp(c0, c1, 2.0f / 3.0f);
            }
            else
            {
                palette[2] = BlockColorOperations.Lerp(c0, c1, 0.5f);
                palette[3] = new Vector4(0, 0, 0, 0);
            }

            for (int i = 0; i < 16; i++)
            {
                int idx = (int)((indices >> (i * 2)) & 0x3);
                pixels[i] = palette[idx];
            }
        }

        /// <summary>
        /// Encodes 16 RGBA <see cref="Vector4"/> pixels into a single 8-byte BC1 block
        /// using bounding-box endpoint selection and closest-match index assignment.
        /// </summary>
        /// <param name="pixels">The 16 source pixels in 4×4 row-major order.</param>
        /// <param name="block">The destination 8-byte block.</param>
        public static void EncodeBlock(ReadOnlySpan<Vector4> pixels, Span<byte> block)
        {
            BlockColorOperations.FindMinMaxColorEndpoints(pixels, out var minColor, out var maxColor);

            Span<Vector4> palette = stackalloc Vector4[4];
            BlockColorOperations.BuildFourColorPalette(minColor, maxColor, out ushort c0Raw, out ushort c1Raw, palette);

            uint indices = 0;
            for (int i = 0; i < 16; i++)
            {
                int bestIdx = BlockColorOperations.FindClosestColorIndex(pixels[i], palette);
                indices |= (uint)bestIdx << (i * 2);
            }

            BinaryPrimitives.WriteUInt16LittleEndian(block, c0Raw);
            BinaryPrimitives.WriteUInt16LittleEndian(block[2..], c1Raw);
            BinaryPrimitives.WriteUInt32LittleEndian(block[4..], indices);
        }
    }
}
