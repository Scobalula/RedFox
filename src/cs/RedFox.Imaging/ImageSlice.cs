using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging
{
    /// <summary>
    /// Represents a single 2D sub-image within an <see cref="Image"/>,
    /// corresponding to a specific mip level, array element, or depth slice.
    /// </summary>
    /// <param name="width">The width of the slice, in pixels.</param>
    /// <param name="height">The height of the slice, in pixels.</param>
    /// <param name="format">The pixel format of the slice data.</param>
    /// <param name="rowPitch">The number of bytes in one row of pixel data.</param>
    /// <param name="slicePitch">The total number of bytes in the slice.</param>
    /// <param name="pixels">The memory that contains the slice pixel data.</param>
    public readonly struct ImageSlice(int width, int height, ImageFormat format, int rowPitch, int slicePitch, Memory<byte> pixels)
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
        /// Gets the number of bytes per row (scan line) of this slice.
        /// </summary>
        public int RowPitch { get; } = rowPitch;

        /// <summary>
        /// Gets the total number of bytes for this slice.
        /// </summary>
        public int SlicePitch { get; } = slicePitch;

        /// <summary>
        /// Gets the pixel data for this slice as a <see cref="Memory{T}"/>.
        /// </summary>
        public Memory<byte> Pixels { get; } = pixels;

        /// <summary>
        /// Gets the pixel data for this slice as a <see cref="Span{T}"/>.
        /// </summary>
        public Span<byte> PixelSpan => Pixels.Span;

        /// <summary>
        /// Gets a span over the raw pixel data reinterpreted as values of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type used to reinterpret the pixel data.</typeparam>
        /// <returns>A span over the pixel data as <typeparamref name="T"/> values.</returns>
        public Span<T> GetPixelsAs<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(Pixels.Span);
        }

        /// <summary>
        /// Gets the pixel at the specified coordinates as <see cref="Vector4"/>.
        /// </summary>
        /// <param name="x">The zero-based horizontal coordinate of the pixel.</param>
        /// <param name="y">The zero-based vertical coordinate of the pixel.</param>
        /// <returns>The decoded pixel as normalized RGBA values.</returns>
        public Vector4 GetPixel(int x, int y)
        {
            IPixelCodec codec = PixelCodecs.GetCodec(Format);
            return codec.ReadPixel(PixelSpan, x, y, Width);
        }
    }
}
