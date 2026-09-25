using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R32G32B32Typeless"/>, <see cref="ImageFormat.R32G32B32Float"/>, <see cref="ImageFormat.R32G32B32Uint"/>, and <see cref="ImageFormat.R32G32B32Sint"/>.
/// Float and typeless variants decode as 32-bit floats; <see cref="ImageFormat.R32G32B32Uint"/> decodes to [0, 1] and <see cref="ImageFormat.R32G32B32Sint"/> to [-1, 1].
/// </summary>
public sealed class R32G32B32Codec : IPixelCodec
{
    private readonly ComponentKind _kind;

    /// <inheritdoc/>
    public ImageFormat Format { get; }

    /// <inheritdoc/>
    public int BytesPerPixel => 12;

    /// <summary>
    /// Initializes a new instance of the <see cref="R32G32B32Codec"/> class for the specified format variant.
    /// </summary>
    /// <param name="format">The image format this codec handles.</param>
    public R32G32B32Codec(ImageFormat format)
    {
        Format = format switch
        {
            ImageFormat.R32G32B32Typeless or ImageFormat.R32G32B32Float or ImageFormat.R32G32B32Uint or ImageFormat.R32G32B32Sint => format,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R32G32B32Codec supports only R32G32B32Typeless, R32G32B32Float, R32G32B32Uint, and R32G32B32Sint."),
        };

        _kind = ComponentEncoding.GetKind(format);
    }

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
        ReadOnlySpan<byte> pixel = source.Slice(pixelIndex * 12, 12);
        return new Vector4(ComponentEncoding.Decode32(pixel, _kind), ComponentEncoding.Decode32(pixel[4..], _kind), ComponentEncoding.Decode32(pixel[8..], _kind), 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        Span<byte> target = destination.Slice(pixelIndex * 12, 12);
        ComponentEncoding.Encode32(pixel.X, target, _kind);
        ComponentEncoding.Encode32(pixel.Y, target[4..], _kind);
        ComponentEncoding.Encode32(pixel.Z, target[8..], _kind);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R32G32B32Codec other && other._kind == _kind)
        {
            source[..(width * height * 12)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
