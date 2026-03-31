using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R32G32B32Typeless"/>, <see cref="ImageFormat.R32G32B32Float"/>, 
    /// <see cref="ImageFormat.R32G32B32Uint"/>, and <see cref="ImageFormat.R32G32B32Sint"/>.
    /// Interprets the 3×32-bit values as floats for conversion purposes.
    /// </summary>
    public sealed class R32G32B32FloatCodec : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 12;

        /// <summary>
        /// Initializes a new instance of the <see cref="R32G32B32FloatCodec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R32G32B32FloatCodec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R32G32B32Typeless => format,
                ImageFormat.R32G32B32Float => format,
                ImageFormat.R32G32B32Uint => format,
                ImageFormat.R32G32B32Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R32G32B32FloatCodec supports only R32G32B32Typeless, R32G32B32Float, R32G32B32Uint, and R32G32B32Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var floats = MemoryMarshal.Cast<byte, float>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                int o = i * 3;
                destination[i] = new Vector4(floats[o], floats[o + 1], floats[o + 2], 1f);
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
                int o = i * 3;
                floats[o] = p.X;
                floats[o + 1] = p.Y;
                floats[o + 2] = p.Z;
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var floats = MemoryMarshal.Cast<byte, float>(source);
            int o = pixelIndex * 3;
            return new Vector4(floats[o], floats[o + 1], floats[o + 2], 1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var floats = MemoryMarshal.Cast<byte, float>(destination);
            int o = pixelIndex * 3;
            floats[o] = pixel.X;
            floats[o + 1] = pixel.Y;
            floats[o + 2] = pixel.Z;
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R32G32B32FloatCodec)
            {
                int byteCount = width * height * 12;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
