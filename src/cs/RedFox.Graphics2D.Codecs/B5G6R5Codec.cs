using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.B5G6R5Unorm"/>.
    /// 16-bit packed format: 5 bits blue, 6 bits green, 5 bits red.
    /// </summary>
    public sealed class B5G6R5Codec : IPixelCodec
    {
        private const float Inv31 = 1.0f / 31.0f;
        private const float Inv63 = 1.0f / 63.0f;

        /// <inheritdoc/>
        public ImageFormat Format => ImageFormat.B5G6R5Unorm;

        /// <inheritdoc/>
        public int BytesPerPixel => 2;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                ushort packed = ushorts[i];
                destination[i] = new Vector4(
                    ((packed >> 11) & 0x1F) * Inv31,
                    ((packed >> 5) & 0x3F) * Inv63,
                    (packed & 0x1F) * Inv31,
                    1f);
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
                ushort b = (ushort)(Math.Clamp(p.X, 0f, 1f) * 31f + 0.5f);
                ushort g = (ushort)(Math.Clamp(p.Y, 0f, 1f) * 63f + 0.5f);
                ushort r = (ushort)(Math.Clamp(p.Z, 0f, 1f) * 31f + 0.5f);
                ushorts[i] = (ushort)((b << 11) | (g << 5) | r);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            ushort packed = ushorts[pixelIndex];
            return new Vector4(
                ((packed >> 11) & 0x1F) * Inv31,
                ((packed >> 5) & 0x3F) * Inv63,
                (packed & 0x1F) * Inv31,
                1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(destination);
            ushort b = (ushort)(Math.Clamp(pixel.X, 0f, 1f) * 31f + 0.5f);
            ushort g = (ushort)(Math.Clamp(pixel.Y, 0f, 1f) * 63f + 0.5f);
            ushort r = (ushort)(Math.Clamp(pixel.Z, 0f, 1f) * 31f + 0.5f);
            ushorts[pixelIndex] = (ushort)((b << 11) | (g << 5) | r);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is B5G6R5Codec)
            {
                int byteCount = width * height * 2;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
