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
                int sourceOffset = i * samplesPerPixel;
                int destinationOffset = i * 4;

                switch (samplesPerPixel)
                {
                    case 1:
                        byte gray = photometric == 0 ? (byte)(255 - src[sourceOffset]) : src[sourceOffset];
                        dst[destinationOffset + 0] = gray;
                        dst[destinationOffset + 1] = gray;
                        dst[destinationOffset + 2] = gray;
                        dst[destinationOffset + 3] = 255;
                        break;
                    case 2:
                        byte grayAlpha = photometric == 0 ? (byte)(255 - src[sourceOffset]) : src[sourceOffset];
                        dst[destinationOffset + 0] = grayAlpha;
                        dst[destinationOffset + 1] = grayAlpha;
                        dst[destinationOffset + 2] = grayAlpha;
                        dst[destinationOffset + 3] = src[sourceOffset + 1];
                        break;
                    case 3:
                        dst[destinationOffset + 0] = src[sourceOffset + 0];
                        dst[destinationOffset + 1] = src[sourceOffset + 1];
                        dst[destinationOffset + 2] = src[sourceOffset + 2];
                        dst[destinationOffset + 3] = 255;
                        break;
                    default:
                        dst[destinationOffset + 0] = src[sourceOffset + 0];
                        dst[destinationOffset + 1] = src[sourceOffset + 1];
                        dst[destinationOffset + 2] = src[sourceOffset + 2];
                        dst[destinationOffset + 3] = src[sourceOffset + 3];
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
        /// <param name="littleEndian"><see langword="true"/> for little-endian sample byte order.</param>
        public static void ConvertToRgba16(
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int pixelCount,
            int samplesPerPixel,
            int photometric,
            bool littleEndian)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                int sourceOffset = i * samplesPerPixel * 2;
                int destinationOffset = i * 4;

                switch (samplesPerPixel)
                {
                    case 1:
                    {
                        ushort gray16 = Read16(src, sourceOffset, littleEndian);
                        byte gray = (byte)(gray16 >> 8);
                        if (photometric == 0)
                            gray = (byte)(255 - gray);

                        dst[destinationOffset + 0] = gray;
                        dst[destinationOffset + 1] = gray;
                        dst[destinationOffset + 2] = gray;
                        dst[destinationOffset + 3] = 255;
                        break;
                    }
                    case 2:
                    {
                        ushort gray16 = Read16(src, sourceOffset, littleEndian);
                        ushort alpha16 = Read16(src, sourceOffset + 2, littleEndian);
                        byte gray = (byte)(gray16 >> 8);
                        if (photometric == 0)
                            gray = (byte)(255 - gray);

                        dst[destinationOffset + 0] = gray;
                        dst[destinationOffset + 1] = gray;
                        dst[destinationOffset + 2] = gray;
                        dst[destinationOffset + 3] = (byte)(alpha16 >> 8);
                        break;
                    }
                    case 3:
                        dst[destinationOffset + 0] = (byte)(Read16(src, sourceOffset, littleEndian) >> 8);
                        dst[destinationOffset + 1] = (byte)(Read16(src, sourceOffset + 2, littleEndian) >> 8);
                        dst[destinationOffset + 2] = (byte)(Read16(src, sourceOffset + 4, littleEndian) >> 8);
                        dst[destinationOffset + 3] = 255;
                        break;
                    default:
                        dst[destinationOffset + 0] = (byte)(Read16(src, sourceOffset, littleEndian) >> 8);
                        dst[destinationOffset + 1] = (byte)(Read16(src, sourceOffset + 2, littleEndian) >> 8);
                        dst[destinationOffset + 2] = (byte)(Read16(src, sourceOffset + 4, littleEndian) >> 8);
                        dst[destinationOffset + 3] = (byte)(Read16(src, sourceOffset + 6, littleEndian) >> 8);
                        break;
                }
            }
        }

        /// <summary>
        /// Extracts image data into TIFF-native interleaved sample bytes ready for writing.
        /// </summary>
        /// <param name="slice">The source image slice.</param>
        /// <param name="format">The pixel format of the source image.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>A <see cref="TiffEncodedPixelData"/> describing the encoded TIFF sample layout.</returns>
        public static TiffEncodedPixelData ExtractEncodedPixelData(in ImageSlice slice, ImageFormat format, int width, int height)
        {
            switch (format)
            {
                case ImageFormat.R8Unorm:
                    return new TiffEncodedPixelData(
                        CopyRowsContiguous(slice.PixelSpan, slice.RowPitch, width, height, rowBytes: width),
                        TiffConstants.PhotometricMinIsBlack,
                        [8],
                        1,
                        null);

                case ImageFormat.R16Unorm:
                    return new TiffEncodedPixelData(
                        CopyRowsContiguous(slice.PixelSpan, slice.RowPitch, width, height, rowBytes: width * 2),
                        TiffConstants.PhotometricMinIsBlack,
                        [16],
                        1,
                        null);

                case ImageFormat.R8G8Unorm:
                    return ExtractGrayAlpha8(slice, width, height);

                case ImageFormat.R16G16Unorm:
                    return ExtractGrayAlpha16(slice, width, height);

                case ImageFormat.R16G16B16A16Unorm:
                    return ExtractRgba16(slice, width, height);
            }

            bool hasAlpha = format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb or ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb
                && HasNonOpaqueAlpha(slice.PixelSpan);
            byte[] pixelData = ExtractRgb8Data(slice, format, width, height, hasAlpha);
            ushort samplesPerPixel = hasAlpha ? (ushort)4 : (ushort)3;

            return new TiffEncodedPixelData(
                pixelData,
                TiffConstants.PhotometricRGB,
                CreateBitsPerSample(bitsPerSample: 8, sampleCount: samplesPerPixel),
                samplesPerPixel,
                hasAlpha ? (ushort)2 : null);
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

        /// <summary>
        /// Extracts TIFF-native sample data from an 8-bit grayscale-plus-alpha image slice.
        /// </summary>
        /// <param name="slice">The source image slice.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>A TIFF-native grayscale or grayscale-plus-alpha representation of <paramref name="slice"/>.</returns>
        public static TiffEncodedPixelData ExtractGrayAlpha8(in ImageSlice slice, int width, int height)
        {
            bool hasAlpha = HasNonOpaqueGrayAlpha8(slice.PixelSpan, slice.RowPitch, width, height);
            if (!hasAlpha)
            {
                return new TiffEncodedPixelData(
                    CopySingleChannel8(slice.PixelSpan, slice.RowPitch, width, height, bytesPerPixel: 2),
                    TiffConstants.PhotometricMinIsBlack,
                    [8],
                    1,
                    null);
            }

            return new TiffEncodedPixelData(
                CopyRowsContiguous(slice.PixelSpan, slice.RowPitch, width, height, rowBytes: width * 2),
                TiffConstants.PhotometricMinIsBlack,
                [8, 8],
                2,
                2);
        }

        /// <summary>
        /// Extracts TIFF-native sample data from a 16-bit grayscale-plus-alpha image slice.
        /// </summary>
        /// <param name="slice">The source image slice.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>A TIFF-native grayscale or grayscale-plus-alpha representation of <paramref name="slice"/>.</returns>
        public static TiffEncodedPixelData ExtractGrayAlpha16(in ImageSlice slice, int width, int height)
        {
            bool hasAlpha = HasNonOpaqueGrayAlpha16(slice.PixelSpan, slice.RowPitch, width, height);
            if (!hasAlpha)
            {
                return new TiffEncodedPixelData(
                    CopySingleChannel16(slice.PixelSpan, slice.RowPitch, width, height, bytesPerPixel: 4),
                    TiffConstants.PhotometricMinIsBlack,
                    [16],
                    1,
                    null);
            }

            return new TiffEncodedPixelData(
                CopyRowsContiguous(slice.PixelSpan, slice.RowPitch, width, height, rowBytes: width * 4),
                TiffConstants.PhotometricMinIsBlack,
                [16, 16],
                2,
                2);
        }

        /// <summary>
        /// Extracts TIFF-native sample data from a 16-bit RGBA image slice.
        /// </summary>
        /// <param name="slice">The source image slice.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>A TIFF-native RGB or RGBA representation of <paramref name="slice"/>.</returns>
        public static TiffEncodedPixelData ExtractRgba16(in ImageSlice slice, int width, int height)
        {
            bool hasAlpha = HasNonOpaqueAlpha16(slice.PixelSpan, slice.RowPitch, width, height);
            if (!hasAlpha)
            {
                return new TiffEncodedPixelData(
                    CopyRgb16FromRgba16(slice.PixelSpan, slice.RowPitch, width, height),
                    TiffConstants.PhotometricRGB,
                    [16, 16, 16],
                    3,
                    null);
            }

            return new TiffEncodedPixelData(
                CopyRowsContiguous(slice.PixelSpan, slice.RowPitch, width, height, rowBytes: width * 8),
                TiffConstants.PhotometricRGB,
                [16, 16, 16, 16],
                4,
                2);
        }

        /// <summary>
        /// Extracts 8-bit RGB or RGBA sample data from a source image slice.
        /// </summary>
        /// <param name="slice">The source image slice.</param>
        /// <param name="format">The pixel format of the source slice.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="includeAlpha"><see langword="true"/> to include alpha samples in the output; otherwise RGB only.</param>
        /// <returns>A tightly packed RGB or RGBA byte buffer.</returns>
        public static byte[] ExtractRgb8Data(in ImageSlice slice, ImageFormat format, int width, int height, bool includeAlpha)
        {
            int channels = includeAlpha ? 4 : 3;
            int rowBytes = width * channels;
            byte[] output = new byte[rowBytes * height];
            ReadOnlySpan<byte> source = slice.PixelSpan;

            if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
            {
                ExtractFromRgba(source, output, width, height, slice.RowPitch, rowBytes, channels, includeAlpha);
                return output;
            }

            if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb or ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb)
            {
                ExtractFromBgra(source, output, width, height, slice.RowPitch, rowBytes, channels, includeAlpha);
                return output;
            }

            if (!PixelCodecRegistry.Default.TryGetCodec(format, out IPixelCodec? codec) || codec is null)
                throw new NotSupportedException($"TIFF writing is not supported for format {format}.");

            ExtractViaCodec(codec, source, output, width, height, rowBytes, channels, includeAlpha);
            return output;
        }

        /// <summary>
        /// Copies image rows into a tightly packed contiguous buffer.
        /// </summary>
        /// <param name="source">The source pixel data.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="rowBytes">The number of bytes to copy from each row.</param>
        /// <returns>A tightly packed copy of the source rows.</returns>
        public static byte[] CopyRowsContiguous(ReadOnlySpan<byte> source, int sourceRowPitch, int width, int height, int rowBytes)
        {
            byte[] output = new byte[rowBytes * height];
            for (int y = 0; y < height; y++)
            {
                int sourceOffset = y * sourceRowPitch;
                int destinationOffset = y * rowBytes;
                source.Slice(sourceOffset, rowBytes).CopyTo(output.AsSpan(destinationOffset, rowBytes));
            }

            return output;
        }

        /// <summary>
        /// Copies the first channel from an 8-bit multi-channel image into a single-channel buffer.
        /// </summary>
        /// <param name="source">The source pixel data.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="bytesPerPixel">The number of bytes per source pixel.</param>
        /// <returns>A tightly packed single-channel byte buffer.</returns>
        public static byte[] CopySingleChannel8(ReadOnlySpan<byte> source, int sourceRowPitch, int width, int height, int bytesPerPixel)
        {
            byte[] output = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                int destinationRow = y * width;
                for (int x = 0; x < width; x++)
                    output[destinationRow + x] = source[sourceRow + (x * bytesPerPixel)];
            }

            return output;
        }

        /// <summary>
        /// Copies the first channel from a 16-bit multi-channel image into a single-channel buffer.
        /// </summary>
        /// <param name="source">The source pixel data.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="bytesPerPixel">The number of bytes per source pixel.</param>
        /// <returns>A tightly packed 16-bit single-channel byte buffer.</returns>
        public static byte[] CopySingleChannel16(ReadOnlySpan<byte> source, int sourceRowPitch, int width, int height, int bytesPerPixel)
        {
            byte[] output = new byte[width * height * 2];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                int destinationRow = y * width * 2;
                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = sourceRow + (x * bytesPerPixel);
                    int destinationOffset = destinationRow + (x * 2);
                    source.Slice(sourceOffset, 2).CopyTo(output.AsSpan(destinationOffset, 2));
                }
            }

            return output;
        }

        /// <summary>
        /// Copies 16-bit RGBA pixels into a tightly packed RGB buffer by dropping alpha samples.
        /// </summary>
        /// <param name="source">The source 16-bit RGBA pixel data.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>A tightly packed 16-bit RGB byte buffer.</returns>
        public static byte[] CopyRgb16FromRgba16(ReadOnlySpan<byte> source, int sourceRowPitch, int width, int height)
        {
            byte[] output = new byte[width * height * 6];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                int destinationRow = y * width * 6;
                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = sourceRow + (x * 8);
                    int destinationOffset = destinationRow + (x * 6);
                    source.Slice(sourceOffset, 6).CopyTo(output.AsSpan(destinationOffset, 6));
                }
            }

            return output;
        }

        /// <summary>
        /// Creates a TIFF bits-per-sample array with the same bit depth repeated for each sample.
        /// </summary>
        /// <param name="bitsPerSample">The bit depth to repeat.</param>
        /// <param name="sampleCount">The number of sample entries to create.</param>
        /// <returns>An array containing <paramref name="sampleCount"/> copies of <paramref name="bitsPerSample"/>.</returns>
        public static ushort[] CreateBitsPerSample(ushort bitsPerSample, ushort sampleCount)
        {
            ushort[] values = new ushort[sampleCount];
            for (int i = 0; i < values.Length; i++)
                values[i] = bitsPerSample;

            return values;
        }

        /// <summary>
        /// Determines whether an 8-bit grayscale-plus-alpha image contains any non-opaque alpha values.
        /// </summary>
        /// <param name="source">The source grayscale-plus-alpha pixel data.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns><see langword="true"/> when any alpha sample is below full opacity; otherwise <see langword="false"/>.</returns>
        public static bool HasNonOpaqueGrayAlpha8(ReadOnlySpan<byte> source, int sourceRowPitch, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                for (int x = 0; x < width; x++)
                {
                    if (source[sourceRow + (x * 2) + 1] < 255)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a 16-bit grayscale-plus-alpha image contains any non-opaque alpha values.
        /// </summary>
        /// <param name="source">The source grayscale-plus-alpha pixel data.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns><see langword="true"/> when any alpha sample is below full opacity; otherwise <see langword="false"/>.</returns>
        public static bool HasNonOpaqueGrayAlpha16(ReadOnlySpan<byte> source, int sourceRowPitch, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                for (int x = 0; x < width; x++)
                {
                    if (Read16Native(source, sourceRow + (x * 4) + 2) < ushort.MaxValue)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a 16-bit RGBA image contains any non-opaque alpha values.
        /// </summary>
        /// <param name="source">The source RGBA pixel data.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns><see langword="true"/> when any alpha sample is below full opacity; otherwise <see langword="false"/>.</returns>
        public static bool HasNonOpaqueAlpha16(ReadOnlySpan<byte> source, int sourceRowPitch, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                for (int x = 0; x < width; x++)
                {
                    if (Read16Native(source, sourceRow + (x * 8) + 6) < ushort.MaxValue)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts RGB or RGBA sample data from an RGBA-formatted source buffer.
        /// </summary>
        /// <param name="source">The source RGBA pixel data.</param>
        /// <param name="output">The destination RGB or RGBA buffer.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="destinationRowBytes">The byte stride of each destination row.</param>
        /// <param name="channels">The number of channels to write per destination pixel.</param>
        /// <param name="includeAlpha"><see langword="true"/> to include alpha samples in the output; otherwise RGB only.</param>
        public static void ExtractFromRgba(
            ReadOnlySpan<byte> source,
            Span<byte> output,
            int width,
            int height,
            int sourceRowPitch,
            int destinationRowBytes,
            int channels,
            bool includeAlpha)
        {
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                int destinationRow = y * destinationRowBytes;

                if (includeAlpha)
                {
                    source.Slice(sourceRow, destinationRowBytes).CopyTo(output.Slice(destinationRow, destinationRowBytes));
                }
                else
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sourceOffset = sourceRow + (x * 4);
                        int destinationOffset = destinationRow + (x * 3);
                        output[destinationOffset + 0] = source[sourceOffset + 0];
                        output[destinationOffset + 1] = source[sourceOffset + 1];
                        output[destinationOffset + 2] = source[sourceOffset + 2];
                    }
                }
            }
        }

        /// <summary>
        /// Extracts RGB or RGBA sample data from a BGRA-formatted source buffer.
        /// </summary>
        /// <param name="source">The source BGRA pixel data.</param>
        /// <param name="output">The destination RGB or RGBA buffer.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="sourceRowPitch">The byte stride between successive source rows.</param>
        /// <param name="destinationRowBytes">The byte stride of each destination row.</param>
        /// <param name="channels">The number of channels to write per destination pixel.</param>
        /// <param name="includeAlpha"><see langword="true"/> to include alpha samples in the output; otherwise RGB only.</param>
        public static void ExtractFromBgra(
            ReadOnlySpan<byte> source,
            Span<byte> output,
            int width,
            int height,
            int sourceRowPitch,
            int destinationRowBytes,
            int channels,
            bool includeAlpha)
        {
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * sourceRowPitch;
                int destinationRow = y * destinationRowBytes;

                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = sourceRow + (x * 4);
                    int destinationOffset = destinationRow + (x * channels);
                    output[destinationOffset + 0] = source[sourceOffset + 2];
                    output[destinationOffset + 1] = source[sourceOffset + 1];
                    output[destinationOffset + 2] = source[sourceOffset + 0];
                    if (includeAlpha)
                        output[destinationOffset + 3] = source[sourceOffset + 3];
                }
            }
        }

        /// <summary>
        /// Extracts RGB or RGBA sample data by decoding the source image through a pixel codec.
        /// </summary>
        /// <param name="codec">The pixel codec used to decode the source image.</param>
        /// <param name="source">The encoded source pixel data.</param>
        /// <param name="output">The destination RGB or RGBA buffer.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="destinationRowBytes">The byte stride of each destination row.</param>
        /// <param name="channels">The number of channels to write per destination pixel.</param>
        /// <param name="includeAlpha"><see langword="true"/> to include alpha samples in the output; otherwise RGB only.</param>
        public static void ExtractViaCodec(
            IPixelCodec codec,
            ReadOnlySpan<byte> source,
            Span<byte> output,
            int width,
            int height,
            int destinationRowBytes,
            int channels,
            bool includeAlpha)
        {
            const int StripHeight = 4;
            Vector4[] pixels = new Vector4[width * StripHeight];

            for (int stripY = 0; stripY < height; stripY += StripHeight)
            {
                int rows = Math.Min(StripHeight, height - stripY);
                codec.DecodeRows(source, pixels, stripY, rows, width, height);

                for (int row = 0; row < rows; row++)
                {
                    int pixelBase = row * width;
                    int destinationRow = (stripY + row) * destinationRowBytes;

                    for (int x = 0; x < width; x++)
                    {
                        Vector4 pixel = pixels[pixelBase + x];
                        int destinationOffset = destinationRow + (x * channels);
                        output[destinationOffset + 0] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
                        output[destinationOffset + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
                        output[destinationOffset + 2] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
                        if (includeAlpha)
                            output[destinationOffset + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
                    }
                }
            }
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer using the specified byte order.
        /// </summary>
        /// <param name="data">The source byte data.</param>
        /// <param name="offset">The byte offset at which to read.</param>
        /// <param name="littleEndian"><see langword="true"/> for little-endian byte order; otherwise big-endian.</param>
        /// <returns>The decoded 16-bit value.</returns>
        public static ushort Read16(ReadOnlySpan<byte> data, int offset, bool littleEndian)
        {
            return littleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
                : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer using the current platform byte order.
        /// </summary>
        /// <param name="data">The source byte data.</param>
        /// <param name="offset">The byte offset at which to read.</param>
        /// <returns>The decoded 16-bit value.</returns>
        public static ushort Read16Native(ReadOnlySpan<byte> data, int offset)
        {
            return BitConverter.IsLittleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
                : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
        }
    }
}
