using RedFox.Graphics2D.Codecs;
using RedFox.Graphics2D.IO;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using RedFox.Graphics2D.Png;

namespace RedFox.Graphics2D.Png
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for PNG files.
    /// Supports full PNG decode (all color types, legal bit depths, filter methods, Adam7 interlace)
    /// and adaptive-filtered encoding from image data.
    /// </summary>
    public sealed class PngImageTranslator : ImageTranslator
    {
        // PNG constants and tables have been moved to a dedicated helper class `PngConstants`.
        // This keeps the translator focused on encoding/decoding flow while keeping
        // large static tables and configuration grouped separately.

        /// <inheritdoc/>
        public override string Name => "PNG";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions { get; } = [".png"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            PngFormatValidator.ValidateSignature(stream);

            PngHeader? header = null;
            byte[]? palette = null;
            byte[]? paletteAlpha = null;
            bool seenSrgb = false;
            bool seenIdat = false;

            using MemoryStream idat = new();

            while (true)
            {
                PngChunk chunk = PngChunkReader.ReadChunk(stream);

                switch (chunk.Type)
                {
                    case "IHDR":
                        if (header.HasValue)
                        {
                            throw new InvalidDataException("PNG contains multiple IHDR chunks.");
                        }
                        header = PngFormatValidator.ParseHeader(chunk.Data);
                        break;
                    case "PLTE":
                        if (!header.HasValue)
                        {
                            throw new InvalidDataException("PLTE chunk appeared before IHDR.");
                        }
                        palette = PngFormatValidator.ParsePalette(chunk.Data, header.Value);
                        break;
                    case "tRNS":
                        if (!header.HasValue)
                        {
                            throw new InvalidDataException("tRNS chunk appeared before IHDR.");
                        }
                        paletteAlpha = PngFormatValidator.ParseTransparency(chunk.Data, header.Value, palette);
                        break;
                    case "sRGB":
                        if (chunk.Data.Length != 1)
                        {
                            throw new InvalidDataException("Invalid sRGB chunk length.");
                        }
                        seenSrgb = true;
                        break;
                    case "IDAT":
                        if (!header.HasValue)
                        {
                            throw new InvalidDataException("IDAT chunk appeared before IHDR.");
                        }
                        seenIdat = true;
                        idat.Write(chunk.Data);
                        break;
                    case "IEND":
                        if (!header.HasValue)
                        {
                            throw new InvalidDataException("PNG is missing IHDR chunk.");
                        }
                        if (!seenIdat)
                        {
                            throw new InvalidDataException("PNG is missing IDAT chunk.");
                        }
                        byte[] decoded = PngScanlineProcessor.DecodePixels(header.Value, idat.ToArray(), palette, paletteAlpha);
                        ImageFormat format = seenSrgb ? ImageFormat.R8G8B8A8UnormSrgb : ImageFormat.R8G8B8A8Unorm;
                        return new Image(header.Value.Width, header.Value.Height, format, decoded);
                    default:
                        if (PngFormatValidator.IsCriticalChunk(chunk.Type))
                        {
                            throw new NotSupportedException($"Unsupported critical PNG chunk '{chunk.Type}'.");
                        }
                        break;
                }
            }
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image)
        {
            WriteCore(stream, image, fastPathCompressionLevel: CompressionLevel.Fastest, adaptiveCompressionLevel: CompressionLevel.Optimal);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image, ImageTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            CompressionLevel compressionLevel = ResolveCompressionLevel(options.Compression, CompressionLevel.Optimal);
            CompressionLevel fastPathCompressionLevel = ResolveCompressionLevel(options.Compression, CompressionLevel.Fastest);
            WriteCore(stream, image, fastPathCompressionLevel, compressionLevel);
        }

        private static void WriteCore(Stream stream, Image image, CompressionLevel fastPathCompressionLevel, CompressionLevel adaptiveCompressionLevel)
        {
            ref readonly var slice = ref image.GetSlice(0, 0, 0);
            int width = slice.Width;
            int height = slice.Height;
            int pixelCount = checked(width * height);

            var rgba = PngEncoderHelper.ExtractRgba8(slice, image.Format);

            // Large-image fast path prioritizes throughput and mirrors the previous writer behavior.
            if (pixelCount >= PngConstants.FastWriteModeMinPixels)
            {
                stream.Write(PngConstants.PngSignature);
                PngChunkWriter.WriteIHDR(stream, width, height, bitDepth: 8, colorType: 6, interlaceMethod: 0);

                if (ImageFormatInfo.IsSrgb(image.Format))
                PngChunkWriter.WriteSRGB(stream, renderingIntent: 0);

                PngChunkWriter.WriteCompressedIdatFilterNoneRgba(stream, rgba, width, height, fastPathCompressionLevel);
                PngChunkWriter.WriteChunk(stream, "IEND", []);
                return;
            }

            var model = PngEncoderHelper.AnalyzeColorModel(rgba, allowPaletteAnalysis: pixelCount <= PngConstants.PaletteAnalysisMaxPixels);
            var selection = PngEncoderHelper.SelectWriteMode(model);

            stream.Write(PngConstants.PngSignature);
            PngChunkWriter.WriteIHDR(stream, width, height, selection.BitDepth, selection.ColorType, interlaceMethod: 0);

            if (ImageFormatInfo.IsSrgb(image.Format))
                PngChunkWriter.WriteSRGB(stream, renderingIntent: 0);

            if (selection.ColorType == 3)
            {
                if (selection.Palette is null || selection.PaletteIndices is null)
                    throw new InvalidOperationException("Palette write mode is missing palette buffers.");

                PngChunkWriter.WriteChunk(stream, "PLTE", selection.Palette);
                if (selection.PaletteAlpha is { Length: > 0 })
                {
                    PngChunkWriter.WriteChunk(stream, "tRNS", selection.PaletteAlpha);
                }
            }

            var scanlineData = PngEncoderHelper.BuildScanlineData(rgba, width, height, selection);
            var filtered = PngScanlineProcessor.ApplyAdaptiveFiltering(scanlineData, width, height, selection.ColorType, selection.BitDepth);
            PngChunkWriter.WriteCompressedIdat(stream, filtered, adaptiveCompressionLevel);
            PngChunkWriter.WriteChunk(stream, "IEND", []);
        }

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            if (!IsValid(filePath, extension))
                return false;

            return header.Length >= PngConstants.PngSignature.Length && header[..PngConstants.PngSignature.Length].SequenceEqual(PngConstants.PngSignature);
        }

        private static CompressionLevel ResolveCompressionLevel(ImageCompressionPreference compressionPreference, CompressionLevel defaultLevel)
        {
            return compressionPreference switch
            {
                ImageCompressionPreference.None => CompressionLevel.NoCompression,
                ImageCompressionPreference.Fast => CompressionLevel.Fastest,
                ImageCompressionPreference.Balanced => CompressionLevel.Optimal,
                ImageCompressionPreference.SmallestSize => CompressionLevel.SmallestSize,
                _ => defaultLevel,
            };
        }
    }
}
