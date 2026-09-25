using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Processing;

internal sealed class MipmapEncoding(ImageFormat format, MipmapMode mode)
{
    private readonly bool _isNormalMap = mode == MipmapMode.NormalMap;

    private readonly bool _isSrgb = mode == MipmapMode.Standard && ImageFormatInfo.IsSrgb(format);

    private readonly bool _isSigned = IsSignedFormat(format);

    private readonly bool _isTwoChannel = IsTwoChannelFormat(format);

    public Vector4[] Expand(Vector4[] pixels)
    {
        if (_isSrgb)
            ImageResampler.SrgbToLinear(pixels);

        if (!_isNormalMap)
            return pixels;

        foreach (ref Vector4 pixel in pixels.AsSpan())
        {
            Vector3 normal = _isSigned ? new Vector3(pixel.X, pixel.Y, pixel.Z) : new Vector3(pixel.X, pixel.Y, pixel.Z) * 2f - Vector3.One;

            if (_isTwoChannel)
                normal.Z = MathF.Sqrt(MathF.Max(0f, 1f - normal.X * normal.X - normal.Y * normal.Y));

            pixel = new Vector4(normal, pixel.W);
        }

        return Normalize(pixels);
    }

    public Vector4[] Normalize(Vector4[] pixels)
    {
        if (!_isNormalMap)
            return pixels;

        foreach (ref Vector4 pixel in pixels.AsSpan())
        {
            Vector3 normal = new(pixel.X, pixel.Y, pixel.Z);
            float length = normal.Length();
            normal = length > 1e-6f ? normal / length : Vector3.UnitZ;
            pixel = new Vector4(normal, pixel.W);
        }

        return pixels;
    }

    public Vector4[] Pack(Vector4[] pixels)
    {
        if (_isSrgb)
        {
            Vector4[] encoded = (Vector4[])pixels.Clone();
            ImageResampler.LinearToSrgb(encoded);
            return encoded;
        }

        if (!_isNormalMap || _isSigned)
            return pixels;

        Vector4[] packed = new Vector4[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
            packed[i] = new Vector4(new Vector3(pixels[i].X, pixels[i].Y, pixels[i].Z) * 0.5f + new Vector3(0.5f), pixels[i].W);

        return packed;
    }

    private static bool IsSignedFormat(ImageFormat format) => format switch
    {
        ImageFormat.R8Snorm or
        ImageFormat.R8G8Snorm or
        ImageFormat.R16Snorm or
        ImageFormat.R16G16Snorm or
        ImageFormat.R16G16B16A16Snorm or
        ImageFormat.BC4Snorm or
        ImageFormat.BC5Snorm or
        ImageFormat.BC6HSF16 or
        ImageFormat.R32G32B32A32Float or
        ImageFormat.R32G32B32Float or
        ImageFormat.R16G16B16A16Float or
        ImageFormat.R32G32Float or
        ImageFormat.R16G16Float => true,
        _ => false,
    };

    private static bool IsTwoChannelFormat(ImageFormat format) => format switch
    {
        ImageFormat.BC5Typeless or
        ImageFormat.BC5Unorm or
        ImageFormat.BC5Snorm or
        ImageFormat.R8G8Typeless or
        ImageFormat.R8G8Unorm or
        ImageFormat.R8G8Snorm or
        ImageFormat.R16G16Typeless or
        ImageFormat.R16G16Unorm or
        ImageFormat.R16G16Snorm or
        ImageFormat.R16G16Float or
        ImageFormat.R32G32Float => true,
        _ => false,
    };
}
