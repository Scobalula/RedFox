using System.IO.Compression;

namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Provides decompression helpers for OpenEXR scanline blocks.
    /// </summary>
    public static class ExrCompression
    {
        /// <summary>
        /// Compresses EXR block data with zlib using the smallest output size.
        /// </summary>
        /// <param name="transformedData">The pre-transformed block data to compress.</param>
        /// <returns>The zlib-compressed byte array.</returns>
        public static byte[] CompressZlib(ReadOnlySpan<byte> transformedData)
        {
            return CompressZlib(transformedData, CompressionLevel.SmallestSize);
        }

        /// <summary>
        /// Compresses EXR block data with zlib using the specified compression level.
        /// </summary>
        /// <param name="transformedData">The pre-transformed block data to compress.</param>
        /// <param name="compressionLevel">The zlib compression level to use.</param>
        /// <returns>The zlib-compressed byte array.</returns>
        public static byte[] CompressZlib(ReadOnlySpan<byte> transformedData, CompressionLevel compressionLevel)
        {
            using var output = new MemoryStream();

            using (var zlib = new ZLibStream(output, compressionLevel, leaveOpen: true))
                zlib.Write(transformedData);

            return output.ToArray();
        }

        /// <summary>
        /// Applies the OpenEXR byte shuffle and predictor transforms
        /// used before ZIP and RLE compression.
        /// </summary>
        /// <param name="rawData">The uncompressed block data.</param>
        /// <returns>The transformed byte array ready for compression.</returns>
        public static byte[] ApplyDataTransform(ReadOnlySpan<byte> rawData)
        {
            var shuffled = new byte[rawData.Length];
            int firstHalfLength = (rawData.Length + 1) / 2;

            for (int index = 0; index < rawData.Length; index++)
            {
                if ((index & 1) == 0)
                    shuffled[index / 2] = rawData[index];
                else
                    shuffled[firstHalfLength + index / 2] = rawData[index];
            }

            var predicted = new byte[shuffled.Length];
            if (shuffled.Length > 0)
                predicted[0] = shuffled[0];

            for (int index = 1; index < shuffled.Length; index++)
                predicted[index] = unchecked((byte)(shuffled[index] - shuffled[index - 1] + 128));

            return predicted;
        }

        /// <summary>
        /// Compresses a ZIP or ZIPS EXR block and falls back to raw block data
        /// when compression is not beneficial.
        /// </summary>
        /// <param name="rawData">The uncompressed block data.</param>
        /// <returns>The compressed data, or a copy of the raw data if compression increased the size.</returns>
        public static byte[] CompressZip(ReadOnlySpan<byte> rawData)
        {
            byte[] compressed = CompressZlib(ApplyDataTransform(rawData));
            return compressed.Length < rawData.Length ? compressed : rawData.ToArray();
        }

        /// <summary>
        /// Compresses an EXR block using the RLE encoding defined by the file format.
        /// </summary>
        /// <param name="rawData">The uncompressed block data.</param>
        /// <returns>The RLE-compressed data, or a copy of the raw data if compression increased the size.</returns>
        public static byte[] CompressRle(ReadOnlySpan<byte> rawData)
        {
            ReadOnlySpan<byte> transformed = ApplyDataTransform(rawData);
            using var output = new MemoryStream();
            int offset = 0;

            while (offset < transformed.Length)
            {
                int runLength = CountRunLength(transformed, offset);
                if (runLength >= 2)
                {
                    output.WriteByte(unchecked((byte)(1 - runLength)));
                    output.WriteByte(transformed[offset]);
                    offset += runLength;
                    continue;
                }

                int literalStart = offset++;

                while (offset < transformed.Length)
                {
                    runLength = CountRunLength(transformed, offset);
                    if (runLength >= 2 || offset - literalStart >= 128)
                        break;

                    offset++;
                }

                int literalLength = offset - literalStart;
                output.WriteByte((byte)(literalLength - 1));
                output.Write(transformed.Slice(literalStart, literalLength));
            }

            byte[] compressed = output.ToArray();
            return compressed.Length < rawData.Length ? compressed : rawData.ToArray();
        }

        /// <summary>
        /// Decompresses a zlib-compressed EXR block without applying any post-processing.
        /// </summary>
        /// <param name="packedData">The zlib-compressed block data.</param>
        /// <param name="expectedSize">The expected decompressed byte count.</param>
        /// <returns>The decompressed byte array.</returns>
        public static byte[] DecompressZlib(ReadOnlySpan<byte> packedData, int expectedSize)
        {
            using var source = new MemoryStream(packedData.ToArray(), writable: false);
            using var zlib = new ZLibStream(source, CompressionMode.Decompress);
            var decoded = new byte[expectedSize];
            zlib.ReadExactly(decoded);

            if (zlib.ReadByte() != -1)
                throw new InvalidDataException("ZIP-compressed EXR block produced more data than expected.");

            return decoded;
        }

        /// <summary>
        /// Decompresses a ZIP-compressed EXR block and reverses the EXR byte transforms.
        /// </summary>
        /// <param name="packedData">The zlib-compressed block data.</param>
        /// <param name="expectedSize">The expected decompressed byte count.</param>
        /// <returns>The decompressed and un-transformed byte array.</returns>
        public static byte[] DecompressZip(ReadOnlySpan<byte> packedData, int expectedSize)
        {
            return ReverseDataTransform(DecompressZlib(packedData, expectedSize));
        }

        /// <summary>
        /// Decompresses an RLE-compressed EXR block and reverses the EXR byte transforms.
        /// </summary>
        /// <param name="packedData">The RLE-compressed block data.</param>
        /// <param name="expectedSize">The expected decompressed byte count.</param>
        /// <returns>The decompressed and un-transformed byte array.</returns>
        public static byte[] DecompressRle(ReadOnlySpan<byte> packedData, int expectedSize)
        {
            var decoded = new byte[expectedSize];
            int sourceOffset = 0;
            int destinationOffset = 0;

            while (sourceOffset < packedData.Length && destinationOffset < decoded.Length)
            {
                int count = (sbyte)packedData[sourceOffset++];

                if (count < 0)
                {
                    int runLength = -count + 1;

                    if (sourceOffset >= packedData.Length)
                        throw new InvalidDataException("EXR RLE block ended before the repeated byte value was available.");

                    byte value = packedData[sourceOffset++];

                    if (destinationOffset + runLength > decoded.Length)
                        throw new InvalidDataException("EXR RLE block expanded beyond the expected size.");

                    decoded.AsSpan(destinationOffset, runLength).Fill(value);
                    destinationOffset += runLength;
                    continue;
                }

                int literalLength = count + 1;

                if (sourceOffset + literalLength > packedData.Length)
                    throw new InvalidDataException("EXR RLE block ended before the literal run was complete.");
                if (destinationOffset + literalLength > decoded.Length)
                    throw new InvalidDataException("EXR RLE block expanded beyond the expected size.");

                packedData.Slice(sourceOffset, literalLength).CopyTo(decoded.AsSpan(destinationOffset, literalLength));
                sourceOffset += literalLength;
                destinationOffset += literalLength;
            }

            if (destinationOffset != decoded.Length)
                throw new InvalidDataException("EXR RLE block did not expand to the expected size.");

            return ReverseDataTransform(decoded);
        }

        /// <summary>
        /// Counts the number of repeated bytes in an RLE run.
        /// </summary>
        private static int CountRunLength(ReadOnlySpan<byte> data, int offset)
        {
            int runLength = 1;
            byte value = data[offset];

            while (offset + runLength < data.Length && runLength < 128 && data[offset + runLength] == value)
                runLength++;

            return runLength;
        }

        /// <summary>
        /// Applies the inverse of the OpenEXR predictor and byte-shuffle transforms.
        /// </summary>
        /// <param name="data">The predictor-encoded and shuffled data.</param>
        /// <returns>The restored raw byte array.</returns>
        public static byte[] ReverseDataTransform(ReadOnlySpan<byte> data)
        {
            var predictorDecoded = data.ToArray();

            for (int index = 1; index < predictorDecoded.Length; index++)
                predictorDecoded[index] = unchecked((byte)(predictorDecoded[index - 1] + predictorDecoded[index] - 128));

            var result = new byte[predictorDecoded.Length];
            int secondHalfOffset = (predictorDecoded.Length + 1) / 2;
            int destinationOffset = 0;

            for (int index = 0; index < secondHalfOffset; index++)
            {
                result[destinationOffset++] = predictorDecoded[index];

                int secondIndex = index + secondHalfOffset;
                if (secondIndex < predictorDecoded.Length)
                    result[destinationOffset++] = predictorDecoded[secondIndex];
            }

            return result;
        }
    }
}
