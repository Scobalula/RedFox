using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.B8G8R8A8Unorm"/>, <see cref="ImageFormat.B8G8R8A8UnormSrgb"/>, and <see cref="ImageFormat.B8G8R8A8Typeless"/>.
/// Values decode to [0, 1]; sRGB values are returned as stored, without linearisation.
/// </summary>
/// <param name="format">The BGRA pixel format handled by this codec.</param>
public sealed class B8G8R8A8Codec(ImageFormat format) : IPixelCodec
{
    private const float Inv255 = 1.0f / 255.0f;

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb or ImageFormat.B8G8R8A8Typeless => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "B8G8R8A8Codec supports only B8G8R8A8Unorm, B8G8R8A8UnormSrgb, and B8G8R8A8Typeless."),
    };

    /// <inheritdoc/>
    public int BytesPerPixel => 4;

    /// <inheritdoc/>
    public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
    {
        PixelSimd.DecodeBgra8(source, destination, width * height);
    }

    /// <inheritdoc/>
    public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
    {
        PixelSimd.EncodeToBgra8(source, destination, width * height);
    }

    /// <inheritdoc/>
    public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
    {
        int offset = pixelIndex * 4;
        return new Vector4(source[offset + 2] * Inv255, source[offset + 1] * Inv255, source[offset] * Inv255, source[offset + 3] * Inv255);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        int offset = pixelIndex * 4;
        destination[offset] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
        destination[offset + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
        destination[offset + 2] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
        destination[offset + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
    }

    /// <inheritdoc/>
    public void WritePixels(ReadOnlySpan<Vector4> pixels, Span<byte> destination, int startPixelIndex)
    {
        PixelSimd.EncodeToBgra8(pixels, destination[(startPixelIndex * 4)..], pixels.Length);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        int pixelCount = width * height;

        if (sourceCodec is B8G8R8A8Codec)
        {
            source[..(pixelCount * 4)].CopyTo(destination);
            return;
        }

        if (sourceCodec is R8G8B8A8Codec { IsUnsigned: true })
        {
            PixelSimd.SwizzleRedBlue(source, destination, pixelCount);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
