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
    /// with Uncompressed, LZW, Deflate, and PackBits compression. Horizontal predictor
    /// reversal is supported for LZW and Deflate streams. Both little-endian and big-endian
    /// byte orders are supported.</para>
    /// <para><b>Writing:</b> Outputs grayscale, grayscale+alpha, RGB, or RGBA TIFF files
    /// in 8-bit or 16-bit integer formats with configurable compression via
    /// <see cref="EncoderOptions"/>.</para>
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
            int fillOrder = TiffIfdReader.GetTagInt(tags, TagFillOrder, data, le, 1);
            int predictor = TiffIfdReader.GetTagInt(tags, TagPredictor, data, le, (int)TiffPredictor.None);
            int planarConfiguration = TiffIfdReader.GetTagInt(tags, TagPlanarConfiguration, data, le, 1);

            if (width <= 0 || height <= 0)
                throw new InvalidDataException($"Invalid TIFF dimensions: {width}x{height}.");

            if (planarConfiguration != 1)
                throw new NotSupportedException($"Unsupported TIFF planar configuration: {planarConfiguration}.");

            uint[] bitsPerSampleValues = TiffIfdReader.GetTagUintArray(tags, TagBitsPerSample, data, le);
            int bitsPerSample = bitsPerSampleValues.Length > 0 ? (int)bitsPerSampleValues[0] : 8;
            if (bitsPerSample is not (8 or 16))
                throw new NotSupportedException($"Unsupported TIFF bits per sample: {bitsPerSample}. Only 8 and 16 are supported.");

            for (int i = 1; i < bitsPerSampleValues.Length; i++)
            {
                if (bitsPerSampleValues[i] != (uint)bitsPerSample)
                    throw new NotSupportedException("TIFF files with mixed bits-per-sample values are not supported.");
            }

            if (compression is not (CompressionNone or CompressionLZW or CompressionDeflate or CompressionAdobeDeflate or CompressionPackBits))
                throw new NotSupportedException($"Unsupported TIFF compression: {compression}.");

            if (fillOrder is not (1 or 2))
                throw new NotSupportedException($"Unsupported TIFF fill order: {fillOrder}.");

            if (predictor is not ((int)TiffPredictor.None or (int)TiffPredictor.Horizontal))
                throw new NotSupportedException($"Unsupported TIFF predictor: {predictor}.");

            var stripOffsets = TiffIfdReader.GetTagUintArray(tags, TagStripOffsets, data, le);
            var stripByteCounts = TiffIfdReader.GetTagUintArray(tags, TagStripByteCounts, data, le);

            if (stripOffsets.Length == 0)
            {
                uint[] tileOffsets = TiffIfdReader.GetTagUintArray(tags, TagTileOffsets, data, le);
                if (tileOffsets.Length > 0)
                    throw new NotSupportedException("Tiled TIFF images are not currently supported.");
            }

            if (stripOffsets.Length == 0 || stripByteCounts.Length == 0)
                throw new InvalidDataException("TIFF missing strip offsets or byte counts.");

            if (stripByteCounts.Length != stripOffsets.Length && stripByteCounts.Length != 1)
                throw new InvalidDataException("TIFF strip offset and byte count arrays must have matching lengths.");

            // Decode all strips into a contiguous raw pixel buffer.
            int bytesPerSample = bitsPerSample / 8;
            int srcRowBytes = width * samplesPerPixel * bytesPerSample;
            var rawPixels = new byte[srcRowBytes * height];
            int rowsDecoded = 0;

            for (int i = 0; i < stripOffsets.Length && rowsDecoded < height; i++)
            {
                int stripRows = Math.Min(rowsPerStrip, height - rowsDecoded);
                int stripRawSize = stripRows * srcRowBytes;
                int stripByteCountIndex = stripByteCounts.Length == 1 ? 0 : i;
                int stripOffset = checked((int)stripOffsets[i]);
                int stripByteCount = checked((int)stripByteCounts[stripByteCountIndex]);
                ReadOnlySpan<byte> compressedData = GetRange(data, stripOffset, stripByteCount);
                if (fillOrder == 2 && compression == CompressionLZW)
                    compressedData = ReverseBitsPerByte(compressedData);

                var decompressed = compression switch
                {
                    CompressionNone => compressedData.ToArray(),
                    CompressionLZW => TiffDecompressor.DecompressLZW(compressedData, stripRawSize),
                    CompressionDeflate or CompressionAdobeDeflate => TiffDecompressor.DecompressDeflate(compressedData, stripRawSize),
                    CompressionPackBits => TiffDecompressor.DecompressPackBits(compressedData, stripRawSize),
                    _ => throw new NotSupportedException()
                };

                if (predictor == (int)TiffPredictor.Horizontal)
                    TiffPredictorTransform.UndoHorizontalDifferencing(decompressed.AsSpan(), width, stripRows, samplesPerPixel, bitsPerSample, le);

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
            WriteCore(stream, image, EncoderOptions);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image, ImageTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            WriteCore(stream, image, ResolveEncoderOptions(options));
        }

        /// <summary>
        /// Resolves generic image translator options into TIFF-specific encoder options.
        /// </summary>
        /// <param name="options">The generic write options to map into TIFF encoder settings.</param>
        /// <returns>A <see cref="TiffEncoderOptions"/> instance derived from <paramref name="options"/>.</returns>
        public TiffEncoderOptions ResolveEncoderOptions(ImageTranslatorOptions options)
        {
            TiffCompression compression = ResolveCompression(EncoderOptions.Compression, options.Compression);
            TiffPredictor predictor = ResolvePredictor(EncoderOptions.Predictor, options.Compression, compression);

            return new TiffEncoderOptions
            {
                Compression = compression,
                Predictor = predictor,
            };
        }

        /// <summary>
        /// Resolves a generic compression hint into a TIFF compression mode.
        /// </summary>
        /// <param name="defaultCompression">The default TIFF compression mode to use when the hint is <see cref="ImageCompressionPreference.Default"/>.</param>
        /// <param name="compressionPreference">The generic compression hint to interpret.</param>
        /// <returns>The TIFF compression mode that best matches <paramref name="compressionPreference"/>.</returns>
        public static TiffCompression ResolveCompression(TiffCompression defaultCompression, ImageCompressionPreference compressionPreference)
        {
            return compressionPreference switch
            {
                ImageCompressionPreference.None => TiffCompression.None,
                ImageCompressionPreference.Fast => TiffCompression.PackBits,
                ImageCompressionPreference.Balanced => TiffCompression.Deflate,
                ImageCompressionPreference.SmallestSize => TiffCompression.Deflate,
                _ => defaultCompression,
            };
        }

        /// <summary>
        /// Resolves a generic compression hint into the TIFF predictor to use during encoding.
        /// </summary>
        /// <param name="defaultPredictor">The default TIFF predictor to use when the hint is <see cref="ImageCompressionPreference.Default"/>.</param>
        /// <param name="compressionPreference">The generic compression hint to interpret.</param>
        /// <param name="compression">The TIFF compression mode selected for the write operation.</param>
        /// <returns>The TIFF predictor that best matches the requested encoding settings.</returns>
        public static TiffPredictor ResolvePredictor(TiffPredictor defaultPredictor, ImageCompressionPreference compressionPreference, TiffCompression compression)
        {
            if (compressionPreference == ImageCompressionPreference.Default)
                return defaultPredictor;

            return compression is TiffCompression.Deflate or TiffCompression.LZW
                ? TiffPredictor.Horizontal
                : TiffPredictor.None;
        }

        /// <summary>
        /// Writes a TIFF image using explicit TIFF encoder options.
        /// </summary>
        /// <param name="stream">The destination stream that receives the TIFF file data.</param>
        /// <param name="image">The image to encode.</param>
        /// <param name="encoderOptions">The explicit TIFF encoder settings to apply.</param>
        public static void WriteCore(Stream stream, Image image, TiffEncoderOptions encoderOptions)
        {
            ref readonly var slice = ref image.GetSlice(0, 0, 0);
            int width = slice.Width;
            int height = slice.Height;
            TiffEncodedPixelData encodedPixelData = TiffPixelConverter.ExtractEncodedPixelData(slice, image.Format, width, height);
            bool littleEndian = BitConverter.IsLittleEndian;
            ushort compressionCode = (ushort)encoderOptions.Compression;
            bool usePredictor = ShouldApplyPredictor(compressionCode, encodedPixelData, encoderOptions.Predictor);

            byte[] sampleData = encodedPixelData.PixelData;
            if (usePredictor)
            {
                sampleData = sampleData.ToArray();
                TiffPredictorTransform.ApplyHorizontalDifferencing(sampleData, width, height, encodedPixelData.SamplesPerPixel, encodedPixelData.BitsPerSample[0], littleEndian);
            }

            // Optionally compress the pixel data.
            byte[] imageData = compressionCode switch
            {
                CompressionDeflate => TiffCompressor.CompressDeflate(sampleData),
                CompressionLZW => TiffCompressor.CompressLZW(sampleData),
                CompressionPackBits => TiffCompressor.CompressPackBits(sampleData),
                _ => sampleData
            };
            int imageDataSize = imageData.Length;

            // ── File layout ──────────────────────────────
            // [0..7]    Header (8 bytes)
            // [8..N]    IFD (count + entries + next-IFD pointer)
            // [N..]     Rational values, BPS array (if needed), pixel data

            bool writeBitsPerSampleArray = encodedPixelData.BitsPerSample.Length > 2;
            bool hasExtraSamples = encodedPixelData.ExtraSamples.HasValue;
            int tagCount = 12;
            if (hasExtraSamples)
                tagCount++;
            if (usePredictor)
                tagCount++;
            int ifdSize = 2 + tagCount * 12 + 4;
            int ifdOffset = 8;

            int rationalsOffset = ifdOffset + ifdSize;
            int bpsArrayOffset = rationalsOffset + 16;
            int bpsArraySize = writeBitsPerSampleArray ? encodedPixelData.BitsPerSample.Length * 2 : 0;
            int pixelDataOffset = bpsArrayOffset + bpsArraySize;

            // ── Header ───────────────────────────────────
            Span<byte> header = stackalloc byte[8];
            if (littleEndian) { header[0] = (byte)'I'; header[1] = (byte)'I'; }
            else    { header[0] = (byte)'M'; header[1] = (byte)'M'; }
            WriteUInt16(header, 2, 42, littleEndian);
            WriteUInt32(header, 4, (uint)ifdOffset, littleEndian);
            stream.Write(header);

            // ── IFD entries ──────────────────────────────
            var ifd = new byte[ifdSize];
            WriteUInt16(ifd, 0, (ushort)tagCount, littleEndian);
            int pos = 2;

            pos = WriteIFDEntry(ifd, pos, TagImageWidth, TypeLong, 1, (uint)width, littleEndian);
            pos = WriteIFDEntry(ifd, pos, TagImageLength, TypeLong, 1, (uint)height, littleEndian);

            if (!writeBitsPerSampleArray)
                pos = WriteIFDEntry(ifd, pos, TagBitsPerSample, TypeShort, (uint)encodedPixelData.BitsPerSample.Length, encodedPixelData.BitsPerSample[0], littleEndian);
            else
                pos = WriteIFDEntryOffset(ifd, pos, TagBitsPerSample, TypeShort, (uint)encodedPixelData.BitsPerSample.Length, (uint)bpsArrayOffset, littleEndian);

            pos = WriteIFDEntry(ifd, pos, TagCompression, TypeShort, 1, compressionCode, littleEndian);
            pos = WriteIFDEntry(ifd, pos, TagPhotometric, TypeShort, 1, encodedPixelData.Photometric, littleEndian);
            pos = WriteIFDEntryOffset(ifd, pos, TagStripOffsets, TypeLong, 1, (uint)pixelDataOffset, littleEndian);
            pos = WriteIFDEntry(ifd, pos, TagSamplesPerPixel, TypeShort, 1, encodedPixelData.SamplesPerPixel, littleEndian);
            pos = WriteIFDEntry(ifd, pos, TagRowsPerStrip, TypeLong, 1, (uint)height, littleEndian);
            pos = WriteIFDEntry(ifd, pos, TagStripByteCounts, TypeLong, 1, (uint)imageDataSize, littleEndian);
            pos = WriteIFDEntryOffset(ifd, pos, TagXResolution, TypeRational, 1, (uint)rationalsOffset, littleEndian);
            pos = WriteIFDEntryOffset(ifd, pos, TagYResolution, TypeRational, 1, (uint)(rationalsOffset + 8), littleEndian);
            pos = WriteIFDEntry(ifd, pos, TagResolutionUnit, TypeShort, 1, 2, littleEndian);

            if (usePredictor)
                pos = WriteIFDEntry(ifd, pos, TagPredictor, TypeShort, 1, (ushort)encoderOptions.Predictor, littleEndian);

            if (hasExtraSamples)
                pos = WriteIFDEntry(ifd, pos, TagExtraSamples, TypeShort, 1, encodedPixelData.ExtraSamples!.Value, littleEndian);

            WriteUInt32(ifd, pos, 0, littleEndian); // Next IFD = 0 (single image)
            stream.Write(ifd);

            // ── Rational values (72 DPI) ─────────────────
            Span<byte> rationals = stackalloc byte[16];
            WriteUInt32(rationals, 0, 72, littleEndian);
            WriteUInt32(rationals, 4, 1, littleEndian);
            WriteUInt32(rationals, 8, 72, littleEndian);
            WriteUInt32(rationals, 12, 1, littleEndian);
            stream.Write(rationals);

            // ── BitsPerSample array ──────────────────────
            if (writeBitsPerSampleArray)
            {
                Span<byte> bpsArray = stackalloc byte[encodedPixelData.BitsPerSample.Length * 2];
                for (int i = 0; i < encodedPixelData.BitsPerSample.Length; i++)
                    WriteUInt16(bpsArray, i * 2, encodedPixelData.BitsPerSample[i], littleEndian);
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

        /// <summary>
        /// Reads an entire stream into a byte array.
        /// </summary>
        /// <param name="stream">The source stream to read.</param>
        /// <returns>A byte array containing the full contents of <paramref name="stream"/>.</returns>
        public static byte[] ReadAllBytes(Stream stream)
        {
            if (stream is MemoryStream ms)
                return ms.ToArray();

            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        /// <summary>
        /// Determines the TIFF byte order from the file header.
        /// </summary>
        /// <param name="data">The TIFF file header bytes to inspect.</param>
        /// <returns><see langword="true"/> for little-endian TIFF data; <see langword="false"/> for big-endian TIFF data.</returns>
        public static bool ParseByteOrder(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8)
                throw new InvalidDataException("TIFF header is incomplete.");

            if (data[0] == (byte)'I' && data[1] == (byte)'I') return true;
            if (data[0] == (byte)'M' && data[1] == (byte)'M') return false;
            throw new InvalidDataException("Not a valid TIFF file.");
        }

        /// <summary>
        /// Determines whether TIFF horizontal differencing should be applied for the specified encoding settings.
        /// </summary>
        /// <param name="compression">The TIFF compression code selected for the write operation.</param>
        /// <param name="encodedPixelData">The encoded sample layout being written.</param>
        /// <param name="predictor">The predictor requested for encoding.</param>
        /// <returns><see langword="true"/> when horizontal differencing should be applied; otherwise <see langword="false"/>.</returns>
        public static bool ShouldApplyPredictor(ushort compression, TiffEncodedPixelData encodedPixelData, TiffPredictor predictor)
        {
            if (predictor != TiffPredictor.Horizontal)
                return false;

            if (compression is not (CompressionLZW or CompressionDeflate or CompressionAdobeDeflate))
                return false;

            return encodedPixelData.BitsPerSample.Length > 0 && encodedPixelData.BitsPerSample[0] is 8 or 16;
        }

        /// <summary>
        /// Returns a bounded slice of TIFF file data.
        /// </summary>
        /// <param name="data">The full TIFF file data.</param>
        /// <param name="offset">The byte offset of the requested range.</param>
        /// <param name="byteCount">The number of bytes to include in the returned range.</param>
        /// <returns>A bounded slice of <paramref name="data"/>.</returns>
        public static ReadOnlySpan<byte> GetRange(ReadOnlySpan<byte> data, int offset, int byteCount)
        {
            if ((uint)offset > data.Length || byteCount < 0 || offset > data.Length - byteCount)
                throw new InvalidDataException("TIFF strip data references bytes outside the file bounds.");

            return data.Slice(offset, byteCount);
        }

        /// <summary>
        /// Reverses the bit order of each byte in the supplied span.
        /// </summary>
        /// <param name="data">The input bytes whose bit order should be reversed.</param>
        /// <returns>A new array containing the bit-reversed bytes.</returns>
        public static byte[] ReverseBitsPerByte(ReadOnlySpan<byte> data)
        {
            byte[] reversed = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                reversed[i] = ReverseBits(data[i]);

            return reversed;
        }

        /// <summary>
        /// Reverses the bit order of a single byte.
        /// </summary>
        /// <param name="value">The byte whose bits should be reversed.</param>
        /// <returns>The bit-reversed representation of <paramref name="value"/>.</returns>
        public static byte ReverseBits(byte value)
        {
            value = (byte)(((value & 0xAA) >> 1) | ((value & 0x55) << 1));
            value = (byte)(((value & 0xCC) >> 2) | ((value & 0x33) << 2));
            value = (byte)(((value & 0xF0) >> 4) | ((value & 0x0F) << 4));
            return value;
        }

        // ──────────────────────────────────────────────
        // Binary write helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Writes a 16-bit unsigned integer using the specified byte order.
        /// </summary>
        /// <param name="buf">The destination buffer.</param>
        /// <param name="offset">The byte offset at which to write the value.</param>
        /// <param name="value">The 16-bit value to write.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order; otherwise big-endian.</param>
        public static void WriteUInt16(Span<byte> buf, int offset, ushort value, bool le)
        {
            if (le) BinaryPrimitives.WriteUInt16LittleEndian(buf[offset..], value);
            else BinaryPrimitives.WriteUInt16BigEndian(buf[offset..], value);
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer using the specified byte order.
        /// </summary>
        /// <param name="buf">The destination buffer.</param>
        /// <param name="offset">The byte offset at which to write the value.</param>
        /// <param name="value">The 32-bit value to write.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order; otherwise big-endian.</param>
        public static void WriteUInt32(Span<byte> buf, int offset, uint value, bool le)
        {
            if (le) BinaryPrimitives.WriteUInt32LittleEndian(buf[offset..], value);
            else BinaryPrimitives.WriteUInt32BigEndian(buf[offset..], value);
        }

        /// <summary>
        /// Writes a TIFF IFD entry, storing inline values when the type and count permit it.
        /// </summary>
        /// <param name="ifd">The destination IFD buffer.</param>
        /// <param name="pos">The byte position at which to write the entry.</param>
        /// <param name="tag">The TIFF tag identifier.</param>
        /// <param name="type">The TIFF field type.</param>
        /// <param name="count">The number of values in the field.</param>
        /// <param name="value">The inline value or offset to write.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order; otherwise big-endian.</param>
        /// <returns>The byte position immediately following the written entry.</returns>
        public static int WriteIFDEntry(Span<byte> ifd, int pos, ushort tag, ushort type, uint count, uint value, bool le)
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
        /// Writes a TIFF IFD entry whose value is stored at an external offset.
        /// </summary>
        /// <param name="ifd">The destination IFD buffer.</param>
        /// <param name="pos">The byte position at which to write the entry.</param>
        /// <param name="tag">The TIFF tag identifier.</param>
        /// <param name="type">The TIFF field type.</param>
        /// <param name="count">The number of values in the field.</param>
        /// <param name="offset">The external data offset to write into the entry.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order; otherwise big-endian.</param>
        /// <returns>The byte position immediately following the written entry.</returns>
        public static int WriteIFDEntryOffset(Span<byte> ifd, int pos, ushort tag, ushort type, uint count, uint offset, bool le)
        {
            WriteUInt16(ifd, pos, tag, le);
            WriteUInt16(ifd, pos + 2, type, le);
            WriteUInt32(ifd, pos + 4, count, le);
            WriteUInt32(ifd, pos + 8, offset, le);
            return pos + 12;
        }
    }
}
