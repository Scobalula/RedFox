using System;
using System.Numerics;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.BlockCompression;

/// <summary>
/// Codec for BC3 (DXT5) block-compressed format.
/// Each 16-byte block contains an 8-byte interpolated alpha block followed by
/// an 8-byte BC1-style color block. This provides smooth alpha gradients
/// compared to BC2's explicit 4-bit alpha.
/// </summary>
/// <param name="format">The image format this codec instance handles.</param>
public sealed class BC3Codec(ImageFormat format) : IPixelCodec
{
    private const int BytesPerBlock = 16;

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.BC3Typeless => format,
        ImageFormat.BC3Unorm => format,
        ImageFormat.BC3UnormSrgb => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "BC3Codec supports only BC3Typeless, BC3Unorm, and BC3UnormSrgb."),
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
    /// Decodes a single 16-byte BC3 block into 16 RGBA <see cref="Vector4"/> pixels.
    /// </summary>
    /// <param name="block">The 16-byte compressed block.</param>
    /// <param name="pixels">The destination span receiving 16 decoded pixels.</param>
    public static void DecodeBlock(ReadOnlySpan<byte> block, Span<Vector4> pixels)
    {
        Span<float> alphas = stackalloc float[16];
        BlockColorOperations.DecodeAlphaBlock(block, alphas, signed: false);

        BlockColorOperations.DecodeFourColorBlock(block[8..], alphas, pixels);
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

        BlockColorOperations.EncodeFourColorBlock(pixels, block[8..]);
    }
}
