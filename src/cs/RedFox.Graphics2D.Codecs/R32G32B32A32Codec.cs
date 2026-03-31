using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R32G32B32A32Typeless"/>, <see cref="ImageFormat.R32G32B32A32Uint"/>, and <see cref="ImageFormat.R32G32B32A32Sint"/>.
    /// Interprets the 4×32-bit values as floats for conversion purposes.
    /// </summary>
    public sealed class R32G32B32A32Codec : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 16;

        /// <summary>
        /// Initializes a new instance of the <see cref="R32G32B32A32Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R32G32B32A32Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R32G32B32A32Typeless => format,
                ImageFormat.R32G32B32A32Uint => format,
                ImageFormat.R32G32B32A32Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R32G32B32A32Codec supports only R32G32B32A32Typeless, R32G32B32A32Uint, and R32G32B32A32Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var sourceVectors = MemoryMarshal.Cast<byte, Vector4>(source);
            int pixelCount = width * height;
            sourceVectors[..pixelCount].CopyTo(destination);
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;
            var destVectors = MemoryMarshal.Cast<byte, Vector4>(destination);
            source[..pixelCount].CopyTo(destVectors);
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var vectors = MemoryMarshal.Cast<byte, Vector4>(source);
            return vectors[pixelIndex];
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var vectors = MemoryMarshal.Cast<byte, Vector4>(destination);
            vectors[pixelIndex] = pixel;
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R32G32B32A32Codec)
            {
                int byteCount = width * height * 16;
                source[..byteCount].CopyTo(destination);
                return;
            }

            var destVectors = MemoryMarshal.Cast<byte, Vector4>(destination);
            sourceCodec.Decode(source, destVectors[..(width * height)], width, height);
        }
    }
}
