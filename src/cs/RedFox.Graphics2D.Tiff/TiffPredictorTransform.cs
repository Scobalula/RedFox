using System.Buffers.Binary;

namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Applies and reverses TIFF horizontal differencing predictor transforms.
    /// </summary>
    public static class TiffPredictorTransform
    {
        /// <summary>
        /// Applies TIFF horizontal differencing to a span of interleaved sample data.
        /// </summary>
        /// <param name="data">The row-major interleaved sample data to transform.</param>
        /// <param name="width">The width of each row in pixels.</param>
        /// <param name="rows">The number of rows represented in <paramref name="data"/>.</param>
        /// <param name="samplesPerPixel">The number of samples stored for each pixel.</param>
        /// <param name="bitsPerSample">The number of bits in each sample.</param>
        /// <param name="littleEndian"><see langword="true"/> when 16-bit samples are encoded in little-endian order.</param>
        public static void ApplyHorizontalDifferencing(Span<byte> data, int width, int rows, int samplesPerPixel, int bitsPerSample, bool littleEndian)
        {
            TransformHorizontalDifferencing(data, width, rows, samplesPerPixel, bitsPerSample, littleEndian, encode: true);
        }

        /// <summary>
        /// Reverses TIFF horizontal differencing on a span of interleaved sample data.
        /// </summary>
        /// <param name="data">The row-major interleaved sample data to restore.</param>
        /// <param name="width">The width of each row in pixels.</param>
        /// <param name="rows">The number of rows represented in <paramref name="data"/>.</param>
        /// <param name="samplesPerPixel">The number of samples stored for each pixel.</param>
        /// <param name="bitsPerSample">The number of bits in each sample.</param>
        /// <param name="littleEndian"><see langword="true"/> when 16-bit samples are encoded in little-endian order.</param>
        public static void UndoHorizontalDifferencing(Span<byte> data, int width, int rows, int samplesPerPixel, int bitsPerSample, bool littleEndian)
        {
            TransformHorizontalDifferencing(data, width, rows, samplesPerPixel, bitsPerSample, littleEndian, encode: false);
        }

        /// <summary>
        /// Applies or reverses TIFF horizontal differencing across a block of interleaved sample data.
        /// </summary>
        /// <param name="data">The row-major interleaved sample data to transform.</param>
        /// <param name="width">The width of each row in pixels.</param>
        /// <param name="rows">The number of rows represented in <paramref name="data"/>.</param>
        /// <param name="samplesPerPixel">The number of samples stored for each pixel.</param>
        /// <param name="bitsPerSample">The number of bits in each sample.</param>
        /// <param name="littleEndian"><see langword="true"/> when 16-bit samples use little-endian byte order.</param>
        /// <param name="encode"><see langword="true"/> to apply the predictor; <see langword="false"/> to reverse it.</param>
        public static void TransformHorizontalDifferencing(
            Span<byte> data,
            int width,
            int rows,
            int samplesPerPixel,
            int bitsPerSample,
            bool littleEndian,
            bool encode)
        {
            if (width <= 1 || rows <= 0 || samplesPerPixel <= 0)
                return;

            if (bitsPerSample is not (8 or 16))
                throw new NotSupportedException($"TIFF horizontal predictor is not supported for {bitsPerSample}-bit samples.");

            int bytesPerSample = bitsPerSample / 8;
            int bytesPerPixel = samplesPerPixel * bytesPerSample;
            int rowBytes = width * bytesPerPixel;

            for (int row = 0; row < rows; row++)
            {
                Span<byte> rowSpan = data.Slice(row * rowBytes, rowBytes);

                if (bitsPerSample == 8)
                {
                    TransformRow8(rowSpan, bytesPerPixel, encode);
                }
                else
                {
                    TransformRow16(rowSpan, bytesPerPixel, bytesPerSample, littleEndian, encode);
                }
            }
        }

        /// <summary>
        /// Applies or reverses TIFF horizontal differencing for a single 8-bit sample row.
        /// </summary>
        /// <param name="rowSpan">The row data to transform in place.</param>
        /// <param name="bytesPerPixel">The number of bytes that make up one pixel in <paramref name="rowSpan"/>.</param>
        /// <param name="encode"><see langword="true"/> to apply the predictor; <see langword="false"/> to reverse it.</param>
        public static void TransformRow8(Span<byte> rowSpan, int bytesPerPixel, bool encode)
        {
            if (encode)
            {
                for (int offset = rowSpan.Length - 1; offset >= bytesPerPixel; offset--)
                    rowSpan[offset] = (byte)(rowSpan[offset] - rowSpan[offset - bytesPerPixel]);
            }
            else
            {
                for (int offset = bytesPerPixel; offset < rowSpan.Length; offset++)
                    rowSpan[offset] = (byte)(rowSpan[offset] + rowSpan[offset - bytesPerPixel]);
            }
        }

        /// <summary>
        /// Applies or reverses TIFF horizontal differencing for a single 16-bit sample row.
        /// </summary>
        /// <param name="rowSpan">The row data to transform in place.</param>
        /// <param name="bytesPerPixel">The number of bytes that make up one pixel in <paramref name="rowSpan"/>.</param>
        /// <param name="bytesPerSample">The number of bytes that make up a single sample.</param>
        /// <param name="littleEndian"><see langword="true"/> when 16-bit samples use little-endian byte order.</param>
        /// <param name="encode"><see langword="true"/> to apply the predictor; <see langword="false"/> to reverse it.</param>
        public static void TransformRow16(Span<byte> rowSpan, int bytesPerPixel, int bytesPerSample, bool littleEndian, bool encode)
        {
            if (encode)
            {
                for (int offset = rowSpan.Length - bytesPerSample; offset >= bytesPerPixel; offset -= bytesPerSample)
                {
                    ushort current = ReadUInt16(rowSpan, offset, littleEndian);
                    ushort previous = ReadUInt16(rowSpan, offset - bytesPerPixel, littleEndian);
                    WriteUInt16(rowSpan, offset, (ushort)(current - previous), littleEndian);
                }
            }
            else
            {
                for (int offset = bytesPerPixel; offset < rowSpan.Length; offset += bytesPerSample)
                {
                    ushort current = ReadUInt16(rowSpan, offset, littleEndian);
                    ushort previous = ReadUInt16(rowSpan, offset - bytesPerPixel, littleEndian);
                    WriteUInt16(rowSpan, offset, (ushort)(current + previous), littleEndian);
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
        public static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian)
        {
            return littleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
                : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
        }

        /// <summary>
        /// Writes a 16-bit unsigned integer using the specified byte order.
        /// </summary>
        /// <param name="data">The destination byte buffer.</param>
        /// <param name="offset">The byte offset at which to write.</param>
        /// <param name="value">The 16-bit value to write.</param>
        /// <param name="littleEndian"><see langword="true"/> for little-endian byte order; otherwise big-endian.</param>
        public static void WriteUInt16(Span<byte> data, int offset, ushort value, bool littleEndian)
        {
            if (littleEndian)
                BinaryPrimitives.WriteUInt16LittleEndian(data[offset..], value);
            else
                BinaryPrimitives.WriteUInt16BigEndian(data[offset..], value);
        }
    }
}