using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.B8G8R8X8Unorm"/>, <see cref="ImageFormat.B8G8R8X8UnormSrgb"/>, and <see cref="ImageFormat.B8G8R8X8Typeless"/>.
/// The unused fourth byte always decodes as opaque alpha and is written as 255.
/// </summary>
/// <param name="format">The BGRX pixel format handled by this codec.</param>
public sealed class B8G8R8X8Codec(ImageFormat format) : IPixelCodec
{
    private const float Inv255 = 1.0f / 255.0f;

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb or ImageFormat.B8G8R8X8Typeless => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "B8G8R8X8Codec supports only B8G8R8X8Unorm, B8G8R8X8UnormSrgb, and B8G8R8X8Typeless."),
    };

    /// <inheritdoc/>
    public int BytesPerPixel => 4;

    /// <inheritdoc/>
    public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
    {
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
            destination[i] = ReadPixel(source, i);
    }

    /// <inheritdoc/>
    public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
    {
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
            WritePixel(source[i], destination, i);
    }

    /// <inheritdoc/>
    public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
    {
        int offset = pixelIndex * 4;
        return new Vector4(source[offset + 2] * Inv255, source[offset + 1] * Inv255, source[offset] * Inv255, 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        int offset = pixelIndex * 4;
        destination[offset] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
        destination[offset + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
        destination[offset + 2] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
        destination[offset + 3] = 255;
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is B8G8R8X8Codec)
        {
            source[..(width * height * 4)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
