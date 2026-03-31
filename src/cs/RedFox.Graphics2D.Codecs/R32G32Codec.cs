using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R32G32Typeless"/>, <see cref="ImageFormat.R32G32Float"/>,
    /// <see cref="ImageFormat.R32G32Uint"/>, and <see cref="ImageFormat.R32G32Sint"/>.
    /// Interprets the 2×32-bit values as floats for conversion purposes.
    /// </summary>
    public sealed class R32G32Codec : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 8;

        /// <summary>
        /// Initializes a new instance of the <see cref="R32G32Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R32G32Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R32G32Typeless => format,
                ImageFormat.R32G32Float => format,
                ImageFormat.R32G32Uint => format,
                ImageFormat.R32G32Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R32G32Codec supports only R32G32Typeless, R32G32Float, R32G32Uint, and R32G32Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var floats = MemoryMarshal.Cast<byte, float>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                int o = i * 2;
                destination[i] = new Vector4(floats[o], floats[o + 1], 0f, 1f);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            var floats = MemoryMarshal.Cast<byte, float>(destination);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                var p = source[i];
                int o = i * 2;
                floats[o] = p.X;
                floats[o + 1] = p.Y;
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var floats = MemoryMarshal.Cast<byte, float>(source);
            int o = pixelIndex * 2;
            return new Vector4(floats[o], floats[o + 1], 0f, 1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var floats = MemoryMarshal.Cast<byte, float>(destination);
            int o = pixelIndex * 2;
            floats[o] = pixel.X;
            floats[o + 1] = pixel.Y;
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R32G32Codec)
            {
                int byteCount = width * height * 8;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
