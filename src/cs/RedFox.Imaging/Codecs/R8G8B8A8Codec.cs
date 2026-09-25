using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for every 8-bit-per-channel RGBA format: <see cref="ImageFormat.R8G8B8A8Unorm"/>, <see cref="ImageFormat.R8G8B8A8UnormSrgb"/>, <see cref="ImageFormat.R8G8B8A8Typeless"/>, <see cref="ImageFormat.R8G8B8A8Uint"/>, <see cref="ImageFormat.R8G8B8A8Snorm"/>, and <see cref="ImageFormat.R8G8B8A8Sint"/>.
/// Unsigned variants decode to [0, 1] and signed variants (Snorm, Sint) to [-1, 1]. sRGB values are returned as stored, without linearisation.
/// </summary>
public sealed class R8G8B8A8Codec : IPixelCodec
{
    private readonly ComponentKind _kind;

    /// <inheritdoc/>
    public ImageFormat Format { get; }

    /// <inheritdoc/>
    public int BytesPerPixel => 4;

    internal bool IsUnsigned => _kind == ComponentKind.Unsigned;

    /// <summary>
    /// Initializes a new instance of the <see cref="R8G8B8A8Codec"/> class for the specified format variant.
    /// </summary>
    /// <param name="format">The image format this codec handles.</param>
    public R8G8B8A8Codec(ImageFormat format)
    {
        Format = format switch
        {
            ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb or ImageFormat.R8G8B8A8Typeless or ImageFormat.R8G8B8A8Uint => format,
            ImageFormat.R8G8B8A8Snorm or ImageFormat.R8G8B8A8Sint => format,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R8G8B8A8Codec supports only the R8G8B8A8 Unorm, UnormSrgb, Typeless, Uint, Snorm, and Sint formats."),
        };

        _kind = format is ImageFormat.R8G8B8A8Snorm or ImageFormat.R8G8B8A8Sint ? ComponentKind.Signed : ComponentKind.Unsigned;
    }

    /// <inheritdoc/>
    public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
    {
        int pixelCount = width * height;

        if (IsUnsigned)
        {
            PixelSimd.DecodeRgba8(source, destination, pixelCount);
            return;
        }

        for (int i = 0; i < pixelCount; i++)
            destination[i] = ReadPixel(source, i);
    }

    /// <inheritdoc/>
    public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
    {
        WritePixels(source[..(width * height)], destination, 0);
    }

    /// <inheritdoc/>
    public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
    {
        ReadOnlySpan<byte> pixel = source.Slice(pixelIndex * 4, 4);
        return new Vector4(ComponentEncoding.Decode8(pixel[0], _kind), ComponentEncoding.Decode8(pixel[1], _kind), ComponentEncoding.Decode8(pixel[2], _kind), ComponentEncoding.Decode8(pixel[3], _kind));
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        Span<byte> target = destination.Slice(pixelIndex * 4, 4);
        target[0] = ComponentEncoding.Encode8(pixel.X, _kind);
        target[1] = ComponentEncoding.Encode8(pixel.Y, _kind);
        target[2] = ComponentEncoding.Encode8(pixel.Z, _kind);
        target[3] = ComponentEncoding.Encode8(pixel.W, _kind);
    }

    /// <inheritdoc/>
    public void WritePixels(ReadOnlySpan<Vector4> pixels, Span<byte> destination, int startPixelIndex)
    {
        if (IsUnsigned)
        {
            PixelSimd.EncodeToRgba8(pixels, destination[(startPixelIndex * 4)..], pixels.Length);
            return;
        }

        for (int i = 0; i < pixels.Length; i++)
            WritePixel(pixels[i], destination, startPixelIndex + i);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        int pixelCount = width * height;

        if (sourceCodec is R8G8B8A8Codec other && other._kind == _kind)
        {
            source[..(pixelCount * 4)].CopyTo(destination);
            return;
        }

        if (sourceCodec is B8G8R8A8Codec && IsUnsigned)
        {
            PixelSimd.SwizzleRedBlue(source, destination, pixelCount);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }
}
