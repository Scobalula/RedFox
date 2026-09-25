using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Processing;

/// <summary>
/// Provides filtered resizing and mip map generation.
/// Pixels are decoded through the format's codec, filtered as <see cref="Vector4"/> values, and encoded back into the source format.
/// </summary>
/// <remarks>
/// <para>sRGB formats are converted to linear light before filtering and back afterwards. Every other format is filtered on its stored values.</para>
/// <para>Filtering uses straight (non-premultiplied) alpha, so colour from fully transparent pixels can bleed into their neighbours.</para>
/// <para>Block-compressed formats are re-encoded after filtering, which is lossy.</para>
/// </remarks>
public static class ImageResampler
{
    /// <summary>
    /// Resizes the top mip level of every array element and depth slice. The depth is not resampled.
    /// The result has a single mip level.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <param name="filter">The resampling filter.</param>
    /// <returns>The resized image.</returns>
    public static Image Resize(this Image image, int width, int height, ResamplingFilter filter)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        bool isSrgb = ImageFormatInfo.IsSrgb(image.Format);
        Image result = new(image.Info with { Width = width, Height = height, MipLevels = 1 });

        foreach (ImageSlice target in result.Slices)
        {
            Vector4[] source = image.DecodeSlice(0, target.ArrayIndex, target.DepthIndex);

            if (isSrgb)
                SrgbToLinear(source);

            Vector4[] resized = Resample(source, image.Width, image.Height, width, height, filter);

            if (isSrgb)
                LinearToSrgb(resized);

            result.EncodeSlice(0, target.ArrayIndex, target.DepthIndex, resized);
        }

        return result;
    }

    /// <summary>
    /// Generates a full mip chain from the top mip level.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <returns>A new image with a full mip chain.</returns>
    public static Image GenerateMipmaps(this Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return GenerateMipmaps(image, ImageInfo.GetMaxMipLevels(image.Width, image.Height, image.Depth), MipmapMode.Standard);
    }

    /// <summary>
    /// Generates a full mip chain from the top mip level using the given mode.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="mode">How the image contents are filtered.</param>
    /// <returns>A new image with a full mip chain.</returns>
    public static Image GenerateMipmaps(this Image image, MipmapMode mode)
    {
        ArgumentNullException.ThrowIfNull(image);
        return GenerateMipmaps(image, ImageInfo.GetMaxMipLevels(image.Width, image.Height, image.Depth), mode);
    }

    /// <summary>
    /// Generates a mip chain with the given number of levels from the top mip level.
    /// The top level is copied unchanged; each further level is box-filtered from the previous level at full precision.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="mipLevels">The number of levels in the result, including the top level.</param>
    /// <param name="mode">How the image contents are filtered.</param>
    /// <returns>A new image with the requested mip chain.</returns>
    public static Image GenerateMipmaps(this Image image, int mipLevels, MipmapMode mode)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(mipLevels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mipLevels, ImageInfo.GetMaxMipLevels(image.Width, image.Height, image.Depth));

        Image result = new(image.Info with { MipLevels = mipLevels });
        MipmapEncoding encoding = new(image.Format, mode);

        for (int arrayIndex = 0; arrayIndex < image.ArraySize; arrayIndex++)
        {
            Vector4[][] level = new Vector4[image.Depth][];

            for (int depthIndex = 0; depthIndex < image.Depth; depthIndex++)
            {
                image.GetSlice(0, arrayIndex, depthIndex).PixelSpan.CopyTo(result.GetSlice(0, arrayIndex, depthIndex).PixelSpan);
                level[depthIndex] = encoding.Expand(image.DecodeSlice(0, arrayIndex, depthIndex));
            }

            for (int mip = 1; mip < mipLevels; mip++)
            {
                level = Downsample(level, Math.Max(1, image.Width >> (mip - 1)), Math.Max(1, image.Height >> (mip - 1)), Math.Max(1, image.Width >> mip), Math.Max(1, image.Height >> mip), encoding);

                for (int depthIndex = 0; depthIndex < level.Length; depthIndex++)
                    result.EncodeSlice(mip, arrayIndex, depthIndex, encoding.Pack(level[depthIndex]));
            }
        }

        return result;
    }

    internal static void SrgbToLinear(Span<Vector4> pixels)
    {
        foreach (ref Vector4 pixel in pixels)
            pixel = new Vector4(SrgbToLinear(pixel.X), SrgbToLinear(pixel.Y), SrgbToLinear(pixel.Z), pixel.W);
    }

    internal static void LinearToSrgb(Span<Vector4> pixels)
    {
        foreach (ref Vector4 pixel in pixels)
            pixel = new Vector4(LinearToSrgb(pixel.X), LinearToSrgb(pixel.Y), LinearToSrgb(pixel.Z), pixel.W);
    }

    private static Vector4[][] Downsample(Vector4[][] level, int sourceWidth, int sourceHeight, int width, int height, MipmapEncoding encoding)
    {
        Vector4[][] next = new Vector4[Math.Max(1, level.Length >> 1)][];

        for (int depthIndex = 0; depthIndex < next.Length; depthIndex++)
        {
            Vector4[] front = Resample(level[Math.Min(depthIndex * 2, level.Length - 1)], sourceWidth, sourceHeight, width, height, ResamplingFilter.Box);
            int backIndex = depthIndex * 2 + 1;

            if (backIndex < level.Length)
            {
                Vector4[] back = Resample(level[backIndex], sourceWidth, sourceHeight, width, height, ResamplingFilter.Box);

                for (int i = 0; i < front.Length; i++)
                    front[i] = (front[i] + back[i]) * 0.5f;
            }

            next[depthIndex] = encoding.Normalize(front);
        }

        return next;
    }

    private static Vector4[] Resample(ReadOnlySpan<Vector4> source, int sourceWidth, int sourceHeight, int width, int height, ResamplingFilter filter)
    {
        Vector4[] result = new Vector4[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[y * width + x] = filter switch
                {
                    ResamplingFilter.NearestNeighbor => SampleNearest(source, sourceWidth, sourceHeight, width, height, x, y),
                    ResamplingFilter.Bilinear => SampleBilinear(source, sourceWidth, sourceHeight, width, height, x, y),
                    ResamplingFilter.Box => SampleBox(source, sourceWidth, sourceHeight, width, height, x, y),
                    _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown resampling filter."),
                };
            }
        }

        return result;
    }

    private static Vector4 SampleNearest(ReadOnlySpan<Vector4> source, int sourceWidth, int sourceHeight, int width, int height, int x, int y)
    {
        int sourceX = (int)((2L * x + 1) * sourceWidth / (2L * width));
        int sourceY = (int)((2L * y + 1) * sourceHeight / (2L * height));
        return source[sourceY * sourceWidth + sourceX];
    }

    private static Vector4 SampleBilinear(ReadOnlySpan<Vector4> source, int sourceWidth, int sourceHeight, int width, int height, int x, int y)
    {
        float sourceX = Math.Max(0f, (x + 0.5f) * sourceWidth / width - 0.5f);
        float sourceY = Math.Max(0f, (y + 0.5f) * sourceHeight / height - 0.5f);
        int x0 = Math.Min((int)sourceX, sourceWidth - 1);
        int y0 = Math.Min((int)sourceY, sourceHeight - 1);
        int x1 = Math.Min(x0 + 1, sourceWidth - 1);
        int y1 = Math.Min(y0 + 1, sourceHeight - 1);
        float tx = sourceX - x0;
        float ty = sourceY - y0;

        Vector4 top = Vector4.Lerp(source[y0 * sourceWidth + x0], source[y0 * sourceWidth + x1], tx);
        Vector4 bottom = Vector4.Lerp(source[y1 * sourceWidth + x0], source[y1 * sourceWidth + x1], tx);
        return Vector4.Lerp(top, bottom, ty);
    }

    private static Vector4 SampleBox(ReadOnlySpan<Vector4> source, int sourceWidth, int sourceHeight, int width, int height, int x, int y)
    {
        int x0 = (int)((long)x * sourceWidth / width);
        int y0 = (int)((long)y * sourceHeight / height);
        int x1 = (int)(((long)(x + 1) * sourceWidth + width - 1) / width);
        int y1 = (int)(((long)(y + 1) * sourceHeight + height - 1) / height);
        Vector4 sum = Vector4.Zero;

        for (int sourceY = y0; sourceY < y1; sourceY++)
        {
            for (int sourceX = x0; sourceX < x1; sourceX++)
                sum += source[sourceY * sourceWidth + sourceX];
        }

        return sum / ((x1 - x0) * (y1 - y0));
    }

    private static float SrgbToLinear(float value)
    {
        return value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }

    private static float LinearToSrgb(float value)
    {
        return value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
    }
}
