using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R10G10B10A2Typeless"/>, <see cref="ImageFormat.R10G10B10A2Unorm"/>,
    /// <see cref="ImageFormat.R10G10B10A2Uint"/>, and <see cref="ImageFormat.R10G10B10XrBiasA2Unorm"/>.
    /// Interprets values as unsigned normalized [0,1] for conversion purposes.
    /// </summary>
    public sealed class R10G10B10A2Codec : IPixelCodec
    {
        private const float Inv1023 = 1.0f / 1023.0f;
        private const float Inv3 = 1.0f / 3.0f;

        /// <inheritdoc/>
        public ImageFormat Format { get; }

        /// <inheritdoc/>
        public int BytesPerPixel => 4;

        /// <summary>
        /// Initializes a new instance of the <see cref="R10G10B10A2Codec"/> class for the specified format variant.
        /// </summary>
        /// <param name="format">The image format this codec handles.</param>
        public R10G10B10A2Codec(ImageFormat format)
        {
            Format = format switch
            {
                ImageFormat.R10G10B10A2Typeless => format,
                ImageFormat.R10G10B10A2Unorm => format,
                ImageFormat.R10G10B10A2Uint => format,
                ImageFormat.R10G10B10XrBiasA2Unorm => format,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "R10G10B10A2Codec supports only R10G10B10A2Typeless, R10G10B10A2Unorm, R10G10B10A2Uint, and R10G10B10XrBiasA2Unorm."),
            };
        }

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                uint packed = uints[i];
                destination[i] = new Vector4(
                    ((packed >> 0) & 0x3FF) * Inv1023,
                    ((packed >> 10) & 0x3FF) * Inv1023,
                    ((packed >> 20) & 0x3FF) * Inv1023,
                    ((packed >> 30) & 0x3) * Inv3);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(destination);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                var p = source[i];
                uint r = (uint)(Math.Clamp(p.X, 0f, 1f) * 1023f + 0.5f);
                uint g = (uint)(Math.Clamp(p.Y, 0f, 1f) * 1023f + 0.5f);
                uint b = (uint)(Math.Clamp(p.Z, 0f, 1f) * 1023f + 0.5f);
                uint a = (uint)(Math.Clamp(p.W, 0f, 1f) * 3f + 0.5f);
                uints[i] = (r & 0x3FF) | ((g & 0x3FF) << 10) | ((b & 0x3FF) << 20) | ((a & 0x3) << 30);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(source);
            uint packed = uints[pixelIndex];
            return new Vector4(
                ((packed >> 0) & 0x3FF) * Inv1023,
                ((packed >> 10) & 0x3FF) * Inv1023,
                ((packed >> 20) & 0x3FF) * Inv1023,
                ((packed >> 30) & 0x3) * Inv3);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(destination);
            uint r = (uint)(Math.Clamp(pixel.X, 0f, 1f) * 1023f + 0.5f);
            uint g = (uint)(Math.Clamp(pixel.Y, 0f, 1f) * 1023f + 0.5f);
            uint b = (uint)(Math.Clamp(pixel.Z, 0f, 1f) * 1023f + 0.5f);
            uint a = (uint)(Math.Clamp(pixel.W, 0f, 1f) * 3f + 0.5f);
            uints[pixelIndex] = (r & 0x3FF) | ((g & 0x3FF) << 10) | ((b & 0x3FF) << 20) | ((a & 0x3) << 30);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R10G10B10A2Codec)
            {
                int byteCount = width * height * 4;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
