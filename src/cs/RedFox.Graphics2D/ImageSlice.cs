using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D
{
    /// <summary>
    /// Represents a single 2D sub-image within an <see cref="Image"/>,
    /// corresponding to a specific mip level, array element, or depth slice.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ImageSlice"/> struct.
    /// </remarks>
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
        public Span<T> GetPixelsAs<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(Pixels.Span);
        }

        /// <summary>
        /// Reads a single pixel at the given (x, y) coordinate and returns it as <see cref="Vector4"/>.
        /// Works for all formats including block-compressed.
        /// </summary>
        public Vector4 GetPixel(int x, int y)
        {
            IPixelCodec codec = PixelCodecRegistry.Default.GetCodec(Format);
            return codec.ReadPixel(PixelSpan, x, y, Width);
        }
    }
}
