using System;
using System.Buffers.Binary;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R16G16Typeless"/>, <see cref="ImageFormat.R16G16Float"/>, <see cref="ImageFormat.R16G16Unorm"/>, <see cref="ImageFormat.R16G16Uint"/>, <see cref="ImageFormat.R16G16Snorm"/>, and <see cref="ImageFormat.R16G16Sint"/>.
/// Unsigned variants decode to [0, 1], signed variants (Snorm, Sint) to [-1, 1], and <see cref="ImageFormat.R16G16Float"/> as half-precision floats.
/// </summary>
public sealed class R16G16Codec : IPixelCodec
{
    private readonly ComponentKind _kind;

    /// <inheritdoc/>
    public ImageFormat Format { get; }

    /// <inheritdoc/>
    public int BytesPerPixel => 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="R16G16Codec"/> class for the specified format variant.
    /// </summary>
    /// <param name="format">The image format this codec handles.</param>
    public R16G16Codec(ImageFormat format)
    {
        Format = format switch
        {
            ImageFormat.R16G16Typeless or ImageFormat.R16G16Float or ImageFormat.R16G16Unorm or ImageFormat.R16G16Uint or ImageFormat.R16G16Snorm or ImageFormat.R16G16Sint => format,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R16G16Codec supports only R16G16Typeless, R16G16Float, R16G16Unorm, R16G16Uint, R16G16Snorm, and R16G16Sint."),
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
        ReadOnlySpan<byte> pixel = source.Slice(pixelIndex * 4, 4);
        return new Vector4(ComponentEncoding.Decode16(BinaryPrimitives.ReadUInt16LittleEndian(pixel), _kind), ComponentEncoding.Decode16(BinaryPrimitives.ReadUInt16LittleEndian(pixel[2..]), _kind), 0f, 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        Span<byte> target = destination.Slice(pixelIndex * 4, 4);
        BinaryPrimitives.WriteUInt16LittleEndian(target, ComponentEncoding.Encode16(pixel.X, _kind));
        BinaryPrimitives.WriteUInt16LittleEndian(target[2..], ComponentEncoding.Encode16(pixel.Y, _kind));
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R16G16Codec other && other._kind == _kind)
        {
            source[..(width * height * 4)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
