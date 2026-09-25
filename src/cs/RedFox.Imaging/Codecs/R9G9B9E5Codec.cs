using System;
using System.Buffers.Binary;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R9G9B9E5SharedExp"/>: three unsigned 9-bit mantissas sharing a 5-bit exponent.
/// Decodes to non-negative floats with opaque alpha; negative values are clamped to 0 and values above 65408 saturate on encode.
/// </summary>
public sealed class R9G9B9E5Codec : IPixelCodec
{
    private const int MantissaBits = 9;

    private const int ExponentBias = 15;

    private const float MaxValue = 511f / 512f * 65536f;

    /// <inheritdoc/>
    public ImageFormat Format => ImageFormat.R9G9B9E5SharedExp;

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
        uint packed = BinaryPrimitives.ReadUInt32LittleEndian(source[(pixelIndex * 4)..]);
        float scale = MathF.ScaleB(1f, (int)(packed >> 27) - ExponentBias - MantissaBits);
        return new Vector4((packed & 0x1FF) * scale, ((packed >> 9) & 0x1FF) * scale, ((packed >> 18) & 0x1FF) * scale, 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        float red = Math.Clamp(pixel.X, 0f, MaxValue);
        float green = Math.Clamp(pixel.Y, 0f, MaxValue);
        float blue = Math.Clamp(pixel.Z, 0f, MaxValue);
        float largest = MathF.Max(red, MathF.Max(green, blue));

        int exponent = Math.Max(-ExponentBias - 1, largest > 0f ? (int)MathF.Floor(MathF.Log2(largest)) : int.MinValue) + 1 + ExponentBias;
        float denominator = MathF.ScaleB(1f, exponent - ExponentBias - MantissaBits);

        if ((int)MathF.Floor(largest / denominator + 0.5f) == 1 << MantissaBits)
        {
            denominator *= 2f;
            exponent++;
        }

        uint packed = Quantize(red, denominator) | (Quantize(green, denominator) << 9) | (Quantize(blue, denominator) << 18) | ((uint)exponent << 27);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[(pixelIndex * 4)..], packed);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R9G9B9E5Codec)
        {
            source[..(width * height * 4)].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }

    private static uint Quantize(float value, float denominator)
    {
        return (uint)Math.Min(511f, MathF.Floor(value / denominator + 0.5f));
    }
}
