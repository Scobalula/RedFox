using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Processing;

/// <summary>
/// Provides channel swizzling, extraction, and combination across every subresource of an image.
/// Pixels are decoded through the format's codec and encoded into the result format; block-compressed results are re-encoded, which is lossy.
/// </summary>
public static class ImageChannels
{
    /// <summary>
    /// Rearranges the channels of every subresource. The result keeps the source format and layout.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="red">The source of the result's red channel.</param>
    /// <param name="green">The source of the result's green channel.</param>
    /// <param name="blue">The source of the result's blue channel.</param>
    /// <param name="alpha">The source of the result's alpha channel.</param>
    /// <returns>The swizzled image.</returns>
    public static Image Swizzle(this Image image, ColorChannel red, ColorChannel green, ColorChannel blue, ColorChannel alpha)
    {
        ArgumentNullException.ThrowIfNull(image);

        Image result = new(image.Info);

        foreach (ImageSlice slice in result.Slices)
        {
            Vector4[] pixels = image.DecodeSlice(slice.MipLevel, slice.ArrayIndex, slice.DepthIndex);

            foreach (ref Vector4 pixel in pixels.AsSpan())
                pixel = new Vector4(Select(pixel, red), Select(pixel, green), Select(pixel, blue), Select(pixel, alpha));

            result.EncodeSlice(slice.MipLevel, slice.ArrayIndex, slice.DepthIndex, pixels);
        }

        return result;
    }

    /// <summary>
    /// Extracts one channel of every subresource into a new image as greyscale (the value in red, green, and blue, with opaque alpha).
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="channel">The channel to extract.</param>
    /// <param name="format">The format of the result, typically a single-channel format such as <see cref="ImageFormat.R8Unorm"/>.</param>
    /// <returns>The extracted channel with the same layout as the source.</returns>
    public static Image ExtractChannel(this Image image, ColorChannel channel, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(image);

        Image result = new(image.Info with { Format = format });

        foreach (ImageSlice slice in result.Slices)
        {
            Vector4[] pixels = image.DecodeSlice(slice.MipLevel, slice.ArrayIndex, slice.DepthIndex);

            foreach (ref Vector4 pixel in pixels.AsSpan())
            {
                float value = Select(pixel, channel);
                pixel = new Vector4(value, value, value, 1f);
            }

            result.EncodeSlice(slice.MipLevel, slice.ArrayIndex, slice.DepthIndex, pixels);
        }

        return result;
    }

    /// <summary>
    /// Combines the red channel of up to four images into the channels of a new image.
    /// All supplied images must share the same layout. Missing colour channels are filled with 0 and a missing alpha channel with 1.
    /// </summary>
    /// <param name="red">The image whose red channel becomes the result's red channel, or <see langword="null"/>.</param>
    /// <param name="green">The image whose red channel becomes the result's green channel, or <see langword="null"/>.</param>
    /// <param name="blue">The image whose red channel becomes the result's blue channel, or <see langword="null"/>.</param>
    /// <param name="alpha">The image whose red channel becomes the result's alpha channel, or <see langword="null"/>.</param>
    /// <param name="format">The format of the result.</param>
    /// <returns>The combined image.</returns>
    public static Image CombineChannels(Image? red, Image? green, Image? blue, Image? alpha, ImageFormat format)
    {
        Image template = red ?? green ?? blue ?? alpha ?? throw new ArgumentException("At least one source image is required.");
        ImageInfo layout = template.Info;

        foreach (Image? source in (ReadOnlySpan<Image?>)[red, green, blue, alpha])
        {
            if (source is not null && source.Info with { Format = layout.Format } != layout)
                throw new ArgumentException("All source images must have the same dimensions, array size, mip levels, and cube map flag.");
        }

        Image result = new(layout with { Format = format });

        foreach (ImageSlice slice in result.Slices)
        {
            Vector4[] pixels = new Vector4[slice.Width * slice.Height];
            ReadOnlySpan<float> r = DecodeRed(red, slice, 0f);
            ReadOnlySpan<float> g = DecodeRed(green, slice, 0f);
            ReadOnlySpan<float> b = DecodeRed(blue, slice, 0f);
            ReadOnlySpan<float> a = DecodeRed(alpha, slice, 1f);

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Vector4(r[i], g[i], b[i], a[i]);

            result.EncodeSlice(slice.MipLevel, slice.ArrayIndex, slice.DepthIndex, pixels);
        }

        return result;
    }

    private static float[] DecodeRed(Image? source, in ImageSlice slice, float fallback)
    {
        float[] values = new float[slice.Width * slice.Height];

        if (source is null)
        {
            values.AsSpan().Fill(fallback);
            return values;
        }

        Vector4[] pixels = source.DecodeSlice(slice.MipLevel, slice.ArrayIndex, slice.DepthIndex);

        for (int i = 0; i < values.Length; i++)
            values[i] = pixels[i].X;

        return values;
    }

    private static float Select(Vector4 pixel, ColorChannel channel) => channel switch
    {
        ColorChannel.Red => pixel.X,
        ColorChannel.Green => pixel.Y,
        ColorChannel.Blue => pixel.Z,
        ColorChannel.Alpha => pixel.W,
        ColorChannel.Zero => 0f,
        ColorChannel.One => 1f,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown color channel."),
    };
}
