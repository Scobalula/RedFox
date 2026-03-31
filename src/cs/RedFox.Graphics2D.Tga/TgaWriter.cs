using System.Numerics;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.Tga
{
    /// <summary>
    /// Provides static methods for writing TGA image data to a stream.
    /// Supports fast path writes for BGRA and RGBA sources, and a general
    /// codec-based decode path for block-compressed or exotic pixel formats.
    /// </summary>
    public static class TgaWriter
    {
        /// <summary>
        /// Writes an <see cref="Image"/> to the given stream in uncompressed TGA format.
        /// Chooses the most efficient write path based on the source pixel format:
        /// BGRA sources write with zero swizzle, RGBA sources perform an inline
        /// R/B swap, and all other formats are decoded through <see cref="PixelCodec"/>.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="image">The image to write.</param>
        public static void Save(Stream stream, Image image)
        {
            ref readonly ImageSlice slice = ref image.GetSlice(0, 0, 0);

            int width = slice.Width;
            int height = slice.Height;
            ImageFormat format = image.Format;
            ReadOnlySpan<byte> pixelSpan = slice.PixelSpan;

            if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb)
            {
                WriteBgra(stream, pixelSpan, width, height);
                return;
            }

            if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
            {
                WriteRgba(stream, pixelSpan, width, height);
                return;
            }

            WriteDecoded(stream, slice, width, height, format);
        }

        /// <summary>
        /// Writes from a BGRA source. If all pixels are opaque the alpha channel
        /// is stripped and a 24-bit TGA is produced; otherwise 32-bit BGRA is written.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="bgra">The source BGRA pixel data.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        public static void WriteBgra(Stream stream, ReadOnlySpan<byte> bgra, int width, int height)
        {
            bool hasAlpha = TgaPixelConverter.HasNonOpaqueAlpha(bgra, stride: 4, alphaOffset: 3);
            WriteHeaderToStream(stream, width, height, hasAlpha);

            if (hasAlpha)
            {
                stream.Write(bgra);
            }
            else
            {
                TgaPixelConverter.WriteRowsBgr(stream, bgra, width, height, srcBytesPerPixel: 4);
            }
        }

        /// <summary>
        /// Writes from an RGBA source by swizzling R and B channels inline.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="rgba">The source RGBA pixel data.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        public static void WriteRgba(Stream stream, ReadOnlySpan<byte> rgba, int width, int height)
        {
            bool hasAlpha = TgaPixelConverter.HasNonOpaqueAlpha(rgba, stride: 4, alphaOffset: 3);
            WriteHeaderToStream(stream, width, height, hasAlpha);
            TgaPixelConverter.WriteRowsSwizzled(stream, rgba, width, height, hasAlpha);
        }

        /// <summary>
        /// General fallback: decodes pixels through <see cref="PixelCodec"/> in 4-row strips
        /// to match block-compressed block height, then writes BGRA to the stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="slice">The source image slice.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="format">The pixel format to decode from.</param>
        public static void WriteDecoded(Stream stream, in ImageSlice slice, int width, int height, ImageFormat format)
        {
            IPixelCodec codec = PixelCodec.GetCodec(format);
            WriteHeaderToStream(stream, width, height, hasAlpha: true);

            const int StripHeight = 4;
            var pixelBuf = new Vector4[width * StripHeight];
            var rowBuffer = new byte[width * 4];
            ReadOnlySpan<byte> pixelSpan = slice.PixelSpan;

            for (int stripY = 0; stripY < height; stripY += StripHeight)
            {
                int rows = Math.Min(StripHeight, height - stripY);
                codec.DecodeRows(pixelSpan, pixelBuf, stripY, rows, width, height);

                for (int row = 0; row < rows; row++)
                {
                    int pixelBase = row * width;

                    for (int x = 0; x < width; x++)
                    {
                        Vector4 pixel = pixelBuf[pixelBase + x];
                        int d = x * 4;
                        rowBuffer[d + 0] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f); // B
                        rowBuffer[d + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f); // G
                        rowBuffer[d + 2] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f); // R
                        rowBuffer[d + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f); // A
                    }

                    stream.Write(rowBuffer);
                }
            }
        }

        /// <summary>
        /// Writes the 18-byte TGA header for an uncompressed true-color image.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="hasAlpha">
        /// <see langword="true"/> for 32-bit BGRA; <see langword="false"/> for 24-bit BGR.
        /// </param>
        public static void WriteHeaderToStream(Stream stream, int width, int height, bool hasAlpha)
        {
            Span<byte> header = stackalloc byte[TgaHeader.SizeInBytes];
            TgaHeader.WriteTrueColor(header, width, height, hasAlpha);
            stream.Write(header);
        }
    }
}
