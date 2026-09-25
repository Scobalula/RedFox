using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Processing;

/// <summary>
/// Provides geometric operations that move pixels without filtering them.
/// Uncompressed formats are processed byte-for-byte and are lossless; block-compressed formats are decoded and re-encoded through their codecs, which is lossy.
/// Every operation returns a new <see cref="Image"/> in the source format and leaves the source untouched.
/// </summary>
public static class ImageTransforms
{
    /// <summary>
    /// Crops the top mip level of every array element and depth slice to the given rectangle.
    /// The result has a single mip level.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="x">The left edge of the rectangle in pixels.</param>
    /// <param name="y">The top edge of the rectangle in pixels.</param>
    /// <param name="width">The rectangle width in pixels.</param>
    /// <param name="height">The rectangle height in pixels.</param>
    /// <returns>The cropped image.</returns>
    public static Image Crop(this Image image, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, image.Width - x);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, image.Height - y);

        Image result = new(image.Info with { Width = width, Height = height, MipLevels = 1 });
        return Remap(image, result, (targetX, targetY, _, _) => (targetX + x, targetY + y));
    }

    /// <summary>
    /// Mirrors every subresource left to right. The mip chain is preserved.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <returns>The flipped image.</returns>
    public static Image FlipHorizontal(this Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Remap(image, new Image(image.Info), static (x, y, width, _) => (width - 1 - x, y));
    }

    /// <summary>
    /// Mirrors every subresource top to bottom. The mip chain is preserved.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <returns>The flipped image.</returns>
    public static Image FlipVertical(this Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Remap(image, new Image(image.Info), static (x, y, _, height) => (x, height - 1 - y));
    }

    /// <summary>
    /// Rotates every subresource 90 degrees clockwise, swapping width and height. The mip chain is preserved.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <returns>The rotated image.</returns>
    public static Image Rotate90(this Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        Image result = new(image.Info with { Width = image.Height, Height = image.Width });
        return Remap(image, result, static (x, y, _, height) => (y, height - 1 - x));
    }

    /// <summary>
    /// Rotates every subresource 180 degrees. The mip chain is preserved.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <returns>The rotated image.</returns>
    public static Image Rotate180(this Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Remap(image, new Image(image.Info), static (x, y, width, height) => (width - 1 - x, height - 1 - y));
    }

    /// <summary>
    /// Rotates every subresource 270 degrees clockwise (90 degrees counter-clockwise), swapping width and height. The mip chain is preserved.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <returns>The rotated image.</returns>
    public static Image Rotate270(this Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        Image result = new(image.Info with { Width = image.Height, Height = image.Width });
        return Remap(image, result, static (x, y, width, _) => (width - 1 - y, x));
    }

    private static Image Remap(Image source, Image destination, Func<int, int, int, int, (int X, int Y)> map)
    {
        int bytesPerPixel = ImageFormatInfo.GetBytesPerPixel(source.Format);

        foreach (ImageSlice target in destination.Slices)
        {
            ref readonly ImageSlice origin = ref source.GetSlice(target.MipLevel, target.ArrayIndex, target.DepthIndex);

            if (bytesPerPixel > 0)
            {
                RemapPixels<byte>(origin.PixelSpan, origin.Width, origin.Height, target.PixelSpan, target.Width, target.Height, bytesPerPixel, map);
                continue;
            }

            Vector4[] decoded = source.DecodeSlice(target.MipLevel, target.ArrayIndex, target.DepthIndex);
            Vector4[] remapped = new Vector4[target.Width * target.Height];
            RemapPixels<Vector4>(decoded, origin.Width, origin.Height, remapped, target.Width, target.Height, 1, map);
            destination.EncodeSlice(target.MipLevel, target.ArrayIndex, target.DepthIndex, remapped);
        }

        return destination;
    }

    private static void RemapPixels<T>(ReadOnlySpan<T> source, int sourceWidth, int sourceHeight, Span<T> destination, int width, int height, int elementsPerPixel, Func<int, int, int, int, (int X, int Y)> map)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (int sourceX, int sourceY) = map(x, y, sourceWidth, sourceHeight);
                source.Slice((sourceY * sourceWidth + sourceX) * elementsPerPixel, elementsPerPixel).CopyTo(destination.Slice((y * width + x) * elementsPerPixel, elementsPerPixel));
            }
        }
    }
}
