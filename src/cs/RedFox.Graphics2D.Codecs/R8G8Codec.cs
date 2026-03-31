using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R8G8Typeless"/>, <see cref="ImageFormat.R8G8Unorm"/>,
    /// <see cref="ImageFormat.R8G8Uint"/>, <see cref="ImageFormat.R8G8Snorm"/>, and <see cref="ImageFormat.R8G8Sint"/>.
    /// Interprets values as unsigned normalized [0,1] for conversion purposes.
    /// </summary>
    public sealed class R8G8Codec : IPixelCodec
    {
        private const float Inv255 = 1.0f / 255.0f;

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="R8G8Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R8G8Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R8G8Typeless => format,
                ImageFormat.R8G8Unorm => format,
                ImageFormat.R8G8Uint => format,
                ImageFormat.R8G8Snorm => format,
                ImageFormat.R8G8Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R8G8Codec supports only R8G8Typeless, R8G8Unorm, R8G8Uint, R8G8Snorm, and R8G8Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                int o = i * 2;
                destination[i] = new Vector4(
                    source[o] * Inv255,
                    source[o + 1] * Inv255,
                    0f,
                    1f);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                var p = source[i];
                int o = i * 2;
                destination[o] = (byte)(Math.Clamp(p.X, 0f, 1f) * 255f + 0.5f);
                destination[o + 1] = (byte)(Math.Clamp(p.Y, 0f, 1f) * 255f + 0.5f);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            int o = pixelIndex * 2;
            return new Vector4(
                source[o] * Inv255,
                source[o + 1] * Inv255,
                0f,
                1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            int o = pixelIndex * 2;
            destination[o] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
            destination[o + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R8G8Codec)
            {
                int byteCount = width * height * 2;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
