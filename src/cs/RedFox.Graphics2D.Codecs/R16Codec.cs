using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R16Typeless"/>, <see cref="ImageFormat.R16Float"/>,
    /// <see cref="ImageFormat.D16Unorm"/>, <see cref="ImageFormat.R16Unorm"/>, <see cref="ImageFormat.R16Uint"/>,
    /// <see cref="ImageFormat.R16Snorm"/>, and <see cref="ImageFormat.R16Sint"/>.
    /// Interprets values as unsigned normalized [0,1] for conversion purposes.
    /// </summary>
    public sealed class R16Codec : IPixelCodec
    {
        private const float Inv65535 = 1.0f / 65535.0f;

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="R16Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R16Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R16Typeless => format,
                ImageFormat.R16Float => format,
                ImageFormat.D16Unorm => format,
                ImageFormat.R16Unorm => format,
                ImageFormat.R16Uint => format,
                ImageFormat.R16Snorm => format,
                ImageFormat.R16Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R16Codec supports only R16Typeless, R16Float, D16Unorm, R16Unorm, R16Uint, R16Snorm, and R16Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                destination[i] = new Vector4(ushorts[i] * Inv65535, 0f, 0f, 1f);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(destination);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                ushorts[i] = (ushort)(Math.Clamp(source[i].X, 0f, 1f) * 65535f + 0.5f);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            return new Vector4(ushorts[pixelIndex] * Inv65535, 0f, 0f, 1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(destination);
            ushorts[pixelIndex] = (ushort)(Math.Clamp(pixel.X, 0f, 1f) * 65535f + 0.5f);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R16Codec)
            {
                int byteCount = width * height * 2;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
