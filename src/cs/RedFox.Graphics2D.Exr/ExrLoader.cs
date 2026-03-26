using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Loads single-part scanline OpenEXR images into <see cref="Image"/> instances.
    /// </summary>
    public static class ExrLoader
    {
        private const uint SupportedVersion = 2;
        private const uint TiledFlag = 0x00000200;
        private const uint NonImageFlag = 0x00000800;
        private const uint MultipartFlag = 0x00001000;

        /// <summary>
        /// Loads an EXR file from the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the EXR file.</param>
        /// <returns>An <see cref="Image"/> containing the decoded pixel data.</returns>
        public static Image Load(string filePath)
        {
            var data = File.ReadAllBytes(filePath);
            return Load(data);
        }

        /// <summary>
        /// Loads an EXR file from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the EXR data.</param>
        /// <returns>An <see cref="Image"/> containing the decoded pixel data.</returns>
        public static Image Load(Stream stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return Load(copy.ToArray());
        }

        /// <summary>
        /// Loads an EXR file from a byte buffer.
        /// </summary>
        /// <param name="data">The raw EXR file bytes.</param>
        /// <returns>An <see cref="Image"/> containing the decoded pixel data.</returns>
        public static Image Load(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var source = data.AsSpan();
            int offset = 0;

            uint magic = ReadUInt32(source, ref offset);
            if (magic != ExrFileLayout.Magic)
                throw new InvalidDataException("Not a valid OpenEXR file.");

            uint versionField = ReadUInt32(source, ref offset);
            uint version = versionField & 0xFF;
            uint flags = versionField & ~0xFFu;

            if (version != SupportedVersion)
                throw new NotSupportedException($"Unsupported OpenEXR version {version}.");
            if ((flags & (TiledFlag | NonImageFlag | MultipartFlag)) != 0)
                throw new NotSupportedException("Only scanline, single-part OpenEXR images are supported.");

            var header = ReadHeader(source, ref offset);
            int linesPerBlock = ExrFileLayout.GetLinesPerBlock(header.Compression);
            int chunkCount = (header.Height + linesPerBlock - 1) / linesPerBlock;

            if (offset + chunkCount * sizeof(ulong) > source.Length)
                throw new InvalidDataException("EXR offset table extends beyond the file length.");

            var chunkOffsets = new ulong[chunkCount];
            for (int index = 0; index < chunkOffsets.Length; index++)
                chunkOffsets[index] = ReadUInt64(source, ref offset);

            var pixels = new byte[checked(header.Width * header.Height * sizeof(float) * 4)];
            var output = MemoryMarshal.Cast<byte, float>(pixels.AsSpan());
            InitializeAlpha(output);

            foreach (ulong chunkOffset in chunkOffsets)
                ReadChunk(source, header, linesPerBlock, chunkOffset, output);

            return new Image(header.Width, header.Height, ImageFormat.R32G32B32A32Float, pixels);
        }

        /// <summary>
        /// Reads the EXR header attribute list.
        /// </summary>
        private static ExrHeader ReadHeader(ReadOnlySpan<byte> source, ref int offset)
        {
            ExrCompressionType? compression = null;
            ExrBox2I? dataWindow = null;
            List<ExrChannel>? channels = null;
            bool hasTilesAttribute = false;

            while (true)
            {
                string attributeName = ReadNullTerminatedString(source, ref offset);
                if (attributeName.Length == 0)
                    break;

                string attributeType = ReadNullTerminatedString(source, ref offset);
                int attributeSize = ReadInt32(source, ref offset);
                ReadOnlySpan<byte> attributeValue = ReadBytes(source, ref offset, attributeSize);

                switch (attributeName)
                {
                    case "compression":
                        compression = ReadCompression(attributeValue);
                        break;

                    case "dataWindow":
                        dataWindow = ReadBox2I(attributeValue, attributeName);
                        break;

                    case "channels":
                        channels = ReadChannels(attributeValue);
                        break;

                    case "tiles":
                        hasTilesAttribute = true;
                        break;

                    default:
                        _ = attributeType;
                        break;
                }
            }

            if (hasTilesAttribute)
                throw new NotSupportedException("Tiled OpenEXR images are not supported.");
            if (!compression.HasValue)
                throw new InvalidDataException("EXR file is missing the compression attribute.");
            if (!dataWindow.HasValue)
                throw new InvalidDataException("EXR file is missing the dataWindow attribute.");
            if (channels is null || channels.Count == 0)
                throw new InvalidDataException("EXR file is missing channel definitions.");

            var box = dataWindow.Value;
            int width = checked(box.MaxX - box.MinX + 1);
            int height = checked(box.MaxY - box.MinY + 1);

            if (width <= 0 || height <= 0)
                throw new InvalidDataException("EXR dataWindow must define a positive image size.");

            return new ExrHeader(width, height, box.MinX, box.MinY, compression.Value, channels);
        }

        /// <summary>
        /// Reads a single scanline chunk and writes its decoded samples into the output buffer.
        /// </summary>
        private static void ReadChunk(ReadOnlySpan<byte> source, ExrHeader header, int linesPerBlock, ulong chunkOffset, Span<float> output)
        {
            if (chunkOffset > int.MaxValue)
                throw new InvalidDataException("EXR chunk offset exceeds the supported file size range.");

            int offset = (int)chunkOffset;
            if (offset < 0 || offset + 8 > source.Length)
                throw new InvalidDataException("EXR chunk header extends beyond the file length.");

            int yCoordinate = ReadInt32(source, ref offset);
            int packedSize = ReadInt32(source, ref offset);
            ReadOnlySpan<byte> packedData = ReadBytes(source, ref offset, packedSize);

            int firstRow = yCoordinate - header.MinY;
            if (firstRow < 0 || firstRow >= header.Height)
                throw new InvalidDataException("EXR chunk y coordinate is outside the dataWindow.");

            int rowsInBlock = Math.Min(linesPerBlock, header.Height - firstRow);
            int uncompressedSize = ExrFileLayout.CalculateBlockSize(header.Channels, header.Width, rowsInBlock);
            byte[] unpackedData = UnpackChunk(header.Compression, packedData, header, rowsInBlock, uncompressedSize);

            WriteChannelsToOutput(unpackedData, header, firstRow, rowsInBlock, output);
        }

        /// <summary>
        /// Decodes the per-scanline channel payload into RGBA float pixels.
        /// The EXR spec defines the uncompressed block layout as scanline-
        /// interleaved: for each scanline, the channel data appears in
        /// alphabetical channel-name order.
        /// </summary>
        private static void WriteChannelsToOutput(byte[] unpackedData, ExrHeader header, int firstRow, int rowsInBlock, Span<float> output)
        {
            int width = header.Width;

            // Compute the byte stride of a single scanline (all channels).
            int scanlineStride = 0;
            foreach (var channel in header.Channels)
                scanlineStride += width * ExrFileLayout.GetBytesPerSample(channel.PixelType);

            for (int row = 0; row < rowsInBlock; row++)
            {
                int rowByteOffset = row * scanlineStride;
                int channelByteOffset = 0;

                foreach (var channel in header.Channels)
                {
                    int bytesPerSample = ExrFileLayout.GetBytesPerSample(channel.PixelType);
                    int channelRowLength = width * bytesPerSample;

                    WriteChannelPlane(
                        unpackedData.AsSpan(rowByteOffset + channelByteOffset, channelRowLength),
                        channel,
                        width,
                        firstRow + row,
                        1,
                        output);

                    channelByteOffset += channelRowLength;
                }
            }
        }

        /// <summary>
        /// Writes a single channel plane into the RGBA output buffer.
        /// </summary>
        private static void WriteChannelPlane(ReadOnlySpan<byte> plane, ExrChannel channel, int width, int firstRow, int rowsInBlock, Span<float> output)
        {
            ChannelTarget target = GetChannelTarget(channel.Name);
            if (target == ChannelTarget.Ignore)
                return;

            int bytesPerSample = ExrFileLayout.GetBytesPerSample(channel.PixelType);

            for (int row = 0; row < rowsInBlock; row++)
            {
                int outputRowStart = ((firstRow + row) * width) * 4;
                int sourceRowStart = row * width * bytesPerSample;

                for (int column = 0; column < width; column++)
                {
                    int sourceIndex = sourceRowStart + column * bytesPerSample;
                    float value = ReadSample(plane[sourceIndex..], channel.PixelType);
                    int pixelIndex = outputRowStart + column * 4;

                    switch (target)
                    {
                        case ChannelTarget.Red:
                            output[pixelIndex + 0] = value;
                            break;

                        case ChannelTarget.Green:
                            output[pixelIndex + 1] = value;
                            break;

                        case ChannelTarget.Blue:
                            output[pixelIndex + 2] = value;
                            break;

                        case ChannelTarget.Alpha:
                            output[pixelIndex + 3] = value;
                            break;

                        case ChannelTarget.Luminance:
                            output[pixelIndex + 0] = value;
                            output[pixelIndex + 1] = value;
                            output[pixelIndex + 2] = value;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Decompresses the chunk payload according to the header compression mode.
        /// When the writer falls back to storing raw (untransformed) data because
        /// compression was not beneficial, the packed size equals the expected size
        /// and no decompression or transform reversal is needed.
        /// </summary>
        private static byte[] UnpackChunk(ExrCompressionType compression, ReadOnlySpan<byte> packedData, ExrHeader header, int rowsInBlock, int expectedSize)
        {
            // When compression is enabled and the writer stored raw data as a
            // fallback (packed size == uncompressed size), the bytes are already
            // in their final channel-major layout with no transforms applied.
            // We detect this case and skip decompression entirely.  Piz, Pxr24,
            // B44, and B44A use their own block-level transforms that never fall
            // back to raw data in this way, so the shortcut only applies to the
            // byte-transform-based codecs (Zip, Zips, Rle).
            if (compression is ExrCompressionType.Rle
                              or ExrCompressionType.Zips
                              or ExrCompressionType.Zip
                && packedData.Length == expectedSize)
            {
                return packedData.ToArray();
            }

            return compression switch
            {
                ExrCompressionType.None => packedData.Length == expectedSize
                    ? packedData.ToArray()
                    : throw new InvalidDataException("Uncompressed EXR chunk size did not match the expected block size."),

                ExrCompressionType.Rle => ExrCompression.DecompressRle(packedData, expectedSize),
                ExrCompressionType.Zips => ExrCompression.DecompressZip(packedData, expectedSize),
                ExrCompressionType.Zip => ExrCompression.DecompressZip(packedData, expectedSize),
                ExrCompressionType.Piz => ReorderChannelMajorToScanlineInterleaved(
                    ExrPizCompression.Decode(packedData, header.Channels, header.Width, rowsInBlock, expectedSize),
                    header.Channels, header.Width, rowsInBlock),
                ExrCompressionType.Pxr24 => ReorderChannelMajorToScanlineInterleaved(
                    ExrPxr24Compression.Decode(packedData, header.Channels, header.Width, rowsInBlock, expectedSize),
                    header.Channels, header.Width, rowsInBlock),
                ExrCompressionType.B44 => ReorderChannelMajorToScanlineInterleaved(
                    ExrB44Compression.Decode(packedData, header.Channels, header.Width, rowsInBlock, expectedSize),
                    header.Channels, header.Width, rowsInBlock),
                ExrCompressionType.B44A => ReorderChannelMajorToScanlineInterleaved(
                    ExrB44Compression.Decode(packedData, header.Channels, header.Width, rowsInBlock, expectedSize),
                    header.Channels, header.Width, rowsInBlock),
                _ => throw new NotSupportedException($"EXR compression mode '{compression}' is not supported."),
            };
        }

        /// <summary>
        /// Reorders a channel-major byte buffer into scanline-interleaved layout.
        /// Piz, Pxr24, B44, and B44A decoders produce output where each channel's
        /// full plane is stored contiguously. The rest of the loader expects
        /// scanline-interleaved layout where each scanline contains all channels
        /// in sequence.
        /// </summary>
        private static byte[] ReorderChannelMajorToScanlineInterleaved(byte[] channelMajor, IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock)
        {
            // Compute per-channel row byte widths and the total scanline stride.
            Span<int> channelRowLengths = channels.Count <= 8
                ? stackalloc int[channels.Count]
                : new int[channels.Count];

            int scanlineStride = 0;
            for (int c = 0; c < channels.Count; c++)
            {
                channelRowLengths[c] = width * ExrFileLayout.GetBytesPerSample(channels[c].PixelType);
                scanlineStride += channelRowLengths[c];
            }

            var result = new byte[channelMajor.Length];

            // Walk each channel plane and scatter rows into the interleaved output.
            int planeOffset = 0;
            int channelScanlineOffset = 0;

            for (int c = 0; c < channels.Count; c++)
            {
                int rowLen = channelRowLengths[c];

                for (int row = 0; row < rowsInBlock; row++)
                {
                    int srcOffset = planeOffset + row * rowLen;
                    int dstOffset = row * scanlineStride + channelScanlineOffset;

                    Buffer.BlockCopy(channelMajor, srcOffset, result, dstOffset, rowLen);
                }

                planeOffset += rowLen * rowsInBlock;
                channelScanlineOffset += rowLen;
            }

            return result;
        }

        /// <summary>
        /// Reads the EXR channel list attribute.
        /// </summary>
        private static List<ExrChannel> ReadChannels(ReadOnlySpan<byte> value)
        {
            var channels = new List<ExrChannel>();
            int offset = 0;

            while (true)
            {
                string name = ReadNullTerminatedString(value, ref offset);
                if (name.Length == 0)
                    break;

                var pixelType = (ExrPixelType)ReadInt32(value, ref offset);
                bool isLinear = ReadByte(value, ref offset) != 0;
                offset += 3;
                int xSampling = ReadInt32(value, ref offset);
                int ySampling = ReadInt32(value, ref offset);

                if (xSampling != 1 || ySampling != 1)
                    throw new NotSupportedException($"Subsampled EXR channel '{name}' is not supported.");
                if (!Enum.IsDefined(pixelType))
                    throw new NotSupportedException($"EXR channel '{name}' uses an unsupported pixel type {pixelType}.");

                channels.Add(new ExrChannel(name, pixelType, isLinear, xSampling, ySampling));
            }

            return channels;
        }

        /// <summary>
        /// Reads the EXR compression attribute.
        /// </summary>
        private static ExrCompressionType ReadCompression(ReadOnlySpan<byte> value)
        {
            if (value.Length != 1)
                throw new InvalidDataException("EXR compression attribute must be exactly one byte.");

            var compression = (ExrCompressionType)value[0];
            if (!Enum.IsDefined(compression))
                throw new NotSupportedException($"EXR compression mode value {value[0]} is not recognized.");

            return compression;
        }

        /// <summary>
        /// Reads an EXR box2i structure.
        /// </summary>
        private static ExrBox2I ReadBox2I(ReadOnlySpan<byte> value, string attributeName)
        {
            if (value.Length != 16)
                throw new InvalidDataException($"EXR attribute '{attributeName}' must contain 16 bytes.");

            int offset = 0;
            int minX = ReadInt32(value, ref offset);
            int minY = ReadInt32(value, ref offset);
            int maxX = ReadInt32(value, ref offset);
            int maxY = ReadInt32(value, ref offset);
            return new ExrBox2I(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Maps an EXR channel name to an RGBA destination component.
        /// </summary>
        private static ChannelTarget GetChannelTarget(string name)
        {
            return name.ToUpperInvariant() switch
            {
                "R" => ChannelTarget.Red,
                "G" => ChannelTarget.Green,
                "B" => ChannelTarget.Blue,
                "A" => ChannelTarget.Alpha,
                "Y" => ChannelTarget.Luminance,
                _ => ChannelTarget.Ignore,
            };
        }

        /// <summary>
        /// Reads a single EXR sample value and converts it to a float.
        /// </summary>
        private static float ReadSample(ReadOnlySpan<byte> source, ExrPixelType pixelType)
        {
            return pixelType switch
            {
                ExrPixelType.Uint => BinaryPrimitives.ReadUInt32LittleEndian(source),
                ExrPixelType.Half => (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(source)),
                ExrPixelType.Float => BinaryPrimitives.ReadSingleLittleEndian(source),
                _ => throw new NotSupportedException($"EXR pixel type '{pixelType}' is not supported."),
            };
        }

        /// <summary>
        /// Initializes alpha to one for formats that omit an explicit alpha channel.
        /// </summary>
        private static void InitializeAlpha(Span<float> output)
        {
            if (Vector.IsHardwareAccelerated && output.Length >= Vector<float>.Count)
            {
                Span<float> patternValues = stackalloc float[Vector<float>.Count];
                for (int index = 3; index < patternValues.Length; index += 4)
                    patternValues[index] = 1f;

                var pattern = new Vector<float>(patternValues);
                int offset = 0;

                while (offset <= output.Length - Vector<float>.Count)
                {
                    pattern.CopyTo(output[offset..]);
                    offset += Vector<float>.Count;
                }

                for (int index = offset + 3; index < output.Length; index += 4)
                    output[index] = 1f;

                return;
            }

            for (int index = 3; index < output.Length; index += 4)
                output[index] = 1f;
        }

        /// <summary>
        /// Reads a null-terminated ASCII string.
        /// </summary>
        private static string ReadNullTerminatedString(ReadOnlySpan<byte> source, ref int offset)
        {
            int start = offset;

            while (offset < source.Length && source[offset] != 0)
                offset++;

            if (offset >= source.Length)
                throw new InvalidDataException("EXR string field was not null terminated.");

            string value = System.Text.Encoding.ASCII.GetString(source[start..offset]);
            offset++;
            return value;
        }

        /// <summary>
        /// Reads a single byte.
        /// </summary>
        private static byte ReadByte(ReadOnlySpan<byte> source, ref int offset)
        {
            if ((uint)offset >= (uint)source.Length)
                throw new InvalidDataException("Unexpected end of EXR data while reading a byte.");

            return source[offset++];
        }

        /// <summary>
        /// Reads a 32-bit signed integer.
        /// </summary>
        private static int ReadInt32(ReadOnlySpan<byte> source, ref int offset)
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(source, ref offset, sizeof(int)));
            return value;
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer.
        /// </summary>
        private static uint ReadUInt32(ReadOnlySpan<byte> source, ref int offset)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(source, ref offset, sizeof(uint)));
            return value;
        }

        /// <summary>
        /// Reads a 64-bit unsigned integer.
        /// </summary>
        private static ulong ReadUInt64(ReadOnlySpan<byte> source, ref int offset)
        {
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(source, ref offset, sizeof(ulong)));
            return value;
        }

        /// <summary>
        /// Reads a fixed number of bytes.
        /// </summary>
        private static ReadOnlySpan<byte> ReadBytes(ReadOnlySpan<byte> source, ref int offset, int count)
        {
            if (count < 0 || offset > source.Length - count)
                throw new InvalidDataException("Unexpected end of EXR data while reading a field.");

            var slice = source.Slice(offset, count);
            offset += count;
            return slice;
        }

        /// <summary>
        /// Identifies which RGBA component receives a decoded EXR channel.
        /// </summary>
        private enum ChannelTarget
        {
            Ignore,
            Red,
            Green,
            Blue,
            Alpha,
            Luminance,
        }

    }
}
