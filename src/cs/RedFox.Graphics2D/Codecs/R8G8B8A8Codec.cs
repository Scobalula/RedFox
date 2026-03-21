using System.Numerics;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R8G8B8A8Unorm"/> and SRGB variants.
    /// </summary>
    public sealed class R8G8B8A8Codec(ImageFormat format) : IPixelCodec
    {
        private const float Inv255 = 1.0f / 255.0f;

        /// <inheritdoc/>
        public ImageFormat Format { get; } = format;

        /// <inheritdoc/>
        public int BytesPerPixel => 4;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            PixelSimd.DecodeRgba8(source, destination, width * height);
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            PixelSimd.EncodeToRgba8(source, destination, width * height);
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            int offset = pixelIndex * 4;
            return new Vector4(
                source[offset + 0] * Inv255,
                source[offset + 1] * Inv255,
                source[offset + 2] * Inv255,
                source[offset + 3] * Inv255);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            int offset = pixelIndex * 4;
            destination[offset + 0] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
            destination[offset + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
            destination[offset + 2] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
            destination[offset + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;

            if (sourceCodec is R8G8B8A8Codec)
            {
                source[..(pixelCount * 4)].CopyTo(destination);
                return;
            }

            if (sourceCodec is B8G8R8A8Codec)
            {
                PixelSimd.SwizzleRedBlue(source, destination, pixelCount);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }

        /// <inheritdoc/>
        public void WritePixels(ReadOnlySpan<Vector4> pixels, Span<byte> destination, int startPixelIndex)
        {
            PixelSimd.EncodeToRgba8(pixels, destination[(startPixelIndex * 4)..], pixels.Length);
        }
    }
}
