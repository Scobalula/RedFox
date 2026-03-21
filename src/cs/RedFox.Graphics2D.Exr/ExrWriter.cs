using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Serializes 2D images to single-part scanline OpenEXR files.
    /// </summary>
    public static class ExrWriter
    {
        private static readonly ExrChannel[] HalfChannels =
        [
            new("A", ExrPixelType.Half, false, 1, 1),
            new("B", ExrPixelType.Half, false, 1, 1),
            new("G", ExrPixelType.Half, false, 1, 1),
            new("R", ExrPixelType.Half, false, 1, 1),
        ];

        private static readonly ExrChannel[] FloatChannels =
        [
            new("A", ExrPixelType.Float, false, 1, 1),
            new("B", ExrPixelType.Float, false, 1, 1),
            new("G", ExrPixelType.Float, false, 1, 1),
            new("R", ExrPixelType.Float, false, 1, 1),
        ];

        /// <summary>
        /// Writes an image to an EXR file at the specified path.
        /// </summary>
        public static void Save(string filePath, Image image, ExrWriteOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            Save(stream, image, options);
        }

        /// <summary>
        /// Writes an image to an EXR stream.
        /// </summary>
        public static void Save(Stream stream, Image image, ExrWriteOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(image);

            ValidateImageShape(image);

            options ??= new ExrWriteOptions();

            ExrCompressionType compression = ResolveCompression(options.Compression);
            ExrPixelType pixelType = ResolvePixelType(image.Format, options.PixelType);

            if (pixelType != ExrPixelType.Half && compression is ExrCompressionType.B44 or ExrCompressionType.B44A)
                throw new NotSupportedException("B44 and B44A compression require HALF channel output.");

            ref readonly var slice = ref image.GetSlice(0, 0, 0);
            IReadOnlyList<ExrChannel> channels = pixelType == ExrPixelType.Half ? HalfChannels : FloatChannels;
            Vector4[]? decodedPixels = RequiresDecodedPixels(slice.Format, pixelType)
                ? DecodePixels(slice, image.Format)
                : null;

            byte[] header = BuildHeader(slice.Width, slice.Height, compression, channels);
            int linesPerBlock = ExrFileLayout.GetLinesPerBlock(compression);
            int chunkCount = (slice.Height + linesPerBlock - 1) / linesPerBlock;
            int offsetTableLength = checked(chunkCount * sizeof(ulong));
            var chunks = new List<byte[]>(chunkCount);
            var offsets = new ulong[chunkCount];
            int nextChunkOffset = checked(header.Length + offsetTableLength);

            for (int blockIndex = 0; blockIndex < chunkCount; blockIndex++)
            {
                int firstRow = blockIndex * linesPerBlock;
                int rowsInBlock = Math.Min(linesPerBlock, slice.Height - firstRow);
                byte[] chunk = CreateChunk(slice, firstRow, rowsInBlock, compression, channels, decodedPixels);
                chunks.Add(chunk);
                offsets[blockIndex] = (ulong)nextChunkOffset;
                nextChunkOffset += chunk.Length;
            }

            stream.Write(header);

            foreach (ulong offset in offsets)
                WriteUInt64(stream, offset);

            foreach (byte[] chunk in chunks)
                stream.Write(chunk);
        }

        /// <summary>
        /// Validates that the image can be represented as a simple 2D EXR file.
        /// </summary>
        private static void ValidateImageShape(Image image)
        {
            if (image.Depth != 1 || image.ArraySize != 1 || image.MipLevels != 1)
                throw new NotSupportedException("EXR writing currently supports only single-slice 2D images without mipmaps.");
        }

        /// <summary>
        /// Decodes the source image into RGBA float pixels for EXR channel packing.
        /// </summary>
        private static Vector4[] DecodePixels(ImageSlice slice, ImageFormat format)
        {
            IPixelCodec codec = PixelCodecRegistry.GetCodec(format);
            var pixels = new Vector4[checked(slice.Width * slice.Height)];
            codec.Decode(slice.PixelSpan, pixels, slice.Width, slice.Height);
            return pixels;
        }

        /// <summary>
        /// Determines whether the source image must be decoded to Vector4 pixels before channel packing.
        /// </summary>
        private static bool RequiresDecodedPixels(ImageFormat format, ExrPixelType pixelType)
        {
            return (format, pixelType) switch
            {
                (ImageFormat.R32G32B32A32Float, ExrPixelType.Float) => false,
                (ImageFormat.R16G16B16A16Float, ExrPixelType.Half) => false,
                _ => true,
            };
        }

        /// <summary>
        /// Resolves the public write compression option to the internal EXR compression code.
        /// </summary>
        private static ExrCompressionType ResolveCompression(ExrWriteCompression compression)
        {
            return compression switch
            {
                ExrWriteCompression.None => ExrCompressionType.None,
                ExrWriteCompression.Rle => ExrCompressionType.Rle,
                ExrWriteCompression.Zips => ExrCompressionType.Zips,
                ExrWriteCompression.Zip => ExrCompressionType.Zip,
                ExrWriteCompression.Pxr24 => ExrCompressionType.Pxr24,
                ExrWriteCompression.B44 => ExrCompressionType.B44,
                ExrWriteCompression.B44A => ExrCompressionType.B44A,
                _ => throw new NotSupportedException($"EXR compression mode '{compression}' is not supported for writing."),
            };
        }

        /// <summary>
        /// Resolves the channel sample type written for the image.
        /// </summary>
        private static ExrPixelType ResolvePixelType(ImageFormat format, ExrWritePixelType pixelType)
        {
            return pixelType switch
            {
                ExrWritePixelType.Auto when format == ImageFormat.R16G16B16A16Float => ExrPixelType.Half,
                ExrWritePixelType.Auto => ExrPixelType.Float,
                ExrWritePixelType.Half => ExrPixelType.Half,
                ExrWritePixelType.Float => ExrPixelType.Float,
                _ => throw new NotSupportedException($"EXR pixel type '{pixelType}' is not supported for writing."),
            };
        }

        /// <summary>
        /// Builds the EXR header attribute block.
        /// </summary>
        private static byte[] BuildHeader(int width, int height, ExrCompressionType compression, IReadOnlyList<ExrChannel> channels)
        {
            using var stream = new MemoryStream();
            WriteUInt32(stream, ExrFileLayout.Magic);
            WriteUInt32(stream, ExrFileLayout.Version);

            WriteAttribute(stream, "channels", "chlist", BuildChannels(channels));
            WriteAttribute(stream, "compression", "compression", [(byte)compression]);
            WriteAttribute(stream, "dataWindow", "box2i", BuildBox2I(0, 0, width - 1, height - 1));
            WriteAttribute(stream, "displayWindow", "box2i", BuildBox2I(0, 0, width - 1, height - 1));
            WriteAttribute(stream, "lineOrder", "lineOrder", [0]);
            WriteAttribute(stream, "pixelAspectRatio", "float", BuildFloat(1.0f));
            WriteAttribute(stream, "screenWindowCenter", "v2f", BuildVector2(0.0f, 0.0f));
            WriteAttribute(stream, "screenWindowWidth", "float", BuildFloat(1.0f));
            stream.WriteByte(0);
            return stream.ToArray();
        }

        /// <summary>
        /// Creates a single scanline chunk for the requested block range.
        /// </summary>
        private static byte[] CreateChunk(ImageSlice slice, int firstRow, int rowsInBlock, ExrCompressionType compression, IReadOnlyList<ExrChannel> channels, Vector4[]? decodedPixels)
        {
            byte[] rawBlock = BuildBlockData(slice, firstRow, rowsInBlock, channels, decodedPixels);
            byte[] packedBlock = compression switch
            {
                ExrCompressionType.None => rawBlock,
                ExrCompressionType.Rle => ExrCompression.CompressRle(rawBlock),
                ExrCompressionType.Zips => ExrCompression.CompressZip(rawBlock),
                ExrCompressionType.Zip => ExrCompression.CompressZip(rawBlock),
                ExrCompressionType.Pxr24 => ExrPxr24Compression.Encode(rawBlock, channels, slice.Width, rowsInBlock),
                ExrCompressionType.B44 => ExrB44Compression.Encode(rawBlock, channels, slice.Width, rowsInBlock, flatFields: false),
                ExrCompressionType.B44A => ExrB44Compression.Encode(rawBlock, channels, slice.Width, rowsInBlock, flatFields: true),
                _ => throw new NotSupportedException($"EXR compression mode '{compression}' is not supported for writing."),
            };

            using var stream = new MemoryStream();
            WriteInt32(stream, firstRow);
            WriteInt32(stream, packedBlock.Length);
            stream.Write(packedBlock);
            return stream.ToArray();
        }

        /// <summary>
        /// Builds the raw scanline-interleaved payload for a scanline block.
        /// </summary>
        private static byte[] BuildBlockData(ImageSlice slice, int firstRow, int rowsInBlock, IReadOnlyList<ExrChannel> channels, Vector4[]? decodedPixels)
        {
            if (decodedPixels is null)
            {
                return (slice.Format, channels[0].PixelType) switch
                {
                    (ImageFormat.R32G32B32A32Float, ExrPixelType.Float) => BuildFloatBlock(slice, firstRow, rowsInBlock),
                    (ImageFormat.R16G16B16A16Float, ExrPixelType.Half) => BuildHalfBlock(slice, firstRow, rowsInBlock),
                    _ => throw new InvalidOperationException("EXR writer fast path could not resolve a source packing strategy."),
                };
            }

            return BuildDecodedBlockData(slice.Width, firstRow, rowsInBlock, channels, decodedPixels);
        }

        /// <summary>
        /// Builds the raw scanline-interleaved payload from pre-decoded RGBA float pixels.
        /// Per the EXR spec, each scanline contains all channel data in alphabetical order.
        /// </summary>
        private static byte[] BuildDecodedBlockData(int width, int firstRow, int rowsInBlock, IReadOnlyList<ExrChannel> channels, IReadOnlyList<Vector4> pixels)
        {
            var block = new byte[ExrFileLayout.CalculateBlockSize(channels, width, rowsInBlock)];
            int offset = 0;

            for (int row = 0; row < rowsInBlock; row++)
            {
                int rowOffset = (firstRow + row) * width;

                foreach (var channel in channels)
                {
                    for (int column = 0; column < width; column++)
                    {
                        float sample = SelectChannelValue(pixels[rowOffset + column], channel.Name);
                        offset += WriteSample(block.AsSpan(offset), sample, channel.PixelType);
                    }
                }
            }

            return block;
        }

        /// <summary>
        /// Packs a FLOAT RGBA source image directly into EXR scanline-interleaved
        /// layout without intermediate Vector4 decoding. Each scanline contains
        /// channels in alphabetical order: A, B, G, R.
        /// </summary>
        private static byte[] BuildFloatBlock(ImageSlice slice, int firstRow, int rowsInBlock)
        {
            int width = slice.Width;
            int pixelCount = width * rowsInBlock;
            var block = new byte[pixelCount * sizeof(float) * 4];
            ReadOnlySpan<float> source = slice.GetPixelsAs<float>();
            Span<float> destination = MemoryMarshal.Cast<byte, float>(block.AsSpan());

            // Scanline-interleaved: for each row, write A row, B row, G row, R row.
            int destOffset = 0;
            for (int row = 0; row < rowsInBlock; row++)
            {
                int sourceRowOffset = ((firstRow + row) * width) * 4;

                // Channel A (alpha)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 3];

                // Channel B (blue)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 2];

                // Channel G (green)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 1];

                // Channel R (red)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 0];
            }

            return block;
        }

        /// <summary>
        /// Packs a HALF RGBA source image directly into EXR scanline-interleaved
        /// layout without widening to float first. Each scanline contains
        /// channels in alphabetical order: A, B, G, R.
        /// </summary>
        private static byte[] BuildHalfBlock(ImageSlice slice, int firstRow, int rowsInBlock)
        {
            int width = slice.Width;
            int pixelCount = width * rowsInBlock;
            var block = new byte[pixelCount * sizeof(ushort) * 4];
            ReadOnlySpan<ushort> source = MemoryMarshal.Cast<byte, ushort>(slice.PixelSpan);
            Span<ushort> destination = MemoryMarshal.Cast<byte, ushort>(block.AsSpan());

            int destOffset = 0;
            for (int row = 0; row < rowsInBlock; row++)
            {
                int sourceRowOffset = ((firstRow + row) * width) * 4;

                // Channel A (alpha)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 3];

                // Channel B (blue)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 2];

                // Channel G (green)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 1];

                // Channel R (red)
                for (int column = 0; column < width; column++)
                    destination[destOffset++] = source[sourceRowOffset + column * 4 + 0];
            }

            return block;
        }

        /// <summary>
        /// Selects the source component for an EXR channel name.
        /// </summary>
        private static float SelectChannelValue(Vector4 pixel, string channelName)
        {
            return channelName switch
            {
                "R" => pixel.X,
                "G" => pixel.Y,
                "B" => pixel.Z,
                "A" => pixel.W,
                _ => throw new NotSupportedException($"EXR channel '{channelName}' is not supported for writing."),
            };
        }

        /// <summary>
        /// Writes one sample to the destination buffer and returns the byte count written.
        /// </summary>
        private static int WriteSample(Span<byte> destination, float value, ExrPixelType pixelType)
        {
            switch (pixelType)
            {
                case ExrPixelType.Half:
                    BinaryPrimitives.WriteUInt16LittleEndian(destination, BitConverter.HalfToUInt16Bits((Half)value));
                    return sizeof(ushort);

                case ExrPixelType.Float:
                    BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
                    return sizeof(float);

                default:
                    throw new NotSupportedException($"EXR pixel type '{pixelType}' is not supported for writing.");
            }
        }

        /// <summary>
        /// Builds the EXR channel list payload.
        /// </summary>
        private static byte[] BuildChannels(IReadOnlyList<ExrChannel> channels)
        {
            using var stream = new MemoryStream();

            foreach (var channel in channels)
            {
                WriteCString(stream, channel.Name);
                WriteInt32(stream, (int)channel.PixelType);
                stream.WriteByte(channel.IsLinear ? (byte)1 : (byte)0);
                stream.Write([0, 0, 0]);
                WriteInt32(stream, channel.XSampling);
                WriteInt32(stream, channel.YSampling);
            }

            stream.WriteByte(0);
            return stream.ToArray();
        }

        /// <summary>
        /// Builds a serialized EXR box2i value.
        /// </summary>
        private static byte[] BuildBox2I(int minX, int minY, int maxX, int maxY)
        {
            using var stream = new MemoryStream();
            WriteInt32(stream, minX);
            WriteInt32(stream, minY);
            WriteInt32(stream, maxX);
            WriteInt32(stream, maxY);
            return stream.ToArray();
        }

        /// <summary>
        /// Builds a serialized EXR float value.
        /// </summary>
        private static byte[] BuildFloat(float value)
        {
            using var stream = new MemoryStream();
            WriteSingle(stream, value);
            return stream.ToArray();
        }

        /// <summary>
        /// Builds a serialized EXR v2f value.
        /// </summary>
        private static byte[] BuildVector2(float x, float y)
        {
            using var stream = new MemoryStream();
            WriteSingle(stream, x);
            WriteSingle(stream, y);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes a single EXR attribute.
        /// </summary>
        private static void WriteAttribute(Stream stream, string name, string type, byte[] value)
        {
            WriteCString(stream, name);
            WriteCString(stream, type);
            WriteInt32(stream, value.Length);
            stream.Write(value);
        }

        /// <summary>
        /// Writes a null-terminated ASCII string.
        /// </summary>
        private static void WriteCString(Stream stream, string value)
        {
            stream.Write(System.Text.Encoding.ASCII.GetBytes(value));
            stream.WriteByte(0);
        }

        /// <summary>
        /// Writes a 32-bit signed integer in little-endian format.
        /// </summary>
        private static void WriteInt32(Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer in little-endian format.
        /// </summary>
        private static void WriteUInt32(Stream stream, uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 64-bit unsigned integer in little-endian format.
        /// </summary>
        private static void WriteUInt64(Stream stream, ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 32-bit float in little-endian format.
        /// </summary>
        private static void WriteSingle(Stream stream, float value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(float)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, BitConverter.SingleToInt32Bits(value));
            stream.Write(buffer);
        }
    }
}