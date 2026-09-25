using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging;

/// <summary>
/// A non-owning view of a single 2D surface within an <see cref="Image"/>, identified by its mip level, array element, and depth slice.
/// The view aliases the image's pixel buffer; it becomes stale (still pointing at the old buffer) once the image is converted.
/// </summary>
/// <param name="width">The width of the slice, in pixels.</param>
/// <param name="height">The height of the slice, in pixels.</param>
/// <param name="format">The pixel format of the slice data.</param>
/// <param name="rowPitch">The number of bytes in one row of pixels, or one row of blocks for block-compressed formats.</param>
/// <param name="slicePitch">The total number of bytes in the slice.</param>
/// <param name="pixels">The memory that contains the slice pixel data.</param>
/// <param name="mipLevel">The mip level this slice belongs to.</param>
/// <param name="arrayIndex">The array element (or cube face) this slice belongs to.</param>
/// <param name="depthIndex">The depth slice index within the mip level.</param>
public readonly struct ImageSlice(int width, int height, ImageFormat format, int rowPitch, int slicePitch, Memory<byte> pixels, int mipLevel, int arrayIndex, int depthIndex)
{
    /// <summary>
    /// Gets the width of this slice in pixels.
    /// </summary>
    public int Width { get; } = width;

    /// <summary>
    /// Gets the height of this slice in pixels.
    /// </summary>
    public int Height { get; } = height;

    /// <summary>
    /// Gets the pixel format of this slice.
    /// </summary>
    public ImageFormat Format { get; } = format;

    /// <summary>
    /// Gets the number of bytes per row of pixels, or per row of 4x4 blocks for block-compressed formats.
    /// </summary>
    public int RowPitch { get; } = rowPitch;

    /// <summary>
    /// Gets the total number of bytes for this slice.
    /// </summary>
    public int SlicePitch { get; } = slicePitch;

    /// <summary>
    /// Gets the mip level this slice belongs to.
    /// </summary>
    public int MipLevel { get; } = mipLevel;

    /// <summary>
    /// Gets the array element (or cube face) this slice belongs to.
    /// </summary>
    public int ArrayIndex { get; } = arrayIndex;

    /// <summary>
    /// Gets the depth slice index within the mip level.
    /// </summary>
    public int DepthIndex { get; } = depthIndex;

    /// <summary>
    /// Gets whether this slice stores 4x4 compressed blocks rather than linear pixels.
    /// </summary>
    public bool IsBlockCompressed => ImageFormatInfo.IsBlockCompressed(Format);

    /// <summary>
    /// Gets the pixel data for this slice as a <see cref="Memory{T}"/>.
    /// </summary>
    public Memory<byte> Pixels { get; } = pixels;

    /// <summary>
    /// Gets the pixel data for this slice as a <see cref="Span{T}"/>.
    /// </summary>
    public Span<byte> PixelSpan => Pixels.Span;

    /// <summary>
    /// Gets the raw bytes of a single pixel row.
    /// </summary>
    /// <param name="y">The zero-based row index.</param>
    /// <returns>A span over exactly one row of pixel bytes.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the format is block-compressed or not byte-aligned.</exception>
    public Span<byte> GetRowSpan(int y)
    {
        int bytesPerPixel = GetRequiredBytesPerPixel();
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        return PixelSpan.Slice(y * RowPitch, Width * bytesPerPixel);
    }

    /// <summary>
    /// Gets a single pixel row typed as <typeparamref name="TPixel"/>.
    /// The pixel type must match the size of one pixel in <see cref="Format"/>; no conversion is performed.
    /// </summary>
    /// <typeparam name="TPixel">An unmanaged type whose size equals the format's bytes per pixel.</typeparam>
    /// <param name="y">The zero-based row index.</param>
    /// <returns>A span with one <typeparamref name="TPixel"/> per pixel in the row.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the format is block-compressed, not byte-aligned, or <typeparamref name="TPixel"/> has the wrong size.</exception>
    public Span<TPixel> GetPixelRowSpan<TPixel>(int y) where TPixel : unmanaged
    {
        int bytesPerPixel = GetRequiredBytesPerPixel();

        if (Unsafe.SizeOf<TPixel>() != bytesPerPixel)
            throw new InvalidOperationException($"{typeof(TPixel).Name} is {Unsafe.SizeOf<TPixel>()} bytes but {Format} pixels are {bytesPerPixel} bytes. Convert the image explicitly instead.");

        return MemoryMarshal.Cast<byte, TPixel>(GetRowSpan(y));
    }

    /// <summary>
    /// Gets the raw bytes of a single row of 4x4 blocks for a block-compressed slice.
    /// </summary>
    /// <param name="blockY">The zero-based block row index.</param>
    /// <returns>A span over exactly one row of compressed blocks.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the format is not block-compressed.</exception>
    public Span<byte> GetBlockRowSpan(int blockY)
    {
        if (!IsBlockCompressed)
            throw new InvalidOperationException($"{Format} is not block-compressed. Use {nameof(GetRowSpan)} instead.");

        ArgumentOutOfRangeException.ThrowIfNegative(blockY);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(blockY, SlicePitch / RowPitch);

        return PixelSpan.Slice(blockY * RowPitch, RowPitch);
    }

    /// <summary>
    /// Reinterprets the entire raw slice buffer as <typeparamref name="T"/> values.
    /// This is an explicit, unchecked reinterpretation of the stored bytes and performs no format conversion.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type used to reinterpret the pixel data.</typeparam>
    /// <returns>A span over the pixel data as <typeparamref name="T"/> values.</returns>
    public Span<T> GetPixelsAs<T>() where T : unmanaged
    {
        return MemoryMarshal.Cast<byte, T>(Pixels.Span);
    }

    /// <summary>
    /// Decodes the pixel at the specified coordinates through the format's codec.
    /// Intended for inspection; decode whole slices or rows for bulk access.
    /// </summary>
    /// <param name="x">The zero-based horizontal coordinate of the pixel.</param>
    /// <param name="y">The zero-based vertical coordinate of the pixel.</param>
    /// <returns>The decoded pixel as RGBA values.</returns>
    public Vector4 GetPixel(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        IPixelCodec codec = PixelCodecs.GetCodec(Format);
        return codec.ReadPixel(PixelSpan, x, y, Width);
    }

    private int GetRequiredBytesPerPixel()
    {
        int bytesPerPixel = ImageFormatInfo.GetBytesPerPixel(Format);

        if (bytesPerPixel == 0)
            throw new InvalidOperationException($"{Format} has no linear per-pixel byte layout. Use {nameof(GetBlockRowSpan)} for block-compressed data or decode through its codec.");

        return bytesPerPixel;
    }
}
