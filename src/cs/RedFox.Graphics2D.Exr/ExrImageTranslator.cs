using System.Buffers.Binary;
using RedFox.Graphics2D.IO;

namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for OpenEXR image files.
    /// Supports loading single-part scanline EXR images into float RGBA image data.
    /// </summary>
    public sealed class ExrImageTranslator : ImageTranslator
    {
        /// <summary>
        /// Gets or sets the default encoder options used when writing EXR files.
        /// </summary>
        public ExrWriteOptions EncoderOptions { get; set; } = new();

        /// <inheritdoc/>
        public override string Name => "EXR";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions { get; } = [".exr"];

        /// <inheritdoc/>
        public override Image Read(Stream stream) => ExrLoader.Load(stream);

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
            if (!IsValid(filePath, extension) || header.Length < sizeof(uint))
                return false;

            return BinaryPrimitives.ReadUInt32LittleEndian(header) == ExrFileLayout.Magic;
        }

        private ExrWriteOptions ResolveEncoderOptions(ImageTranslatorOptions options)
        {
            return new ExrWriteOptions
            {
                Compression = ResolveCompression(EncoderOptions.Compression, options.Compression),
                PixelType = ResolvePixelType(EncoderOptions.PixelType, options.BitsPerChannel),
            };
        }

        private static ExrWriteCompression ResolveCompression(ExrWriteCompression defaultCompression, ImageCompressionPreference compressionPreference)
        {
            return compressionPreference switch
            {
                ImageCompressionPreference.None => ExrWriteCompression.None,
                ImageCompressionPreference.Fast => ExrWriteCompression.Rle,
                ImageCompressionPreference.Balanced => ExrWriteCompression.Zip,
                ImageCompressionPreference.SmallestSize => ExrWriteCompression.Pxr24,
                _ => defaultCompression,
            };
        }

        private static ExrWritePixelType ResolvePixelType(ExrWritePixelType defaultPixelType, int? bitsPerChannel)
        {
            return bitsPerChannel switch
            {
                16 => ExrWritePixelType.Half,
                32 => ExrWritePixelType.Float,
                _ => defaultPixelType,
            };
        }

        private static void WriteCore(Stream stream, Image image, ExrWriteOptions encoderOptions)
        {
            ExrWriter.Save(stream, image, encoderOptions);
        }
    }
}
