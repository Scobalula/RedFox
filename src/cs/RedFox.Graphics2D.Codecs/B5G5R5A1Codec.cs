using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.B5G5R5A1Unorm"/>.
    /// 16-bit packed format: 5 bits blue, 5 bits green, 5 bits red, 1 bit alpha.
    /// </summary>
    public sealed class B5G5R5A1Codec : IPixelCodec
    {
        private const float Inv31 = 1.0f / 31.0f;

        /// <inheritdoc/>
        public ImageFormat Format => ImageFormat.B5G5R5A1Unorm;

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
                    ((packed >> 10) & 0x1F) * Inv31,
                    ((packed >> 5) & 0x1F) * Inv31,
                    (packed & 0x1F) * Inv31,
                    ((packed >> 15) & 0x1) != 0 ? 1f : 0f);
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
                ushort g = (ushort)(Math.Clamp(p.Y, 0f, 1f) * 31f + 0.5f);
                ushort r = (ushort)(Math.Clamp(p.Z, 0f, 1f) * 31f + 0.5f);
                ushort a = p.W >= 0.5f ? (ushort)1 : (ushort)0;
                ushorts[i] = (ushort)((b << 10) | (g << 5) | r | (a << 15));
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            ushort packed = ushorts[pixelIndex];
            return new Vector4(
                ((packed >> 10) & 0x1F) * Inv31,
                ((packed >> 5) & 0x1F) * Inv31,
                (packed & 0x1F) * Inv31,
                ((packed >> 15) & 0x1) != 0 ? 1f : 0f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(destination);
            ushort b = (ushort)(Math.Clamp(pixel.X, 0f, 1f) * 31f + 0.5f);
            ushort g = (ushort)(Math.Clamp(pixel.Y, 0f, 1f) * 31f + 0.5f);
            ushort r = (ushort)(Math.Clamp(pixel.Z, 0f, 1f) * 31f + 0.5f);
            ushort a = pixel.W >= 0.5f ? (ushort)1 : (ushort)0;
            ushorts[pixelIndex] = (ushort)((b << 10) | (g << 5) | r | (a << 15));
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is B5G5R5A1Codec)
            {
                int byteCount = width * height * 2;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
