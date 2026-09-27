using System;
using System.Numerics;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.BlockCompression;

/// <summary>
/// Codec for BC2 (DXT3) block-compressed format.
/// Each 16-byte block contains 8 bytes of explicit 4-bit-per-pixel alpha data
/// followed by an 8-byte BC1-style color block.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BC2Codec"/> class for the specified format variant.
/// </remarks>
/// <param name="format">The image format this codec instance handles.</param>
public sealed class BC2Codec(ImageFormat format) : IPixelCodec
{
    private const int BytesPerBlock = 16;

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.BC2Typeless => format,
        ImageFormat.BC2Unorm => format,
        ImageFormat.BC2UnormSrgb => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "BC2Codec supports only BC2Typeless, BC2Unorm, and BC2UnormSrgb."),
    };

    /// <inheritdoc/>
    public int BytesPerPixel => 0;

    /// <inheritdoc/>
    public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height) => BlockProcessor.DecodeBlocks(source, destination, width, height, BytesPerBlock, DecodeBlock);

    /// <inheritdoc/>
    public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height) => BlockProcessor.EncodeBlocks(source, destination, width, height, BytesPerBlock, EncodeBlock);

    /// <inheritdoc/>
    public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex) => throw new NotSupportedException("Block-compressed formats do not support per-pixel reads by flat index.");

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
    public void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height) => BlockProcessor.DecodeRows(source, destination, startRow, rowCount, width, height, BytesPerBlock, DecodeBlock);

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex) => throw new NotSupportedException("Block-compressed formats do not support per-pixel writes.");

    /// <inheritdoc/>
    public void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height) => BlockProcessor.DecodeBlocksTo(source, targetCodec, destination, width, height, BytesPerBlock, DecodeBlock);

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        Vector4[] pixels = new Vector4[width * height];
        sourceCodec.Decode(source, pixels, width, height);

        BlockProcessor.EncodeBlocks(pixels, destination, width, height, BytesPerBlock, EncodeBlock);
    }

    /// <summary>
    /// Decodes a single 16-byte BC2 block into 16 RGBA <see cref="Vector4"/> pixels.
    /// </summary>
    /// <param name="block">The 16-byte compressed block.</param>
    /// <param name="pixels">The destination span receiving 16 decoded pixels.</param>
    public static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
    {
        Span<float> alphas = stackalloc float[16];
        for (int i = 0; i < 16; i++)
        {
            int byteIndex = i / 2;
            int nibble = (i & 1) == 0 ? (block[byteIndex] & 0xF) : (block[byteIndex] >> 4);
            alphas[i] = nibble / 15.0f;
        }

        BlockColorOperations.DecodeFourColorBlock(block[8..], alphas, pixels);
    }

    /// <summary>
    /// Encodes 16 RGBA <see cref="Vector4"/> pixels into a single 16-byte BC2 block.
    /// Alpha is stored as explicit 4-bit values; color uses BC1-style bounding-box compression.
    /// </summary>
    /// <param name="pixels">The 16 source pixels in 4×4 row-major order.</param>
    /// <param name="block">The destination 16-byte block.</param>
    public static void EncodeBlock(ReadOnlySpan<Vector4> pixels, Span<byte> block)
    {
        for (int i = 0; i < 16; i++)
        {
            int a = (int)(Math.Clamp(pixels[i].W, 0f, 1f) * 15f + 0.5f);
            int byteIndex = i / 2;
            if ((i & 1) == 0)
                block[byteIndex] = (byte)(a & 0xF);
            else
                block[byteIndex] |= (byte)((a & 0xF) << 4);
        }

        var colorBlock = block[8..];

        BlockColorOperations.EncodeFourColorBlock(pixels, colorBlock);
    }
}
