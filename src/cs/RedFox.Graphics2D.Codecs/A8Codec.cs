using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.A8Unorm"/>.
    /// 8-bit alpha-only format.
    /// </summary>
    public sealed class A8Codec : IPixelCodec
    {
        private const float Inv255 = 1.0f / 255.0f;

        /// <inheritdoc/>
        public ImageFormat Format => ImageFormat.A8Unorm;

        /// <inheritdoc/>
        public int BytesPerPixel => 1;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                destination[i] = new Vector4(0f, 0f, 0f, source[i] * Inv255);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                destination[i] = (byte)(Math.Clamp(source[i].W, 0f, 1f) * 255f + 0.5f);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            return new Vector4(0f, 0f, 0f, source[pixelIndex] * Inv255);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            destination[pixelIndex] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is A8Codec)
            {
                int byteCount = width * height;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
