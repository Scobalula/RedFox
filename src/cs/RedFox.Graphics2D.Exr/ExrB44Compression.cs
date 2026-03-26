using System.Buffers.Binary;

namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Reconstructs B44 and B44A compressed scanline blocks.
    /// </summary>
    public static class ExrB44Compression
    {
        /// <summary>
        /// Encodes a channel-major scanline block using the B44 or B44A HALF block layout.
        /// </summary>
        /// <param name="rawData">The uncompressed channel-major block data.</param>
        /// <param name="channels">The channel list from the EXR header.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="rowsInBlock">The number of scanlines in the block.</param>
        /// <param name="flatFields">Whether to use B44A flat-field mode for constant blocks.</param>
        /// <returns>The B44-encoded byte array.</returns>
        public static byte[] Encode(ReadOnlySpan<byte> rawData, IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock, bool flatFields)
        {
            using var output = new MemoryStream();
            int sourceOffset = 0;

            foreach (var channel in channels)
            {
                int bytesPerSample = ExrFileLayout.GetBytesPerSample(channel.PixelType);
                int planeLength = checked(width * rowsInBlock * bytesPerSample);
                ReadOnlySpan<byte> plane = rawData.Slice(sourceOffset, planeLength);
                sourceOffset += planeLength;

                if (channel.PixelType != ExrPixelType.Half)
                {
                    output.Write(plane);
                    continue;
                }

                if (channel.IsLinear)
                    throw new NotSupportedException("B44 writing does not support perceptually linear EXR channels.");

                EncodeHalfPlane(output, plane, width, rowsInBlock, flatFields);
            }

            if (sourceOffset != rawData.Length)
                throw new InvalidDataException("B44 EXR block size did not match the expected channel payload size.");

            return output.ToArray();
        }

        /// <summary>
        /// Decodes a B44 or B44A compressed block into channel-major byte layout.
        /// </summary>
        /// <param name="packedData">The compressed block data.</param>
        /// <param name="channels">The channel list from the EXR header.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="rowsInBlock">The number of scanlines in the block.</param>
        /// <param name="expectedSize">The expected decompressed byte count.</param>
        /// <returns>The decoded channel-major byte array.</returns>
        public static byte[] Decode(ReadOnlySpan<byte> packedData, IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock, int expectedSize)
        {
            var scratch = new byte[expectedSize];
            int sourceOffset = 0;
            int scratchOffset = 0;

            foreach (var channel in channels)
            {
                int bytesPerSample = channel.PixelType switch
                {
                    ExrPixelType.Uint => 4,
                    ExrPixelType.Half => 2,
                    ExrPixelType.Float => 4,
                    _ => throw new NotSupportedException($"EXR pixel type '{channel.PixelType}' is not supported."),
                };

                int planeLength = checked(width * rowsInBlock * bytesPerSample);

                if (channel.PixelType != ExrPixelType.Half)
                {
                    if (sourceOffset + planeLength > packedData.Length)
                        throw new InvalidDataException("B44 EXR block ended before the uncompressed non-HALF payload completed.");

                    packedData.Slice(sourceOffset, planeLength).CopyTo(scratch.AsSpan(scratchOffset, planeLength));
                    sourceOffset += planeLength;
                    scratchOffset += planeLength;
                    continue;
                }

                DecodeHalfPlane(
                    packedData,
                    ref sourceOffset,
                    scratch.AsSpan(scratchOffset, planeLength),
                    width,
                    rowsInBlock,
                    channel.IsLinear);

                scratchOffset += planeLength;
            }

            if (sourceOffset != packedData.Length)
                throw new InvalidDataException("B44 EXR block size did not match the expected channel payload size.");

            return scratch;
        }

        /// <summary>
        /// Decodes a HALF plane stored as a sequence of 4x4 B44 blocks.
        /// </summary>
        private static void DecodeHalfPlane(ReadOnlySpan<byte> packedData, ref int sourceOffset, Span<byte> plane, int width, int rowsInBlock, bool isLinear)
        {
            ushort[] block = new ushort[16];

            for (int row = 0; row < rowsInBlock; row += 4)
            {
                for (int column = 0; column < width; column += 4)
                {
                    if (sourceOffset + 3 > packedData.Length)
                        throw new InvalidDataException("B44 EXR block ended before the compressed HALF payload completed.");

                    ReadOnlySpan<byte> encoded = packedData[sourceOffset..];
                    if (encoded[2] >= (13 << 2))
                    {
                        Unpack3(encoded, block);
                        sourceOffset += 3;
                    }
                    else
                    {
                        if (sourceOffset + 14 > packedData.Length)
                            throw new InvalidDataException("B44 EXR block ended before the compressed HALF payload completed.");

                        Unpack14(encoded, block);
                        sourceOffset += 14;
                    }

                    if (isLinear)
                        ConvertToLinear(block);

                    int validRows = Math.Min(4, rowsInBlock - row);
                    int validColumns = Math.Min(4, width - column);

                    for (int blockRow = 0; blockRow < validRows; blockRow++)
                    {
                        int destinationRow = (row + blockRow) * width;

                        for (int blockColumn = 0; blockColumn < validColumns; blockColumn++)
                        {
                            ushort value = block[blockRow * 4 + blockColumn];
                            int destinationOffset = (destinationRow + column + blockColumn) * sizeof(ushort);
                            BinaryPrimitives.WriteUInt16LittleEndian(plane[destinationOffset..], value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Encodes a HALF plane as a sequence of 4x4 B44 blocks.
        /// </summary>
        private static void EncodeHalfPlane(Stream stream, ReadOnlySpan<byte> plane, int width, int rowsInBlock, bool flatFields)
        {
            var block = new ushort[16];

            for (int row = 0; row < rowsInBlock; row += 4)
            {
                for (int column = 0; column < width; column += 4)
                {
                    BuildHalfBlock(plane, width, rowsInBlock, row, column, block);
                    byte[] encoded = PackB44Block(block, flatFields);
                    stream.Write(encoded);
                }
            }
        }

        /// <summary>
        /// Builds a padded 4x4 HALF block for B44 packing.
        /// </summary>
        private static void BuildHalfBlock(ReadOnlySpan<byte> plane, int width, int rowsInBlock, int row, int column, ushort[] block)
        {
            for (int blockRow = 0; blockRow < 4; blockRow++)
            {
                int sourceRow = Math.Min(row + blockRow, rowsInBlock - 1);
                int rowOffset = sourceRow * width;

                for (int blockColumn = 0; blockColumn < 4; blockColumn++)
                {
                    int sourceColumn = Math.Min(column + blockColumn, width - 1);
                    int sourceOffset = (rowOffset + sourceColumn) * sizeof(ushort);
                    block[blockRow * 4 + blockColumn] = BinaryPrimitives.ReadUInt16LittleEndian(plane[sourceOffset..]);
                }
            }
        }

        /// <summary>
        /// Converts B44 log-encoded values back to linear HALF values.
        /// </summary>
        private static void ConvertToLinear(ushort[] block)
        {
            for (int index = 0; index < block.Length; index++)
                block[index] = ConvertToLinear(block[index]);
        }

        /// <summary>
        /// Converts a single B44 log-encoded HALF value back to linear space.
        /// </summary>
        private static ushort ConvertToLinear(ushort value)
        {
            if ((value & 0x7C00) == 0x7C00)
                return 0;
            if (value > 0x8000)
                return 0;

            float sample = (float)BitConverter.UInt16BitsToHalf(value);
            if (sample <= 0f)
                return 0;

            return BitConverter.HalfToUInt16Bits((Half)(8f * MathF.Log(sample)));
        }

        /// <summary>
        /// Unpacks the standard 14-byte B44 block representation.
        /// </summary>
        private static void Unpack14(ReadOnlySpan<byte> block, ushort[] output)
        {
            ushort shift = (ushort)(block[2] >> 2);
            uint bias = (uint)(0x20u << shift);

            output[0] = (ushort)((block[0] << 8) | block[1]);
            output[4] = (ushort)((uint)output[0] + (((uint)((block[2] << 4) | (block[3] >> 4)) & 0x3Fu) << shift) - bias);
            output[8] = (ushort)((uint)output[4] + (((uint)((block[3] << 2) | (block[4] >> 6)) & 0x3Fu) << shift) - bias);
            output[12] = (ushort)((uint)output[8] + (((uint)block[4] & 0x3Fu) << shift) - bias);

            output[1] = (ushort)((uint)output[0] + ((uint)(block[5] >> 2) << shift) - bias);
            output[5] = (ushort)((uint)output[4] + (((uint)((block[5] << 4) | (block[6] >> 4)) & 0x3Fu) << shift) - bias);
            output[9] = (ushort)((uint)output[8] + (((uint)((block[6] << 2) | (block[7] >> 6)) & 0x3Fu) << shift) - bias);
            output[13] = (ushort)((uint)output[12] + (((uint)block[7] & 0x3Fu) << shift) - bias);

            output[2] = (ushort)((uint)output[1] + ((uint)(block[8] >> 2) << shift) - bias);
            output[6] = (ushort)((uint)output[5] + (((uint)((block[8] << 4) | (block[9] >> 4)) & 0x3Fu) << shift) - bias);
            output[10] = (ushort)((uint)output[9] + (((uint)((block[9] << 2) | (block[10] >> 6)) & 0x3Fu) << shift) - bias);
            output[14] = (ushort)((uint)output[13] + (((uint)block[10] & 0x3Fu) << shift) - bias);

            output[3] = (ushort)((uint)output[2] + ((uint)(block[11] >> 2) << shift) - bias);
            output[7] = (ushort)((uint)output[6] + (((uint)((block[11] << 4) | (block[12] >> 4)) & 0x3Fu) << shift) - bias);
            output[11] = (ushort)((uint)output[10] + (((uint)((block[12] << 2) | (block[13] >> 6)) & 0x3Fu) << shift) - bias);
            output[15] = (ushort)((uint)output[14] + (((uint)block[13] & 0x3Fu) << shift) - bias);

            for (int index = 0; index < output.Length; index++)
            {
                if ((output[index] & 0x8000) != 0)
                    output[index] &= 0x7FFF;
                else
                    output[index] = (ushort)~output[index];
            }
        }

        /// <summary>
        /// Unpacks the compact 3-byte B44A flat-field representation.
        /// </summary>
        private static void Unpack3(ReadOnlySpan<byte> block, ushort[] output)
        {
            ushort value = (ushort)((block[0] << 8) | block[1]);
            value = (value & 0x8000) != 0 ? (ushort)(value & 0x7FFF) : (ushort)~value;

            for (int index = 0; index < output.Length; index++)
                output[index] = value;
        }

        /// <summary>
        /// Packs a single HALF block using the OpenEXR B44/B44A layout.
        /// </summary>
        private static byte[] PackB44Block(ushort[] source, bool flatFields)
        {
            int[] differences = new int[16];
            int[] runs = new int[15];
            ushort[] transformed = new ushort[16];

            for (int index = 0; index < source.Length; index++)
            {
                ushort value = source[index];
                transformed[index] = (value & 0x7C00) == 0x7C00
                    ? (ushort)0x8000
                    : (value & 0x8000) != 0
                        ? (ushort)~value
                        : (ushort)(value | 0x8000);
            }

            ushort maximum = transformed.Max();
            int shift = -1;
            int minRun;
            int maxRun;

            do
            {
                shift++;

                for (int index = 0; index < transformed.Length; index++)
                    differences[index] = ShiftAndRound(maximum - transformed[index], shift);

                runs[0] = differences[0] - differences[4] + 0x20;
                runs[1] = differences[4] - differences[8] + 0x20;
                runs[2] = differences[8] - differences[12] + 0x20;
                runs[3] = differences[0] - differences[1] + 0x20;
                runs[4] = differences[4] - differences[5] + 0x20;
                runs[5] = differences[8] - differences[9] + 0x20;
                runs[6] = differences[12] - differences[13] + 0x20;
                runs[7] = differences[1] - differences[2] + 0x20;
                runs[8] = differences[5] - differences[6] + 0x20;
                runs[9] = differences[9] - differences[10] + 0x20;
                runs[10] = differences[13] - differences[14] + 0x20;
                runs[11] = differences[2] - differences[3] + 0x20;
                runs[12] = differences[6] - differences[7] + 0x20;
                runs[13] = differences[10] - differences[11] + 0x20;
                runs[14] = differences[14] - differences[15] + 0x20;

                minRun = runs.Min();
                maxRun = runs.Max();
            }
            while (minRun < 0 || maxRun > 0x3F);

            if (flatFields && minRun == 0x20 && maxRun == 0x20)
            {
                return
                [
                    (byte)(transformed[0] >> 8),
                    (byte)transformed[0],
                    0xFC,
                ];
            }

            var packed = new byte[14];
            packed[0] = (byte)(transformed[0] >> 8);
            packed[1] = (byte)transformed[0];
            packed[2] = (byte)((shift << 2) | (runs[0] >> 4));
            packed[3] = (byte)((runs[0] << 4) | (runs[1] >> 2));
            packed[4] = (byte)((runs[1] << 6) | runs[2]);
            packed[5] = (byte)((runs[3] << 2) | (runs[4] >> 4));
            packed[6] = (byte)((runs[4] << 4) | (runs[5] >> 2));
            packed[7] = (byte)((runs[5] << 6) | runs[6]);
            packed[8] = (byte)((runs[7] << 2) | (runs[8] >> 4));
            packed[9] = (byte)((runs[8] << 4) | (runs[9] >> 2));
            packed[10] = (byte)((runs[9] << 6) | runs[10]);
            packed[11] = (byte)((runs[11] << 2) | (runs[12] >> 4));
            packed[12] = (byte)((runs[12] << 4) | (runs[13] >> 2));
            packed[13] = (byte)((runs[13] << 6) | runs[14]);
            return packed;
        }

        /// <summary>
        /// Rounds a shifted integer using the same rule as the OpenEXR B44 packer.
        /// </summary>
        private static int ShiftAndRound(int value, int shift)
        {
            value <<= 1;
            int adjustment = (1 << shift) - 1;
            shift += 1;
            int tieBit = (value >> shift) & 1;
            return (value + adjustment + tieBit) >> shift;
        }
    }
}
