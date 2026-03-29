using RedFox.Graphics2D.IO;

namespace RedFox.Graphics2D.Jpeg
{
    /// <summary>
    /// JPEG reader/writer implementation for the RedFox image translator pipeline.
    /// Supports baseline (SOF0) and progressive (SOF2) JPEG decoding, and baseline JPEG encoding.
    /// </summary>
    public sealed class JpegImageTranslator : ImageTranslator
    {
        /// <summary>
        /// Gets or sets the encoder options used when writing JPEG files.
        /// </summary>
        public JpegEncoderOptions EncoderOptions { get; set; } = new();

        /// <inheritdoc/>
        public override string Name => "JPEG";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions { get; } = [".jpg", ".jpeg", ".jpe", ".jfif"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            var decoder = new JpegDecoder(stream);
            var decoded = decoder.Decode();
            return ConvertToImage(decoded);
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

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            if (!IsValid(filePath, extension) || header.Length < 3)
                return false;

            return IsJpegHeader(header);
        }

        private static bool IsJpegHeader(ReadOnlySpan<byte> header)
        {
            return header.Length >= 3
                && header[0] == 0xFF
                && header[1] == 0xD8
                && header[2] == 0xFF;
        }

        private JpegEncoderOptions ResolveEncoderOptions(ImageTranslatorOptions options)
        {
            return new JpegEncoderOptions
            {
                Quality = options.Quality ?? EncoderOptions.Quality,
                Subsampling = ResolveSubsampling(EncoderOptions.Subsampling, options.Compression),
                OptimizeHuffmanTables = ResolveOptimizeHuffmanTables(EncoderOptions.OptimizeHuffmanTables, options.Compression),
            };
        }

        private static JpegChromaSubsampling ResolveSubsampling(JpegChromaSubsampling defaultSubsampling, ImageCompressionPreference compressionPreference)
        {
            return compressionPreference switch
            {
                ImageCompressionPreference.None => JpegChromaSubsampling.Yuv444,
                ImageCompressionPreference.Fast => JpegChromaSubsampling.Yuv420,
                ImageCompressionPreference.Balanced => JpegChromaSubsampling.Yuv422,
                ImageCompressionPreference.SmallestSize => JpegChromaSubsampling.Yuv420,
                _ => defaultSubsampling,
            };
        }

        private static bool ResolveOptimizeHuffmanTables(bool defaultOptimizeHuffmanTables, ImageCompressionPreference compressionPreference)
        {
            return compressionPreference switch
            {
                ImageCompressionPreference.Fast => false,
                ImageCompressionPreference.SmallestSize => true,
                _ => defaultOptimizeHuffmanTables,
            };
        }

        private static void WriteCore(Stream stream, Image image, JpegEncoderOptions encoderOptions)
        {
            JpegEncoder encoder = new(stream, encoderOptions);
            encoder.Encode(image);
        }

        /// <summary>
        /// Converts the decoded JPEG component planes to an <see cref="Image"/> in R8G8B8A8Unorm format.
        /// </summary>
        private static Image ConvertToImage(DecodedJpegImage decoded)
        {
            int width = decoded.Width;
            int height = decoded.Height;
            var pixels = new byte[width * height * 4];

            switch (decoded.ColorSpace)
            {
                case JpegColorSpace.Grayscale:
                    ConvertGrayscale(decoded, pixels, width, height);
                    break;

                case JpegColorSpace.YCbCr:
                    ConvertYCbCr(decoded, pixels, width, height);
                    break;

                default:
                    // Treat unknown color spaces as YCbCr
                    ConvertYCbCr(decoded, pixels, width, height);
                    break;
            }

            return new Image(width, height, ImageFormat.R8G8B8A8Unorm, pixels);
        }

        /// <summary>
        /// Converts grayscale component data to RGBA.
        /// </summary>
        private static void ConvertGrayscale(DecodedJpegImage decoded, byte[] pixels, int width, int height)
        {
            var grayPlane = decoded.ComponentData[0];
            int grayWidth = decoded.ComponentWidths[0];

            // Extract only the valid image region from the (possibly padded) component plane
            var trimmed = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                grayPlane.AsSpan(y * grayWidth, width).CopyTo(trimmed.AsSpan(y * width));
            }

            JpegColorConverter.GrayscaleToRgba(trimmed, pixels, width * height);
        }

        /// <summary>
        /// Converts YCbCr component data to RGBA with chroma upsampling.
        /// </summary>
        private static void ConvertYCbCr(DecodedJpegImage decoded, byte[] pixels, int width, int height)
        {
            // Extract the valid image region for each component
            var yPlane = ExtractPlane(decoded.ComponentData[0], decoded.ComponentWidths[0], width, height,
                decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[0], decoded.ComponentVSamples[0]);
            var cbPlane = ExtractPlane(decoded.ComponentData[1], decoded.ComponentWidths[1], width, height,
                decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[1], decoded.ComponentVSamples[1]);
            var crPlane = ExtractPlane(decoded.ComponentData[2], decoded.ComponentWidths[2], width, height,
                decoded.MaxHSample, decoded.MaxVSample, decoded.ComponentHSamples[2], decoded.ComponentVSamples[2]);

            int cbWidth = (width * decoded.ComponentHSamples[1] + decoded.MaxHSample - 1) / decoded.MaxHSample;

            var chroma = new ChromaSampling(
                cbWidth,
                decoded.MaxHSample,
                decoded.MaxVSample,
                decoded.ComponentHSamples[1],
                decoded.ComponentVSamples[1]);

            JpegColorConverter.YCbCrToRgba(
                yPlane, cbPlane, crPlane,
                pixels,
                width, height,
                chroma);
        }

        /// <summary>
        /// Extracts the valid region from a (possibly padded) component sample plane.
        /// </summary>
        private static byte[] ExtractPlane(byte[] source, int sourceWidth,
            int imageWidth, int imageHeight,
            int maxH, int maxV, int compH, int compV)
        {
            int planeWidth = (imageWidth * compH + maxH - 1) / maxH;
            int planeHeight = (imageHeight * compV + maxV - 1) / maxV;

            var result = new byte[planeWidth * planeHeight];

            for (int y = 0; y < planeHeight; y++)
            {
                int srcOffset = y * sourceWidth;
                int dstOffset = y * planeWidth;
                int copyLen = Math.Min(planeWidth, sourceWidth - (y * sourceWidth < source.Length ? 0 : planeWidth));

                if (srcOffset + copyLen <= source.Length)
                {
                    source.AsSpan(srcOffset, copyLen).CopyTo(result.AsSpan(dstOffset));
                }
            }

            return result;
        }
    }
}
