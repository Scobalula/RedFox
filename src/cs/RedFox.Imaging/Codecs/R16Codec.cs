using System;
using System.Buffers.Binary;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R16Typeless"/>, <see cref="ImageFormat.R16Float"/>, <see cref="ImageFormat.D16Unorm"/>, <see cref="ImageFormat.R16Unorm"/>, <see cref="ImageFormat.R16Uint"/>, <see cref="ImageFormat.R16Snorm"/>, and <see cref="ImageFormat.R16Sint"/>.
/// Unsigned variants decode to [0, 1], signed variants (Snorm, Sint) to [-1, 1], and <see cref="ImageFormat.R16Float"/> as half-precision floats.
/// </summary>
/// <param name="format">The image format this codec handles.</param>
public sealed class R16Codec(ImageFormat format) : IPixelCodec
{
    private readonly ComponentKind _kind = ComponentEncoding.GetKind(format);

    /// <inheritdoc/>
    public ImageFormat Format { get; } = format switch
    {
        ImageFormat.R16Typeless or ImageFormat.R16Float or ImageFormat.D16Unorm or ImageFormat.R16Unorm or ImageFormat.R16Uint or ImageFormat.R16Snorm or ImageFormat.R16Sint => format,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R16Codec supports only R16Typeless, R16Float, D16Unorm, R16Unorm, R16Uint, R16Snorm, and R16Sint."),
    };

    /// <inheritdoc/>
    public int BytesPerPixel => 2;

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
        return new Vector4(ComponentEncoding.Decode16(BinaryPrimitives.ReadUInt16LittleEndian(source[(pixelIndex * 2)..]), _kind), 0f, 0f, 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination[(pixelIndex * 2)..], ComponentEncoding.Encode16(pixel.X, _kind));
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R16Codec other && other._kind == _kind)
        {
            source[..(width * height * 2)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
