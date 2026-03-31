using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R8Typeless"/>, <see cref="ImageFormat.R8Unorm"/>,
    /// <see cref="ImageFormat.R8Uint"/>, <see cref="ImageFormat.R8Snorm"/>, and <see cref="ImageFormat.R8Sint"/>.
    /// Interprets values as unsigned normalized [0,1] for conversion purposes.
    /// </summary>
    public sealed class R8Codec : IPixelCodec
    {
        private const float Inv255 = 1.0f / 255.0f;

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="R8Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R8Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R8Typeless => format,
                ImageFormat.R8Unorm => format,
                ImageFormat.R8Uint => format,
                ImageFormat.R8Snorm => format,
                ImageFormat.R8Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R8Codec supports only R8Typeless, R8Unorm, R8Uint, R8Snorm, and R8Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                destination[i] = new Vector4(source[i] * Inv255, 0f, 0f, 1f);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                destination[i] = (byte)(Math.Clamp(source[i].X, 0f, 1f) * 255f + 0.5f);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            return new Vector4(source[pixelIndex] * Inv255, 0f, 0f, 1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            destination[pixelIndex] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R8Codec)
            {
                int byteCount = width * height;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
