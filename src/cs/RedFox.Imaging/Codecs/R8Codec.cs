using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R8Typeless"/>, <see cref="ImageFormat.R8Unorm"/>, <see cref="ImageFormat.R8Uint"/>, <see cref="ImageFormat.R8Snorm"/>, and <see cref="ImageFormat.R8Sint"/>.
/// Unsigned variants decode to [0, 1] and signed variants (Snorm, Sint) to [-1, 1].
/// </summary>
/// <param name="format">The image format this codec handles.</param>
public sealed class R8Codec(ImageFormat format) : IPixelCodec
{
    private readonly ComponentKind _kind = ComponentEncoding.GetKind(format);

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.R8Typeless or ImageFormat.R8Unorm or ImageFormat.R8Uint or ImageFormat.R8Snorm or ImageFormat.R8Sint => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R8Codec supports only R8Typeless, R8Unorm, R8Uint, R8Snorm, and R8Sint."),
    };

    /// <inheritdoc/>
    public int BytesPerPixel => 1;

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
        return new Vector4(ComponentEncoding.Decode8(source[pixelIndex], _kind), 0f, 0f, 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        destination[pixelIndex] = ComponentEncoding.Encode8(pixel.X, _kind);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R8Codec other && other._kind == _kind)
        {
            source[..(width * height)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
