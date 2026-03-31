using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.B4G4R4A4Unorm"/>.
    /// 16-bit packed format: 4 bits blue, 4 bits green, 4 bits red, 4 bits alpha.
    /// </summary>
    public sealed class B4G4R4A4Codec : IPixelCodec
    {
        private const float Inv15 = 1.0f / 15.0f;

        /// <inheritdoc/>
        public ImageFormat Format => ImageFormat.B4G4R4A4Unorm;

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
                    ((packed >> 12) & 0xF) * Inv15,
                    ((packed >> 8) & 0xF) * Inv15,
                    ((packed >> 4) & 0xF) * Inv15,
                    (packed & 0xF) * Inv15);
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
                ushort b = (ushort)(Math.Clamp(p.X, 0f, 1f) * 15f + 0.5f);
                ushort g = (ushort)(Math.Clamp(p.Y, 0f, 1f) * 15f + 0.5f);
                ushort r = (ushort)(Math.Clamp(p.Z, 0f, 1f) * 15f + 0.5f);
                ushort a = (ushort)(Math.Clamp(p.W, 0f, 1f) * 15f + 0.5f);
                ushorts[i] = (ushort)((b << 12) | (g << 8) | (r << 4) | a);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(source);
            ushort packed = ushorts[pixelIndex];
            return new Vector4(
                ((packed >> 12) & 0xF) * Inv15,
                ((packed >> 8) & 0xF) * Inv15,
                ((packed >> 4) & 0xF) * Inv15,
                (packed & 0xF) * Inv15);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var ushorts = MemoryMarshal.Cast<byte, ushort>(destination);
            ushort b = (ushort)(Math.Clamp(pixel.X, 0f, 1f) * 15f + 0.5f);
            ushort g = (ushort)(Math.Clamp(pixel.Y, 0f, 1f) * 15f + 0.5f);
            ushort r = (ushort)(Math.Clamp(pixel.Z, 0f, 1f) * 15f + 0.5f);
            ushort a = (ushort)(Math.Clamp(pixel.W, 0f, 1f) * 15f + 0.5f);
            ushorts[pixelIndex] = (ushort)((b << 12) | (g << 8) | (r << 4) | a);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is B4G4R4A4Codec)
            {
                int byteCount = width * height * 2;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
