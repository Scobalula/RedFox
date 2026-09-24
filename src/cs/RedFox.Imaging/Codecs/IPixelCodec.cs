using System;
using System.Numerics;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs
{
    /// <summary>
    /// Defines a codec for a specific <see cref="ImageFormat"/>.
    /// </summary>
    public interface IPixelCodec
    {
        /// <summary>
        /// Gets the <see cref="ImageFormat"/> this codec handles.
        /// </summary>
        ImageFormat Format { get; }

        /// <summary>
        /// Gets the number of bytes per pixel for this format.
        /// Block-compressed codecs return 0.
        /// </summary>
        int BytesPerPixel { get; }

        /// <summary>
        /// Decodes native pixel data into RGBA values.
        /// </summary>
        /// <param name="source">The source pixel or block data in this codec's native format.</param>
        /// <param name="destination">The destination span that receives decoded RGBA pixels.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height);

        /// <summary>
        /// Encodes <see cref="Vector4"/> pixels into the raw format.
        /// </summary>
        /// <param name="source">The source RGBA pixels to encode.</param>
        /// <param name="destination">The destination span that receives encoded bytes in this codec's native format.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height);

        /// <summary>
        /// Writes a pixel to the destination buffer.
        /// </summary>
        /// <param name="pixel">The pixel value to write.</param>
        /// <param name="destination">The destination buffer in this codec's native byte layout.</param>
        /// <param name="pixelIndex">The zero-based pixel index to write.</param>
        void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex);

        /// <summary>
        /// Reads a single pixel from the source buffer at the given pixel index and returns it as <see cref="Vector4"/>.
        /// </summary>
        /// <param name="source">The source buffer in this codec's native byte layout.</param>
        /// <param name="pixelIndex">The zero-based pixel index to read.</param>
        /// <returns>The decoded pixel value.</returns>
        Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex);

        /// <summary>
        /// Reads a pixel at the specified coordinates.
        /// </summary>
        /// <param name="source">The source buffer in this codec's native byte layout.</param>
        /// <param name="x">The zero-based X coordinate of the pixel to read.</param>
        /// <param name="y">The zero-based Y coordinate of the pixel to read.</param>
        /// <param name="width">The full image width in pixels.</param>
        /// <returns>The decoded pixel value.</returns>
        Vector4 ReadPixel(ReadOnlySpan<byte> source, int x, int y, int width)
        {
            return ReadPixel(source, y * width + x);
        }

        /// <summary>
        /// Writes contiguous pixels to the destination buffer.
        /// </summary>
        /// <param name="pixels">The pixels to write.</param>
        /// <param name="destination">The destination buffer in this codec's native byte layout.</param>
        /// <param name="startPixelIndex">The zero-based destination pixel index for the first pixel.</param>
        void WritePixels(ReadOnlySpan<Vector4> pixels, Span<byte> destination, int startPixelIndex)
        {
            for (int i = 0; i < pixels.Length; i++)
                WritePixel(pixels[i], destination, startPixelIndex + i);
        }

        /// <summary>
        /// Decodes a range of rows into RGBA values.
        /// </summary>
        /// <param name="source">The source pixel or block data in this codec's native format.</param>
        /// <param name="destination">The destination span that receives decoded RGBA pixels for the requested rows.</param>
        /// <param name="startRow">The first row to decode.</param>
        /// <param name="rowCount">The number of rows to decode.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        void DecodeRows(ReadOnlySpan<byte> source, Span<Vector4> destination, int startRow, int rowCount, int width, int height)
        {
            for (int row = 0; row < rowCount; row++)
            {
                int y = startRow + row;
                if (y >= height) break;

                for (int x = 0; x < width; x++)
                    destination[row * width + x] = ReadPixel(source, x, y, width);
            }
        }

        /// <summary>
        /// Converts native data into the target codec's format.
        /// </summary>
        /// <param name="source">The source pixel or block data in this codec's native format.</param>
        /// <param name="targetCodec">The codec describing the destination byte layout.</param>
        /// <param name="destination">The destination buffer that receives encoded bytes in <paramref name="targetCodec"/>'s format.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
                targetCodec.WritePixel(ReadPixel(source, i), destination, i);
        }

        /// <summary>
        /// Converts source data into this codec's format.
        /// </summary>
        /// <param name="source">The source pixel or block data in <paramref name="sourceCodec"/>'s native format.</param>
        /// <param name="sourceCodec">The codec describing the source byte layout.</param>
        /// <param name="destination">The destination buffer in this codec's native format.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
