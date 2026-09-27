using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R32Typeless"/>, <see cref="ImageFormat.D32Float"/>, <see cref="ImageFormat.R32Float"/>, <see cref="ImageFormat.R32Uint"/>, and <see cref="ImageFormat.R32Sint"/>.
/// Float and typeless variants decode as 32-bit floats; <see cref="ImageFormat.R32Uint"/> decodes to [0, 1] and <see cref="ImageFormat.R32Sint"/> to [-1, 1].
/// </summary>
/// <param name="format">The image format this codec handles.</param>
public sealed class R32Codec(ImageFormat format) : IPixelCodec
{
    private readonly ComponentKind _kind = ComponentEncoding.GetKind(format);

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.R32Typeless or ImageFormat.D32Float or ImageFormat.R32Float or ImageFormat.R32Uint or ImageFormat.R32Sint => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R32Codec supports only R32Typeless, D32Float, R32Float, R32Uint, and R32Sint."),
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
        return new Vector4(ComponentEncoding.Decode32(source[(pixelIndex * 4)..], _kind), 0f, 0f, 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        ComponentEncoding.Encode32(pixel.X, destination[(pixelIndex * 4)..], _kind);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R32Codec other && other._kind == _kind)
        {
            source[..(width * height * 4)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
