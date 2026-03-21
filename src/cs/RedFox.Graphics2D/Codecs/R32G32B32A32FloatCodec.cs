using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R32G32B32A32Float"/>.
    /// This is a lossless pass-through since <see cref="Vector4"/> is already 4×float.
    /// </summary>
    public sealed class R32G32B32A32FloatCodec : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format => ImageFormat.R32G32B32A32Float;

        /// <inheritdoc/>
        public int BytesPerPixel => 16;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var sourceVectors = MemoryMarshal.Cast<byte, Vector4>(source);
            int pixelCount = width * height;
            sourceVectors[..pixelCount].CopyTo(destination);
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;
            var destVectors = MemoryMarshal.Cast<byte, Vector4>(destination);
            source[..pixelCount].CopyTo(destVectors);
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var vectors = MemoryMarshal.Cast<byte, Vector4>(source);
            return vectors[pixelIndex];
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var vectors = MemoryMarshal.Cast<byte, Vector4>(destination);
            vectors[pixelIndex] = pixel;
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R32G32B32A32FloatCodec)
            {
                int byteCount = width * height * 16;
                source[..byteCount].CopyTo(destination);
                return;
            }

            // R32G32B32A32_FLOAT is Vector4 in memory — decode directly into destination
            var destVectors = MemoryMarshal.Cast<byte, Vector4>(destination);
            sourceCodec.Decode(source, destVectors[..(width * height)], width, height);
        }
    }
}
