using System.Buffers.Binary;
using RedFox.Graphics2D.IO;
using static RedFox.Graphics2D.Tiff.TiffConstants;

namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for TIFF (Tagged Image File Format) files.
    /// </summary>
    /// <remarks>
    /// <para><b>Reading:</b> Supports 8-bit and 16-bit grayscale, RGB, and RGBA images
    /// with Uncompressed, LZW, and PackBits compression. Both little-endian and big-endian
    /// byte orders are supported.</para>
    /// <para><b>Writing:</b> Outputs 8-bit RGB or RGBA TIFF files with configurable
    /// compression (None, LZW, or PackBits) via <see cref="EncoderOptions"/>.</para>
    /// </remarks>
    public sealed class TiffImageTranslator : ImageTranslator
    {
        /// <summary>
        /// Gets or sets the encoder options used when writing TIFF files.
        /// </summary>
        public TiffEncoderOptions EncoderOptions { get; set; } = new();

        /// <inheritdoc/>
        public override string Name => "TIFF";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions { get; } = [".tif", ".tiff"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            var data = ReadAllBytes(stream);
            bool le = ParseByteOrder(data);

            uint ifdOffset = TiffIfdReader.ReadUInt32(data, 4, le);
            var tags = TiffIfdReader.ParseIFD(data, ifdOffset, le);

            int width = TiffIfdReader.GetTagInt(tags, TagImageWidth, data, le);
            int height = TiffIfdReader.GetTagInt(tags, TagImageLength, data, le);
            int compression = TiffIfdReader.GetTagInt(tags, TagCompression, data, le, 1);
            int photometric = TiffIfdReader.GetTagInt(tags, TagPhotometric, data, le, PhotometricRGB);
            int samplesPerPixel = TiffIfdReader.GetTagInt(tags, TagSamplesPerPixel, data, le, 1);
            int rowsPerStrip = TiffIfdReader.GetTagInt(tags, TagRowsPerStrip, data, le, height);

            if (width <= 0 || height <= 0)
                throw new InvalidDataException($"Invalid TIFF dimensions: {width}x{height}.");

            int bitsPerSample = TiffIfdReader.GetTagInt(tags, TagBitsPerSample, data, le, 8);
            if (bitsPerSample is not (8 or 16))
                throw new NotSupportedException($"Unsupported TIFF bits per sample: {bitsPerSample}. Only 8 and 16 are supported.");

            if (compression is not (CompressionNone or CompressionLZW or CompressionPackBits))
                throw new NotSupportedException($"Unsupported TIFF compression: {compression}.");

            var stripOffsets = TiffIfdReader.GetTagUintArray(tags, TagStripOffsets, data, le);
            var stripByteCounts = TiffIfdReader.GetTagUintArray(tags, TagStripByteCounts, data, le);

            if (stripOffsets.Length == 0 || stripByteCounts.Length == 0)
                throw new InvalidDataException("TIFF missing strip offsets or byte counts.");

            // Decode all strips into a contiguous raw pixel buffer.
            int bytesPerSample = bitsPerSample / 8;
            int srcRowBytes = width * samplesPerPixel * bytesPerSample;
            var rawPixels = new byte[srcRowBytes * height];
            int rowsDecoded = 0;

            for (int i = 0; i < stripOffsets.Length && rowsDecoded < height; i++)
            {
                int stripRows = Math.Min(rowsPerStrip, height - rowsDecoded);
                int stripRawSize = stripRows * srcRowBytes;
                var compressedData = data.AsSpan((int)stripOffsets[i], (int)stripByteCounts[i]);

                var decompressed = compression switch
                {
                    CompressionNone => compressedData.ToArray(),
                    CompressionLZW => TiffDecompressor.DecompressLZW(compressedData, stripRawSize),
                    CompressionPackBits => TiffDecompressor.DecompressPackBits(compressedData, stripRawSize),
                    _ => throw new NotSupportedException()
                };

                decompressed.AsSpan(0, Math.Min(decompressed.Length, stripRawSize))
                    .CopyTo(rawPixels.AsSpan(rowsDecoded * srcRowBytes));
                rowsDecoded += stripRows;
            }

            // Convert decoded samples to RGBA8.
            var output = new byte[width * height * 4];

            if (bitsPerSample == 8)
                TiffPixelConverter.ConvertToRgba8(rawPixels, output, width * height, samplesPerPixel, photometric);
            else
                TiffPixelConverter.ConvertToRgba16(rawPixels, output, width * height, samplesPerPixel, photometric, le);

            return new Image(width, height, ImageFormat.R8G8B8A8Unorm, output);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image)
        {
            ref readonly var slice = ref image.GetSlice(0, 0, 0);
            int width = slice.Width;
            int height = slice.Height;
            var format = image.Format;
            var src = slice.PixelSpan;

            // Determine whether the image actually uses alpha.
            bool hasAlpha = false;
            if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb
                       or ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb)
            {
                hasAlpha = TiffPixelConverter.HasNonOpaqueAlpha(src);
            }

            int samplesPerPixel = hasAlpha ? 4 : 3;
            int rowBytes = width * samplesPerPixel;

            // Extract pixel data as interleaved 8-bit RGB or RGBA.
            var pixelData = TiffPixelConverter.ExtractRgbData(slice, format, width, height, hasAlpha);

            // Optionally compress the pixel data.
            ushort compressionCode = (ushort)EncoderOptions.Compression;
            byte[] imageData = compressionCode switch
            {
                CompressionLZW => TiffCompressor.CompressLZW(pixelData),
                CompressionPackBits => TiffCompressor.CompressPackBits(pixelData),
                _ => pixelData
            };
            int imageDataSize = imageData.Length;

            // ── File layout ──────────────────────────────
            // [0..7]    Header (8 bytes)
            // [8..N]    IFD (count + entries + next-IFD pointer)
            // [N..]     Rational values, BPS array (if needed), pixel data

            int tagCount = hasAlpha ? 12 : 11; // +ExtraSamples when alpha present
            int ifdSize = 2 + tagCount * 12 + 4;
            int ifdOffset = 8;

            int rationalsOffset = ifdOffset + ifdSize;
            int bpsArrayOffset = rationalsOffset + 16;
            int bpsArraySize = samplesPerPixel > 2 ? samplesPerPixel * 2 : 0;
            int pixelDataOffset = bpsArrayOffset + bpsArraySize;

            // ── Header ───────────────────────────────────
            bool le = BitConverter.IsLittleEndian;
            Span<byte> header = stackalloc byte[8];
            if (le) { header[0] = (byte)'I'; header[1] = (byte)'I'; }
            else    { header[0] = (byte)'M'; header[1] = (byte)'M'; }
            WriteUInt16(header, 2, 42, le);
            WriteUInt32(header, 4, (uint)ifdOffset, le);
            stream.Write(header);

            // ── IFD entries ──────────────────────────────
            var ifd = new byte[ifdSize];
            WriteUInt16(ifd, 0, (ushort)tagCount, le);
            int pos = 2;

            pos = WriteIFDEntry(ifd, pos, TagImageWidth, TypeLong, 1, (uint)width, le);
            pos = WriteIFDEntry(ifd, pos, TagImageLength, TypeLong, 1, (uint)height, le);

            if (samplesPerPixel <= 2)
                pos = WriteIFDEntry(ifd, pos, TagBitsPerSample, TypeShort, (uint)samplesPerPixel, 8, le);
            else
                pos = WriteIFDEntryOffset(ifd, pos, TagBitsPerSample, TypeShort, (uint)samplesPerPixel, (uint)bpsArrayOffset, le);

            pos = WriteIFDEntry(ifd, pos, TagCompression, TypeShort, 1, compressionCode, le);
            pos = WriteIFDEntry(ifd, pos, TagPhotometric, TypeShort, 1, PhotometricRGB, le);
            pos = WriteIFDEntryOffset(ifd, pos, TagStripOffsets, TypeLong, 1, (uint)pixelDataOffset, le);
            pos = WriteIFDEntry(ifd, pos, TagSamplesPerPixel, TypeShort, 1, (uint)samplesPerPixel, le);
            pos = WriteIFDEntry(ifd, pos, TagRowsPerStrip, TypeLong, 1, (uint)height, le);
            pos = WriteIFDEntry(ifd, pos, TagStripByteCounts, TypeLong, 1, (uint)imageDataSize, le);
            pos = WriteIFDEntryOffset(ifd, pos, TagXResolution, TypeRational, 1, (uint)rationalsOffset, le);
            pos = WriteIFDEntryOffset(ifd, pos, TagYResolution, TypeRational, 1, (uint)(rationalsOffset + 8), le);

            if (hasAlpha)
                pos = WriteIFDEntry(ifd, pos, TagExtraSamples, TypeShort, 1, 2, le); // 2 = unassociated alpha

            WriteUInt32(ifd, pos, 0, le); // Next IFD = 0 (single image)
            stream.Write(ifd);

            // ── Rational values (72 DPI) ─────────────────
            Span<byte> rationals = stackalloc byte[16];
            WriteUInt32(rationals, 0, 72, le);
            WriteUInt32(rationals, 4, 1, le);
            WriteUInt32(rationals, 8, 72, le);
            WriteUInt32(rationals, 12, 1, le);
            stream.Write(rationals);

            // ── BitsPerSample array ──────────────────────
            if (samplesPerPixel > 2)
            {
                Span<byte> bpsArray = stackalloc byte[samplesPerPixel * 2];
                for (int i = 0; i < samplesPerPixel; i++)
                    WriteUInt16(bpsArray, i * 2, 8, le);
                stream.Write(bpsArray);
            }

            // ── Pixel data ──────────────────────────────
            stream.Write(imageData);
        }

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            if (!IsValid(filePath, extension) || header.Length < 4)
                return false;

            if (header[0] == (byte)'I' && header[1] == (byte)'I')
                return BinaryPrimitives.ReadUInt16LittleEndian(header[2..]) == 42;
            if (header[0] == (byte)'M' && header[1] == (byte)'M')
                return BinaryPrimitives.ReadUInt16BigEndian(header[2..]) == 42;

            return false;
        }

        // ──────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────

        /// <summary>Reads the entire stream into a byte array.</summary>
        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream is MemoryStream ms && ms.TryGetBuffer(out var buffer))
                return buffer.Array!;

            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        /// <summary>Determines byte order from the first two bytes of the TIFF header.</summary>
        private static bool ParseByteOrder(ReadOnlySpan<byte> data)
        {
            if (data[0] == (byte)'I' && data[1] == (byte)'I') return true;
            if (data[0] == (byte)'M' && data[1] == (byte)'M') return false;
            throw new InvalidDataException("Not a valid TIFF file.");
        }

        // ──────────────────────────────────────────────
        // Binary write helpers
        // ──────────────────────────────────────────────

        /// <summary>Writes a 16-bit unsigned integer with the specified byte order.</summary>
        private static void WriteUInt16(Span<byte> buf, int offset, ushort value, bool le)
        {
            if (le) BinaryPrimitives.WriteUInt16LittleEndian(buf[offset..], value);
            else BinaryPrimitives.WriteUInt16BigEndian(buf[offset..], value);
        }

        /// <summary>Writes a 32-bit unsigned integer with the specified byte order.</summary>
        private static void WriteUInt32(Span<byte> buf, int offset, uint value, bool le)
        {
            if (le) BinaryPrimitives.WriteUInt32LittleEndian(buf[offset..], value);
            else BinaryPrimitives.WriteUInt32BigEndian(buf[offset..], value);
        }

        private static int WriteIFDEntry(Span<byte> ifd, int pos, ushort tag, ushort type, uint count, uint value, bool le)
        {
            WriteUInt16(ifd, pos, tag, le);
            WriteUInt16(ifd, pos + 2, type, le);
            WriteUInt32(ifd, pos + 4, count, le);

            if (type == TypeShort && count <= 2)
            {
                WriteUInt16(ifd, pos + 8, (ushort)value, le);
                WriteUInt16(ifd, pos + 10, count == 2 ? (ushort)value : (ushort)0, le);
            }
            else
            {
                WriteUInt32(ifd, pos + 8, value, le);
            }

            return pos + 12;
        }

        /// <summary>
        /// Writes a 12-byte IFD entry whose value is stored at an external offset.
        /// </summary>
        private static int WriteIFDEntryOffset(Span<byte> ifd, int pos, ushort tag, ushort type, uint count, uint offset, bool le)
        {
            WriteUInt16(ifd, pos, tag, le);
            WriteUInt16(ifd, pos + 2, type, le);
            WriteUInt32(ifd, pos + 4, count, le);
            WriteUInt32(ifd, pos + 8, offset, le);
            return pos + 12;
        }
    }
}
