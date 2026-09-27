using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

/// <summary>
/// Codec for <see cref="ImageFormat.R11G11B10Float"/>.
/// Uses IEEE 754 half-precision float decoding for R and G channels,
/// and a 10-bit float approximation for the B channel.
/// </summary>
public sealed class R11G11B10FloatCodec : IPixelCodec
{
    /// <inheritdoc/>
    public ImageFormat Format => ImageFormat.R11G11B10Float;

    /// <inheritdoc/>
    public int BytesPerPixel => 4;

    /// <inheritdoc/>
    public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(source);
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
        {
            uint packed = uints[i];
            destination[i] = new Vector4(DecodeFloat11((packed >> 0) & 0x7FF), DecodeFloat11((packed >> 11) & 0x7FF), DecodeFloat10((packed >> 22) & 0x3FF), 1f);
        }
    }

    /// <inheritdoc/>
    public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(destination);
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
        {
            var p = source[i];
            uint r = EncodeFloat11(p.X);
            uint g = EncodeFloat11(p.Y);
            uint b = EncodeFloat10(p.Z);
            uints[i] = (r & 0x7FF) | ((g & 0x7FF) << 11) | ((b & 0x3FF) << 22);
        }
    }

    /// <inheritdoc/>
    public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(source);
        uint packed = uints[pixelIndex];
        return new Vector4(DecodeFloat11((packed >> 0) & 0x7FF), DecodeFloat11((packed >> 11) & 0x7FF), DecodeFloat10((packed >> 22) & 0x3FF), 1f);
    }

    /// <inheritdoc/>
    public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(destination);
        uint r = EncodeFloat11(pixel.X);
        uint g = EncodeFloat11(pixel.Y);
        uint b = EncodeFloat10(pixel.Z);
        uints[pixelIndex] = (r & 0x7FF) | ((g & 0x7FF) << 11) | ((b & 0x3FF) << 22);
    }

    /// <inheritdoc/>
    public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
    {
        if (sourceCodec is R11G11B10FloatCodec)
        {
            int byteCount = width * height * 4;
            source[..byteCount].CopyTo(destination);
            return;
        }

        sourceCodec.DecodeTo(source, this, destination, width, height);
    }

    private static float DecodeFloat11(uint value)
    {
        return DecodeUnsignedFloat(value, 6);
    }

    private static float DecodeFloat10(uint value)
    {
        return DecodeUnsignedFloat(value, 5);
    }

    private static uint EncodeFloat11(float value)
    {
        return EncodeUnsignedFloat(value, 6);
    }

    private static uint EncodeFloat10(float value)
    {
        return EncodeUnsignedFloat(value, 5);
    }

    private static float DecodeUnsignedFloat(uint value, int mantissaBits)
    {
        uint mantissaMask = (1u << mantissaBits) - 1;
        uint exponent = value >> mantissaBits;
        uint mantissa = value & mantissaMask;
        if (exponent == 0)
            return MathF.ScaleB(mantissa, 1 - 15 - mantissaBits);
        if (exponent == 31)
            return mantissa == 0 ? float.PositiveInfinity : float.NaN;
        return MathF.ScaleB(1f + mantissa / (float)(1u << mantissaBits), (int)exponent - 15);
    }

    private static uint EncodeUnsignedFloat(float value, int mantissaBits)
    {
        int maxExponent = (1 << 5) - 1;
        uint mantissaMask = (1u << mantissaBits) - 1;
        if (float.IsNaN(value))
            return (uint)(maxExponent << mantissaBits) | 1;
        if (float.IsPositiveInfinity(value))
            return (uint)(maxExponent << mantissaBits);
        if (value <= 0f)
            return 0;

        int exponent = Math.ILogB(value);
        if (exponent < -14)
        {
            uint mantissa = (uint)MathF.Round(MathF.ScaleB(value, 14 + mantissaBits));
            return mantissa >= 1u << mantissaBits ? 1u << mantissaBits : mantissa;
        }

        if (exponent > 15)
            return (uint)((maxExponent - 1) << mantissaBits) | mantissaMask;

        float normalized = MathF.ScaleB(value, -exponent) - 1f;
        uint encodedMantissa = (uint)MathF.Round(normalized * (1u << mantissaBits));
        if (encodedMantissa > mantissaMask)
        {
            exponent++;
            encodedMantissa = 0;
        }
        if (exponent > 15)
            return (uint)((maxExponent - 1) << mantissaBits) | mantissaMask;

        return (uint)(exponent + 15) << mantissaBits | encodedMantissa;
    }
}
