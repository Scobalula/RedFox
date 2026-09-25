using System;
using System.IO;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging;

/// <summary>
/// Describes the dimensions, format, and subresource layout of an <see cref="Image"/> without holding any pixel data.
/// Returned by metadata probes so a container can be inspected before it is decoded.
/// </summary>
/// <param name="Width">The width of the top-level image in pixels.</param>
/// <param name="Height">The height of the top-level image in pixels.</param>
/// <param name="Depth">The depth of the top-level image in pixels (1 for 2D textures).</param>
/// <param name="ArraySize">The number of array elements, including every cube face.</param>
/// <param name="MipLevels">The number of mip levels.</param>
/// <param name="Format">The pixel format.</param>
/// <param name="IsCubemap">Whether the array elements form cube maps.</param>
public readonly record struct ImageInfo(int Width, int Height, int Depth, int ArraySize, int MipLevels, ImageFormat Format, bool IsCubemap)
{
    /// <summary>
    /// Initializes a new <see cref="ImageInfo"/> for a single 2D image with no mip maps.
    /// </summary>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="format">The pixel format.</param>
    public ImageInfo(int width, int height, ImageFormat format) : this(width, height, 1, 1, 1, format, false)
    {
    }

    /// <summary>
    /// Gets the maximum number of mip levels a full chain can contain for the given dimensions.
    /// </summary>
    /// <param name="width">The top-level width in pixels.</param>
    /// <param name="height">The top-level height in pixels.</param>
    /// <param name="depth">The top-level depth in pixels.</param>
    /// <returns>The number of levels down to and including 1x1x1.</returns>
    public static int GetMaxMipLevels(int width, int height, int depth)
    {
        int largest = Math.Max(Math.Max(width, height), Math.Max(depth, 1));
        return BitOperations.Log2((uint)largest) + 1;
    }

    /// <summary>
    /// Calculates the number of 2D slices across all array elements, mip levels, and depth slices.
    /// </summary>
    /// <returns>The total slice count.</returns>
    public int CalculateSliceCount()
    {
        int perArrayElement = 0;

        for (int mip = 0; mip < MipLevels; mip++)
            perArrayElement += Math.Max(1, Depth >> mip);

        return checked(perArrayElement * ArraySize);
    }

    /// <summary>
    /// Calculates the number of bytes required to store every subresource described by this layout.
    /// </summary>
    /// <returns>The total byte count.</returns>
    /// <exception cref="NotSupportedException">Thrown when <see cref="Format"/> has no defined storage size.</exception>
    public long CalculateTotalByteCount()
    {
        long perArrayElement = 0;

        for (int mip = 0; mip < MipLevels; mip++)
        {
            (long rowPitch, long rowCount) = GetMipStorage(mip);
            perArrayElement = checked(perArrayElement + rowPitch * rowCount * Math.Max(1, Depth >> mip));
        }

        return checked(perArrayElement * ArraySize);
    }

    /// <summary>
    /// Validates the layout and throws when it cannot describe a valid in-memory image.
    /// Intended for metadata read from untrusted containers.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the layout is invalid or too large to allocate.</exception>
    /// <exception cref="NotSupportedException">Thrown when <see cref="Format"/> has no defined storage size.</exception>
    public void Validate()
    {
        if (!HasDefinedStorage(Format))
            throw new NotSupportedException($"Image format {Format} has no defined storage size.");

        string? error = GetValidationError();

        if (error is not null)
            throw new InvalidDataException(error);
    }

    internal string? GetValidationError()
    {
        if (Width < 1 || Height < 1 || Depth < 1)
            return $"Image dimensions must be positive, but were {Width}x{Height}x{Depth}.";
        if (ArraySize < 1)
            return $"Image array size must be positive, but was {ArraySize}.";
        if (MipLevels < 1 || MipLevels > GetMaxMipLevels(Width, Height, Depth))
            return $"Image mip level count {MipLevels} is invalid for {Width}x{Height}x{Depth}.";
        if (IsCubemap && ArraySize % 6 != 0)
            return $"Cube map array size must be a multiple of 6, but was {ArraySize}.";
        if (IsCubemap && Depth != 1)
            return "Cube maps must have a depth of 1.";
        if (!HasDefinedStorage(Format))
            return $"Image format {Format} has no defined storage size.";

        if ((long)Depth * ArraySize > int.MaxValue || !TryCalculateTotalByteCount(out long totalBytes) || totalBytes > Array.MaxLength)
            return $"Image {Width}x{Height}x{Depth} with {ArraySize} element(s) and {MipLevels} mip(s) in {Format} exceeds the maximum buffer size.";

        return null;
    }

    private static bool HasDefinedStorage(ImageFormat format)
    {
        return ImageFormatInfo.IsBlockCompressed(format) || ImageFormatInfo.TryGetBitsPerPixel(format, out _);
    }

    private bool TryCalculateTotalByteCount(out long totalBytes)
    {
        try
        {
            totalBytes = CalculateTotalByteCount();
            return true;
        }
        catch (OverflowException)
        {
            totalBytes = 0;
            return false;
        }
    }

    private (long RowPitch, long RowCount) GetMipStorage(int mip)
    {
        long mipWidth = Math.Max(1, Width >> mip);
        long mipHeight = Math.Max(1, Height >> mip);

        if (ImageFormatInfo.IsBlockCompressed(Format))
            return ((mipWidth + 3) / 4 * ImageFormatInfo.GetBlockSize(Format), (mipHeight + 3) / 4);

        return ((mipWidth * ImageFormatInfo.GetBitsPerPixel(Format) + 7) / 8, mipHeight);
    }
}
