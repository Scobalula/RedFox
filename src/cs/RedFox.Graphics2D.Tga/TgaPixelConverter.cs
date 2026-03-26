namespace RedFox.Graphics2D.Tga
{
    /// <summary>
    /// Provides pixel format conversion routines for TGA image reading and writing.
    /// TGA files store pixels in BGR or BGRA order; this class converts between
    /// TGA byte layouts and interleaved RGBA8 used by <see cref="Image"/>.
    /// </summary>
    public static class TgaPixelConverter
    {
        /// <summary>
        /// Converts BGR or BGRA pixel data to interleaved R8G8B8A8 output,
        /// swizzling the blue and red channels and filling alpha to 255 for 24-bit sources.
        /// </summary>
        /// <param name="src">The raw TGA pixel data in BGR or BGRA order.</param>
        /// <param name="dst">The destination buffer for RGBA8 output (4 bytes per pixel).</param>
        /// <param name="pixelCount">The total number of pixels to convert.</param>
        /// <param name="bytesPerPixel">
        /// Source bytes per pixel (3 for BGR, 4 for BGRA).
        /// </param>
        public static void BgrToRgba(ReadOnlySpan<byte> src, Span<byte> dst, int pixelCount, int bytesPerPixel)
        {
            bool hasAlpha = bytesPerPixel == 4;

            for (int i = 0; i < pixelCount; i++)
            {
                int s = i * bytesPerPixel;
                int d = i * 4;
                dst[d + 0] = src[s + 2]; // R
                dst[d + 1] = src[s + 1]; // G
                dst[d + 2] = src[s + 0]; // B
                dst[d + 3] = hasAlpha ? src[s + 3] : (byte)255;
            }
        }

        /// <summary>
        /// Flips an image buffer vertically in-place by swapping rows from the top
        /// and bottom toward the center. Used when the TGA origin is bottom-left.
        /// </summary>
        /// <param name="pixels">The pixel buffer (modified in-place).</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="bytesPerPixel">Bytes per pixel in the buffer (typically 4 for RGBA8).</param>
        public static void FlipVertical(Span<byte> pixels, int width, int height, int bytesPerPixel)
        {
            int rowBytes = width * bytesPerPixel;
            Span<byte> temp = rowBytes <= 4096
                ? stackalloc byte[rowBytes]
                : new byte[rowBytes];

            for (int y = 0; y < height / 2; y++)
            {
                int topOffset = y * rowBytes;
                int bottomOffset = (height - 1 - y) * rowBytes;

                var topRow = pixels.Slice(topOffset, rowBytes);
                var bottomRow = pixels.Slice(bottomOffset, rowBytes);

                topRow.CopyTo(temp);
                bottomRow.CopyTo(topRow);
                temp.CopyTo(bottomRow);
            }
        }

        /// <summary>
        /// Scans pixel data for any alpha value less than 255 (non-opaque).
        /// </summary>
        /// <param name="data">The pixel data buffer.</param>
        /// <param name="stride">The byte stride between successive alpha samples.</param>
        /// <param name="alphaOffset">The byte offset of the first alpha sample within the stride.</param>
        /// <returns><see langword="true"/> if any alpha byte is less than 255.</returns>
        public static bool HasNonOpaqueAlpha(ReadOnlySpan<byte> data, int stride, int alphaOffset)
        {
            for (int i = alphaOffset; i < data.Length; i += stride)
            {
                if (data[i] < 255)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Writes pixel rows from an RGBA source with R/B channel swizzle to produce BGR or BGRA output.
        /// Each row is written to the stream individually to avoid large temporary allocations.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="rgba">The source RGBA pixel data.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="hasAlpha">
        /// <see langword="true"/> to write 4-byte BGRA pixels;
        /// <see langword="false"/> to write 3-byte BGR pixels.
        /// </param>
        public static void WriteRowsSwizzled(Stream stream, ReadOnlySpan<byte> rgba, int width, int height, bool hasAlpha)
        {
            int outBpp = hasAlpha ? 4 : 3;
            var rowBuffer = new byte[width * outBpp];

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int s = rowStart + x * 4;
                    int d = x * outBpp;
                    rowBuffer[d + 0] = rgba[s + 2]; // B
                    rowBuffer[d + 1] = rgba[s + 1]; // G
                    rowBuffer[d + 2] = rgba[s + 0]; // R
                    if (hasAlpha)
                    {
                        rowBuffer[d + 3] = rgba[s + 3]; // A
                    }
                }

                stream.Write(rowBuffer);
            }
        }

        /// <summary>
        /// Writes BGR rows from a BGRA source, stripping the alpha channel.
        /// Each row is written to the stream individually.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="bgra">The source BGRA pixel data.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="srcBytesPerPixel">Source bytes per pixel (typically 4).</param>
        public static void WriteRowsBgr(Stream stream, ReadOnlySpan<byte> bgra, int width, int height, int srcBytesPerPixel)
        {
            var rowBuffer = new byte[width * 3];

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * srcBytesPerPixel;

                for (int x = 0; x < width; x++)
                {
                    int s = rowStart + x * srcBytesPerPixel;
                    int d = x * 3;
                    rowBuffer[d + 0] = bgra[s + 0];
                    rowBuffer[d + 1] = bgra[s + 1];
                    rowBuffer[d + 2] = bgra[s + 2];
                }

                stream.Write(rowBuffer);
            }
        }
    }
}
