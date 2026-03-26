using System.Buffers.Binary;

namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Reconstructs PXR24-compressed scanline blocks.
    /// </summary>
    public static class ExrPxr24Compression
    {
        /// <summary>
        /// Encodes a channel-major scanline block using the PXR24 transform
        /// and zlib compression.
        /// </summary>
        /// <param name="rawData">The uncompressed channel-major block data.</param>
        /// <param name="channels">The channel list from the EXR header.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="rowsInBlock">The number of scanlines in the block.</param>
        /// <returns>The PXR24-compressed byte array.</returns>
        public static byte[] Encode(ReadOnlySpan<byte> rawData, IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock)
        {
            int[] planeOffsets = CalculatePlaneOffsets(channels, width, rowsInBlock);
            using var output = new MemoryStream();

            for (int row = 0; row < rowsInBlock; row++)
            {
                for (int channelIndex = 0; channelIndex < channels.Count; channelIndex++)
                {
                    var channel = channels[channelIndex];
                    int inputOffset = planeOffsets[channelIndex] + row * width * GetOutputBytesPerSample(channel.PixelType);

                    switch (channel.PixelType)
                    {
                        case ExrPixelType.Uint:
                            EncodeUIntChannel(output, rawData.Slice(inputOffset, width * sizeof(uint)), width);
                            break;

                        case ExrPixelType.Half:
                            EncodeHalfChannel(output, rawData.Slice(inputOffset, width * sizeof(ushort)), width);
                            break;

                        case ExrPixelType.Float:
                            EncodeFloatChannel(output, rawData.Slice(inputOffset, width * sizeof(float)), width);
                            break;

                        default:
                            throw new NotSupportedException($"EXR pixel type '{channel.PixelType}' is not supported.");
                    }
                }
            }

            return ExrCompression.CompressZlib(output.ToArray());
        }

        /// <summary>
        /// Decodes a PXR24-compressed block into the standard channel-major byte layout.
        /// </summary>
        /// <param name="packedData">The compressed block data.</param>
        /// <param name="channels">The channel list from the EXR header.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="rowsInBlock">The number of scanlines in the block.</param>
        /// <param name="expectedSize">The expected decompressed byte count.</param>
        /// <returns>The decoded channel-major byte array.</returns>
        public static byte[] Decode(ReadOnlySpan<byte> packedData, IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock, int expectedSize)
        {
            int transformedSize = CalculateTransformedSize(channels, width, rowsInBlock);
            byte[] transformed = ExrCompression.DecompressZlib(packedData, transformedSize);
            var output = new byte[expectedSize];
            int[] planeOffsets = CalculatePlaneOffsets(channels, width, rowsInBlock);

            int transformedOffset = 0;

            for (int row = 0; row < rowsInBlock; row++)
            {
                for (int channelIndex = 0; channelIndex < channels.Count; channelIndex++)
                {
                    var channel = channels[channelIndex];
                    int outputOffset = planeOffsets[channelIndex] + row * width * GetOutputBytesPerSample(channel.PixelType);

                    switch (channel.PixelType)
                    {
                        case ExrPixelType.Uint:
                            DecodeUIntChannel(transformed, ref transformedOffset, output, ref outputOffset, width);
                            break;

                        case ExrPixelType.Half:
                            DecodeHalfChannel(transformed, ref transformedOffset, output, ref outputOffset, width);
                            break;

                        case ExrPixelType.Float:
                            DecodeFloatChannel(transformed, ref transformedOffset, output, ref outputOffset, width);
                            break;

                        default:
                            throw new NotSupportedException($"EXR pixel type '{channel.PixelType}' is not supported.");
                    }
                }
            }

            if (transformedOffset != transformed.Length)
                throw new InvalidDataException("PXR24 EXR block size did not match the expected channel payload size.");

            return output;
        }

        /// <summary>
        /// Computes the zlib-expanded byte count for a PXR24 block.
        /// </summary>
        private static int CalculateTransformedSize(IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock)
        {
            int total = 0;

            foreach (var channel in channels)
            {
                int bytesPerSample = channel.PixelType switch
                {
                    ExrPixelType.Uint => 4,
                    ExrPixelType.Half => 2,
                    ExrPixelType.Float => 3,
                    _ => throw new NotSupportedException($"EXR pixel type '{channel.PixelType}' is not supported."),
                };

                total = checked(total + width * rowsInBlock * bytesPerSample);
            }

            return total;
        }

        /// <summary>
        /// Computes the destination offset of each decoded channel plane.
        /// </summary>
        private static int[] CalculatePlaneOffsets(IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock)
        {
            var offsets = new int[channels.Count];
            int currentOffset = 0;

            for (int index = 0; index < channels.Count; index++)
            {
                offsets[index] = currentOffset;
                currentOffset = checked(currentOffset + width * rowsInBlock * GetOutputBytesPerSample(channels[index].PixelType));
            }

            return offsets;
        }

        /// <summary>
        /// Gets the byte count written per decoded sample.
        /// </summary>
        private static int GetOutputBytesPerSample(ExrPixelType pixelType)
        {
            return pixelType switch
            {
                ExrPixelType.Uint => 4,
                ExrPixelType.Half => 2,
                ExrPixelType.Float => 4,
                _ => throw new NotSupportedException($"EXR pixel type '{pixelType}' is not supported."),
            };
        }

        /// <summary>
        /// Writes the transformed UINT channel bytes for a single scanline.
        /// </summary>
        private static void EncodeUIntChannel(Stream stream, ReadOnlySpan<byte> input, int width)
        {
            byte[] plane0 = new byte[width];
            byte[] plane1 = new byte[width];
            byte[] plane2 = new byte[width];
            byte[] plane3 = new byte[width];
            uint previous = 0;

            for (int column = 0; column < width; column++)
            {
                uint pixel = BinaryPrimitives.ReadUInt32LittleEndian(input[(column * sizeof(uint))..]);
                uint diff = pixel - previous;
                previous = pixel;
                plane0[column] = (byte)(diff >> 24);
                plane1[column] = (byte)(diff >> 16);
                plane2[column] = (byte)(diff >> 8);
                plane3[column] = (byte)diff;
            }

            stream.Write(plane0);
            stream.Write(plane1);
            stream.Write(plane2);
            stream.Write(plane3);
        }

        /// <summary>
        /// Writes the transformed HALF channel bytes for a single scanline.
        /// </summary>
        private static void EncodeHalfChannel(Stream stream, ReadOnlySpan<byte> input, int width)
        {
            byte[] plane0 = new byte[width];
            byte[] plane1 = new byte[width];
            uint previous = 0;

            for (int column = 0; column < width; column++)
            {
                uint pixel = BinaryPrimitives.ReadUInt16LittleEndian(input[(column * sizeof(ushort))..]);
                uint diff = pixel - previous;
                previous = pixel;
                plane0[column] = (byte)(diff >> 8);
                plane1[column] = (byte)diff;
            }

            stream.Write(plane0);
            stream.Write(plane1);
        }

        /// <summary>
        /// Writes the transformed FLOAT channel bytes for a single scanline.
        /// </summary>
        private static void EncodeFloatChannel(Stream stream, ReadOnlySpan<byte> input, int width)
        {
            byte[] plane0 = new byte[width];
            byte[] plane1 = new byte[width];
            byte[] plane2 = new byte[width];
            uint previous = 0;

            for (int column = 0; column < width; column++)
            {
                uint bits = BinaryPrimitives.ReadUInt32LittleEndian(input[(column * sizeof(float))..]);
                uint pixel = Float32ToFloat24(bits);
                uint diff = pixel - previous;
                previous = pixel;
                plane0[column] = (byte)(diff >> 16);
                plane1[column] = (byte)(diff >> 8);
                plane2[column] = (byte)diff;
            }

            stream.Write(plane0);
            stream.Write(plane1);
            stream.Write(plane2);
        }

        /// <summary>
        /// Converts 32-bit floating-point bits into the 24-bit representation used by PXR24.
        /// </summary>
        private static uint Float32ToFloat24(uint bits)
        {
            uint sign = bits & 0x80000000;
            uint exponent = bits & 0x7F800000;
            uint mantissa = bits & 0x007FFFFF;

            uint payload;
            if (exponent == 0x7F800000)
            {
                if (mantissa == 0)
                {
                    payload = exponent >> 8;
                }
                else
                {
                    mantissa >>= 8;
                    payload = (exponent >> 8) | mantissa | (mantissa == 0 ? 1u : 0u);
                }
            }
            else
            {
                payload = ((exponent | mantissa) + (mantissa & 0x00000080)) >> 8;
                if (payload >= 0x7F8000)
                    payload = (exponent | mantissa) >> 8;
            }

            return (sign >> 8) | payload;
        }

        /// <summary>
        /// Rebuilds a UINT channel from its byte-transposed delta stream.
        /// </summary>
        private static void DecodeUIntChannel(byte[] transformed, ref int transformedOffset, byte[] output, ref int outputOffset, int width)
        {
            ReadOnlySpan<byte> plane0 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;
            ReadOnlySpan<byte> plane1 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;
            ReadOnlySpan<byte> plane2 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;
            ReadOnlySpan<byte> plane3 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;

            uint pixel = 0;
            for (int index = 0; index < width; index++)
            {
                uint diff = ((uint)plane0[index] << 24)
                    | ((uint)plane1[index] << 16)
                    | ((uint)plane2[index] << 8)
                    | plane3[index];

                pixel += diff;
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(outputOffset, sizeof(uint)), pixel);
                outputOffset += sizeof(uint);
            }
        }

        /// <summary>
        /// Rebuilds a HALF channel from its byte-transposed delta stream.
        /// </summary>
        private static void DecodeHalfChannel(byte[] transformed, ref int transformedOffset, byte[] output, ref int outputOffset, int width)
        {
            ReadOnlySpan<byte> plane0 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;
            ReadOnlySpan<byte> plane1 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;

            uint pixel = 0;
            for (int index = 0; index < width; index++)
            {
                uint diff = ((uint)plane0[index] << 8) | plane1[index];
                pixel += diff;
                BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(outputOffset, sizeof(ushort)), (ushort)pixel);
                outputOffset += sizeof(ushort);
            }
        }

        /// <summary>
        /// Rebuilds a FLOAT channel from its 24-bit delta stream.
        /// </summary>
        private static void DecodeFloatChannel(byte[] transformed, ref int transformedOffset, byte[] output, ref int outputOffset, int width)
        {
            ReadOnlySpan<byte> plane0 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;
            ReadOnlySpan<byte> plane1 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;
            ReadOnlySpan<byte> plane2 = transformed.AsSpan(transformedOffset, width);
            transformedOffset += width;

            uint pixel = 0;
            for (int index = 0; index < width; index++)
            {
                uint diff = ((uint)plane0[index] << 24)
                    | ((uint)plane1[index] << 16)
                    | ((uint)plane2[index] << 8);

                pixel += diff;
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(outputOffset, sizeof(uint)), pixel);
                outputOffset += sizeof(uint);
            }
        }
    }
}
