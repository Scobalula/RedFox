using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R16G16B16A16Float"/>.
    /// Converts between <see cref="Half"/> and <see cref="float"/> to preserve HDR range.
    /// </summary>
    public sealed class R16G16B16A16FloatCodec : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format => ImageFormat.R16G16B16A16Float;

        /// <inheritdoc/>
        public int BytesPerPixel => 8;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            int pixelCount = width * height;
            var halfs = MemoryMarshal.Cast<byte, Half>(source);

            for (int i = 0; i < pixelCount; i++)
            {
                int o = i * 4;
                destination[i] = new Vector4(
                    (float)halfs[o],
                    (float)halfs[o + 1],
                    (float)halfs[o + 2],
                    (float)halfs[o + 3]);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;
            var halfs = MemoryMarshal.Cast<byte, Half>(destination);

            for (int i = 0; i < pixelCount; i++)
            {
                var p = source[i];
                int o = i * 4;
                halfs[o] = (Half)p.X;
                halfs[o + 1] = (Half)p.Y;
                halfs[o + 2] = (Half)p.Z;
                halfs[o + 3] = (Half)p.W;
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var halfs = MemoryMarshal.Cast<byte, Half>(source);
            int o = pixelIndex * 4;
            return new Vector4(
                (float)halfs[o],
                (float)halfs[o + 1],
                (float)halfs[o + 2],
                (float)halfs[o + 3]);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var halfs = MemoryMarshal.Cast<byte, Half>(destination);
            int o = pixelIndex * 4;
            halfs[o] = (Half)pixel.X;
            halfs[o + 1] = (Half)pixel.Y;
            halfs[o + 2] = (Half)pixel.Z;
            halfs[o + 3] = (Half)pixel.W;
        }
    }
}
