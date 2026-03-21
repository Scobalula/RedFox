using System.Numerics;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Defines a codec capable of decoding and encoding pixel data for a specific <see cref="ImageFormat"/>.
    /// <see cref="Vector4"/> is used as the canonical per-pixel representation for HDR-safe conversion,
    /// but the primary conversion path (<see cref="ConvertFrom"/>) avoids bulk intermediate allocations
    /// by writing each pixel directly into the target byte layout.
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
        /// Decodes raw pixel/block data into <see cref="Vector4"/> pixels (RGBA, 0–1 for LDR, unbounded for HDR).
        /// Use this when you explicitly need <see cref="Vector4"/> output (e.g. HDR processing).
        /// For format-to-format conversion prefer <see cref="ConvertFrom"/>.
        /// </summary>
        void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height);

        /// <summary>
        /// Encodes <see cref="Vector4"/> pixels into the raw format.
        /// </summary>
        void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height);

        /// <summary>
        /// Writes a single <see cref="Vector4"/> pixel into the destination buffer at the given pixel index.
        /// This is the per-pixel conversion hot path used by block decoders to avoid intermediate arrays.
        /// </summary>
        void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex);

        /// <summary>
        /// Reads a single pixel from the source buffer at the given pixel index and returns it as <see cref="Vector4"/>.
        /// </summary>
        Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex);

        /// <summary>
        /// Reads a single pixel at the given (x, y) coordinate from the source buffer.
        /// For uncompressed formats this computes the flat index. For block-compressed formats
        /// this decodes the containing 4×4 block and returns the requested pixel.
        /// </summary>
        Vector4 ReadPixel(ReadOnlySpan<byte> source, int x, int y, int width)
        {
            return ReadPixel(source, y * width + x);
        }

        /// <summary>
        /// Batch-writes contiguous <see cref="Vector4"/> pixels into the destination buffer.
        /// The default loops per-pixel; uncompressed codecs may override with SIMD-accelerated paths.
        /// Used by <see cref="BCHelper"/> to write 4 pixels per block row efficiently.
        /// </summary>
        void WritePixels(ReadOnlySpan<Vector4> pixels, Span<byte> destination, int startPixelIndex)
        {
            for (int i = 0; i < pixels.Length; i++)
                WritePixel(pixels[i], destination, startPixelIndex + i);
        }

        /// <summary>
        /// Decodes multiple rows of pixels at once into a <see cref="Vector4"/> span.
        /// The default implementation uses <see cref="ReadPixel(ReadOnlySpan{byte}, int, int, int)"/> per pixel.
        /// Block-compressed codecs override this to decode block-rows efficiently.
        /// </summary>
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
        /// Source-driven conversion: decodes pixels from this codec's format and writes them
        /// into the <paramref name="targetCodec"/>'s byte layout. The default implementation
        /// uses per-pixel <see cref="ReadPixel"/>→<see cref="IPixelCodec.WritePixel"/>.
        /// Block-compressed codecs override this to iterate blocks with only a stackalloc
        /// <see cref="Vector4"/> buffer per block (16 pixels), achieving zero heap allocation.
        /// </summary>
        void DecodeTo(ReadOnlySpan<byte> source, IPixelCodec targetCodec, Span<byte> destination, int width, int height)
        {
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
                targetCodec.WritePixel(ReadPixel(source, i), destination, i);
        }

        /// <summary>
        /// Converts raw pixel data from a source format directly into this codec's format.
        /// The default delegates to the source codec's <see cref="DecodeTo"/> which handles
        /// both uncompressed (per-pixel) and block-compressed (per-block) sources efficiently.
        /// Codecs may override this for fast byte-to-byte paths (memcopy, swizzle, etc.).
        /// </summary>
        void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            sourceCodec.DecodeTo(source, this, destination, width, height);
        }
    }
}
