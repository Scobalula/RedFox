using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R16G16B16A16Typeless"/>, <see cref="ImageFormat.R16G16B16A16Unorm"/>,
    /// <see cref="ImageFormat.R16G16B16A16Uint"/>, <see cref="ImageFormat.R16G16B16A16Snorm"/>, and <see cref="ImageFormat.R16G16B16A16Sint"/>.
    /// Interprets values as unsigned normalized [0,1] for conversion purposes.
    /// </summary>
    public sealed class R16G16B16A16Codec : IPixelCodec
    {
        private const float Inv65535 = 1.0f / 65535.0f;

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 8;

        /// <summary>
        /// Initializes a new instance of the <see cref="R16G16B16A16Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R16G16B16A16Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R16G16B16A16Typeless => format,
                ImageFormat.R16G16B16A16Unorm => format,
                ImageFormat.R16G16B16A16Uint => format,
                ImageFormat.R16G16B16A16Snorm => format,
                ImageFormat.R16G16B16A16Sint => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R16G16B16A16Codec supports only R16G16B16A16Typeless, R16G16B16A16Unorm, R16G16B16A16Uint, R16G16B16A16Snorm, and R16G16B16A16Sint."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                int o = i * 4;
                destination[i] = new Vector4(
                    ushorts[o] * Inv65535,
                    ushorts[o + 1] * Inv65535,
                    ushorts[o + 2] * Inv65535,
                    ushorts[o + 3] * Inv65535);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(destination);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                var p = source[i];
                int o = i * 4;
                ushorts[o] = (ushort)(Math.Clamp(p.X, 0f, 1f) * 65535f + 0.5f);
                ushorts[o + 1] = (ushort)(Math.Clamp(p.Y, 0f, 1f) * 65535f + 0.5f);
                ushorts[o + 2] = (ushort)(Math.Clamp(p.Z, 0f, 1f) * 65535f + 0.5f);
                ushorts[o + 3] = (ushort)(Math.Clamp(p.W, 0f, 1f) * 65535f + 0.5f);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            int o = pixelIndex * 4;
            return new Vector4(
                ushorts[o] * Inv65535,
                ushorts[o + 1] * Inv65535,
                ushorts[o + 2] * Inv65535,
                ushorts[o + 3] * Inv65535);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(destination);
            int o = pixelIndex * 4;
            ushorts[o] = (ushort)(Math.Clamp(pixel.X, 0f, 1f) * 65535f + 0.5f);
            ushorts[o + 1] = (ushort)(Math.Clamp(pixel.Y, 0f, 1f) * 65535f + 0.5f);
            ushorts[o + 2] = (ushort)(Math.Clamp(pixel.Z, 0f, 1f) * 65535f + 0.5f);
            ushorts[o + 3] = (ushort)(Math.Clamp(pixel.W, 0f, 1f) * 65535f + 0.5f);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R16G16B16A16Codec)
            {
                int byteCount = width * height * 8;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
