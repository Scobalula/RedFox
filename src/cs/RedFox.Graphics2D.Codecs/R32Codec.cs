using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R32Typeless"/>, <see cref="ImageFormat.D32Float"/>,
    /// <see cref="ImageFormat.R32Float"/>, <see cref="ImageFormat.R32Uint"/>, and <see cref="ImageFormat.R32Sint"/>.
    /// Interprets the 32-bit value as float for conversion purposes.
    /// </summary>
    public sealed class R32Codec : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 4;

        /// <summary>
        /// Initializes a new instance of the <see cref="R32Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R32Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R32Typeless => format,
                ImageFormat.D32Float => format,
                ImageFormat.R32Float => format,
                ImageFormat.R32Uint => format,
                ImageFormat.R32Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R32Codec supports only R32Typeless, D32Float, R32Float, R32Uint, and R32Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var floats = MemoryMarshal.Cast<byte, float>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                destination[i] = new Vector4(floats[i], 0f, 0f, 1f);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            var floats = MemoryMarshal.Cast<byte, float>(destination);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                floats[i] = source[i].X;
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var floats = MemoryMarshal.Cast<byte, float>(source);
            return new Vector4(floats[pixelIndex], 0f, 0f, 1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var floats = MemoryMarshal.Cast<byte, float>(destination);
            floats[pixelIndex] = pixel.X;
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R32Codec)
            {
                int byteCount = width * height * 4;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
