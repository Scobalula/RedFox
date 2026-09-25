using System;
using System.Buffers.Binary;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R10G10B10A2Typeless"/>, <see cref="ImageFormat.R10G10B10A2Unorm"/>, <see cref="ImageFormat.R10G10B10A2Uint"/>, and <see cref="ImageFormat.R10G10B10XrBiasA2Unorm"/>.
/// Colour channels decode to [0, 1], except the extended-range XR bias variant which decodes to roughly [-0.75, 1.25]. Alpha always decodes to [0, 1].
/// </summary>
public sealed class R10G10B10A2Codec : IPixelCodec
{
    private const float XrBias = 384f;

    private const float XrScale = 510f;

    private readonly bool _isXrBias;

    /// <inheritdoc/>
    public ImageFormat Format { get; }

    /// <inheritdoc/>
    public int BytesPerPixel => 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="R10G10B10A2Codec"/> class for the specified format variant.
    /// </summary>
    /// <param name="format">The image format this codec handles.</param>
    public R10G10B10A2Codec(ImageFormat format)
    {
        Format = format switch
        {
            ImageFormat.R10G10B10A2Typeless or ImageFormat.R10G10B10A2Unorm or ImageFormat.R10G10B10A2Uint or ImageFormat.R10G10B10XrBiasA2Unorm => format,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R10G10B10A2Codec supports only R10G10B10A2Typeless, R10G10B10A2Unorm, R10G10B10A2Uint, and R10G10B10XrBiasA2Unorm."),
        };

        _isXrBias = format == ImageFormat.R10G10B10XrBiasA2Unorm;
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
        uint packed = BinaryPrimitives.ReadUInt32LittleEndian(source[(pixelIndex * 4)..]);
        return new Vector4(DecodeColor(packed & 0x3FF), DecodeColor((packed >> 10) & 0x3FF), DecodeColor((packed >> 20) & 0x3FF), (packed >> 30) / 3f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        uint alpha = (uint)(Math.Clamp(pixel.W, 0f, 1f) * 3f + 0.5f);
        uint packed = EncodeColor(pixel.X) | (EncodeColor(pixel.Y) << 10) | (EncodeColor(pixel.Z) << 20) | (alpha << 30);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[(pixelIndex * 4)..], packed);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R10G10B10A2Codec other && other._isXrBias == _isXrBias)
        {
            source[..(width * height * 4)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }

    private float DecodeColor(uint value)
    {
        return _isXrBias ? (value - XrBias) / XrScale : value / 1023f;
    }

    private uint EncodeColor(float value)
    {
        return _isXrBias ? (uint)Math.Clamp(MathF.Round(value * XrScale + XrBias), 0f, 1023f) : (uint)(Math.Clamp(value, 0f, 1f) * 1023f + 0.5f);
    }
}
