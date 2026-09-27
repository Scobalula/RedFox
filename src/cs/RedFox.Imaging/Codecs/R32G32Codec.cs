using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R32G32Typeless"/>, <see cref="ImageFormat.R32G32Float"/>, <see cref="ImageFormat.R32G32Uint"/>, and <see cref="ImageFormat.R32G32Sint"/>.
/// Float and typeless variants decode as 32-bit floats; <see cref="ImageFormat.R32G32Uint"/> decodes to [0, 1] and <see cref="ImageFormat.R32G32Sint"/> to [-1, 1].
/// </summary>
/// <param name="format">The image format this codec handles.</param>
public sealed class R32G32Codec(ImageFormat format) : IPixelCodec
{
    private readonly ComponentKind _kind = ComponentEncoding.GetKind(format);

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.R32G32Typeless or ImageFormat.R32G32Float or ImageFormat.R32G32Uint or ImageFormat.R32G32Sint => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R32G32Codec supports only R32G32Typeless, R32G32Float, R32G32Uint, and R32G32Sint."),
    };

    /// <inheritdoc/>
    public int BytesPerPixel => 8;

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
        ReadOnlySpan<byte> pixel = source.Slice(pixelIndex * 8, 8);
        return new Vector4(ComponentEncoding.Decode32(pixel, _kind), ComponentEncoding.Decode32(pixel[4..], _kind), 0f, 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        Span<byte> target = destination.Slice(pixelIndex * 8, 8);
        ComponentEncoding.Encode32(pixel.X, target, _kind);
        ComponentEncoding.Encode32(pixel.Y, target[4..], _kind);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R32G32Codec other && other._kind == _kind)
        {
            source[..(width * height * 8)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
