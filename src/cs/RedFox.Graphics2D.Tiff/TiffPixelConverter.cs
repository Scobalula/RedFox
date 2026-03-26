using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Provides pixel format conversion routines for TIFF image reading and writing.
    /// </summary>
    public static class TiffPixelConverter
    {
        /// <summary>
        /// Converts decoded 8-bit TIFF sample data to interleaved RGBA8 output.
        /// Handles grayscale (1 sample), grayscale+alpha (2), RGB (3), and RGBA (4+) layouts.
        /// </summary>
        /// <param name="src">Source pixel data in the TIFF sample layout.</param>
        /// <param name="dst">Destination buffer for RGBA8 output (4 bytes per pixel).</param>
        /// <param name="pixelCount">Total number of pixels to convert.</param>
        /// <param name="samplesPerPixel">Number of samples per pixel in the source data.</param>
        /// <param name="photometric">Photometric interpretation (0 = MinIsWhite, 1 = MinIsBlack, 2 = RGB).</param>
        public static void ConvertToRgba8(
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int pixelCount,
            int samplesPerPixel,
            int photometric)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                int s = i * samplesPerPixel;
                int d = i * 4;

                switch (samplesPerPixel)
                {
                    case 1:
                        byte gray = photometric == 0 ? (byte)(255 - src[s]) : src[s];
                        dst[d + 0] = gray;
                        dst[d + 1] = gray;
                        dst[d + 2] = gray;
                        dst[d + 3] = 255;
                        break;
                    case 2:
                        byte gray2 = photometric == 0 ? (byte)(255 - src[s]) : src[s];
                        dst[d + 0] = gray2;
                        dst[d + 1] = gray2;
                        dst[d + 2] = gray2;
                        dst[d + 3] = src[s + 1];
                        break;
                    case 3:
                        dst[d + 0] = src[s + 0];
                        dst[d + 1] = src[s + 1];
                        dst[d + 2] = src[s + 2];
                        dst[d + 3] = 255;
                        break;
                    default:
                        dst[d + 0] = src[s + 0];
                        dst[d + 1] = src[s + 1];
                        dst[d + 2] = src[s + 2];
                        dst[d + 3] = src[s + 3];
                        break;
                }
            }
        }

        /// <summary>
        /// Converts decoded 16-bit TIFF sample data to interleaved RGBA8 output
        /// by taking the high byte of each 16-bit sample.
        /// </summary>
        /// <param name="src">Source pixel data with 16-bit samples.</param>
        /// <param name="dst">Destination buffer for RGBA8 output (4 bytes per pixel).</param>
        /// <param name="pixelCount">Total number of pixels to convert.</param>
        /// <param name="samplesPerPixel">Number of samples per pixel in the source data.</param>
        /// <param name="photometric">Photometric interpretation.</param>
        /// <param name="le"><see langword="true"/> for little-endian sample byte order.</param>
        public static void ConvertToRgba16(
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int pixelCount,
            int samplesPerPixel,
            int photometric,
            bool le)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                int s = i * samplesPerPixel * 2;
                int d = i * 4;

                switch (samplesPerPixel)
                {
                    case 1:
                    {
                        ushort g16 = Read16(src, s, le);
                        byte gray = (byte)(g16 >> 8);
                        if (photometric == 0) gray = (byte)(255 - gray);
                        dst[d + 0] = gray;
                        dst[d + 1] = gray;
                        dst[d + 2] = gray;
                        dst[d + 3] = 255;
                        break;
                    }
                    case 2:
                    {
                        ushort g16 = Read16(src, s, le);
                        ushort a16 = Read16(src, s + 2, le);
                        byte gray = (byte)(g16 >> 8);
                        if (photometric == 0) gray = (byte)(255 - gray);
                        dst[d + 0] = gray;
                        dst[d + 1] = gray;
                        dst[d + 2] = gray;
                        dst[d + 3] = (byte)(a16 >> 8);
                        break;
                    }
                    case 3:
                    {
                        dst[d + 0] = (byte)(Read16(src, s, le) >> 8);
                        dst[d + 1] = (byte)(Read16(src, s + 2, le) >> 8);
                        dst[d + 2] = (byte)(Read16(src, s + 4, le) >> 8);
                        dst[d + 3] = 255;
                        break;
                    }
                    default:
                    {
                        dst[d + 0] = (byte)(Read16(src, s, le) >> 8);
                        dst[d + 1] = (byte)(Read16(src, s + 2, le) >> 8);
                        dst[d + 2] = (byte)(Read16(src, s + 4, le) >> 8);
                        dst[d + 3] = (byte)(Read16(src, s + 6, le) >> 8);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Extracts image pixel data as interleaved 8-bit RGB or RGBA suitable for TIFF strip output.
        /// Handles RGBA, BGRA, BGRX source formats directly, and falls back to
        /// <see cref="PixelCodecRegistry"/> for other formats.
        /// </summary>
        /// <param name="slice">The source image slice.</param>
        /// <param name="format">The pixel format of the source image.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="includeAlpha">If <see langword="true"/>, output RGBA; otherwise RGB.</param>
        /// <returns>A byte array containing the extracted pixel data.</returns>
        public static byte[] ExtractRgbData(
            in ImageSlice slice,
            ImageFormat format,
            int width,
            int height,
            bool includeAlpha)
        {
            int channels = includeAlpha ? 4 : 3;
            int rowBytes = width * channels;
            var output = new byte[rowBytes * height];
            var src = slice.PixelSpan;

            if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
            {
                ExtractFromRgba(src, output, width, height, slice.RowPitch, rowBytes, channels, includeAlpha);
                return output;
            }

            if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb
                      or ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb)
            {
                ExtractFromBgra(src, output, width, height, slice.RowPitch, rowBytes, channels, includeAlpha);
                return output;
            }

            // General fallback via codec
            if (!PixelCodecRegistry.TryGetCodec(format, out var codec) || codec is null)
                throw new NotSupportedException($"TIFF writing is not supported for format {format}.");

            ExtractViaCodec(codec, src, output, width, height, rowBytes, channels, includeAlpha);
            return output;
        }

        /// <summary>
        /// Checks whether any pixel in a 4-bpp buffer has a non-opaque alpha value.
        /// </summary>
        /// <param name="data">The pixel data with alpha at every 4th byte.</param>
        /// <returns><see langword="true"/> if any alpha byte is less than 255.</returns>
        public static bool HasNonOpaqueAlpha(ReadOnlySpan<byte> data)
        {
            for (int i = 3; i < data.Length; i += 4)
            {
                if (data[i] < 255)
                    return true;
            }
            return false;
        }

        // ──────────────────────────────────────────────
        // Private extraction helpers
        // ──────────────────────────────────────────────

        private static void ExtractFromRgba(
            ReadOnlySpan<byte> src,
            Span<byte> output,
            int width, int height,
            int srcRowPitch, int dstRowBytes,
            int channels, bool includeAlpha)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = y * srcRowPitch;
                int dstRow = y * dstRowBytes;

                if (includeAlpha)
                {
                    // RGBA → RGBA — direct copy per row.
                    src.Slice(srcRow, dstRowBytes).CopyTo(output.Slice(dstRow, dstRowBytes));
                }
                else
                {
                    // RGBA → RGB — strip alpha channel.
                    for (int x = 0; x < width; x++)
                    {
                        int s = srcRow + x * 4;
                        int d = dstRow + x * 3;
                        output[d + 0] = src[s + 0];
                        output[d + 1] = src[s + 1];
                        output[d + 2] = src[s + 2];
                    }
                }
            }
        }

        private static void ExtractFromBgra(
            ReadOnlySpan<byte> src,
            Span<byte> output,
            int width, int height,
            int srcRowPitch, int dstRowBytes,
            int channels, bool includeAlpha)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = y * srcRowPitch;
                int dstRow = y * dstRowBytes;

                for (int x = 0; x < width; x++)
                {
                    int s = srcRow + x * 4;
                    int d = dstRow + x * channels;
                    output[d + 0] = src[s + 2]; // R
                    output[d + 1] = src[s + 1]; // G
                    output[d + 2] = src[s + 0]; // B
                    if (includeAlpha)
                        output[d + 3] = src[s + 3];
                }
            }
        }

        private static void ExtractViaCodec(
            IPixelCodec codec,
            ReadOnlySpan<byte> src,
            Span<byte> output,
            int width, int height,
            int dstRowBytes, int channels,
            bool includeAlpha)
        {
            const int stripHeight = 4;
            var pixels = new Vector4[width * stripHeight];

            for (int stripY = 0; stripY < height; stripY += stripHeight)
            {
                int rows = Math.Min(stripHeight, height - stripY);
                codec.DecodeRows(src, pixels, stripY, rows, width, height);

                for (int row = 0; row < rows; row++)
                {
                    int pixelBase = row * width;
                    int dstRow = (stripY + row) * dstRowBytes;

                    for (int x = 0; x < width; x++)
                    {
                        Vector4 pixel = pixels[pixelBase + x];
                        int d = dstRow + x * channels;
                        output[d + 0] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
                        output[d + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
                        output[d + 2] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
                        if (includeAlpha)
                            output[d + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
                    }
                }
            }
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer with the specified byte order.
        /// </summary>
        private static ushort Read16(ReadOnlySpan<byte> data, int offset, bool le)
        {
            return le
                ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
                : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
        }
    }
}
