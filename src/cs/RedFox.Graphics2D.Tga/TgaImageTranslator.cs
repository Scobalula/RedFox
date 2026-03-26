using RedFox.Graphics2D.IO;

namespace RedFox.Graphics2D.Tga
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for TGA (Truevision TARGA) image files.
    /// </summary>
    /// <remarks>
    /// <para><b>Reading:</b> Supports uncompressed (type 2) and RLE-compressed (type 10)
    /// true-color TGA images with 24-bit BGR or 32-bit BGRA pixel data.
    /// Both top-to-bottom and bottom-to-top origin orientations are handled.</para>
    /// <para><b>Writing:</b> Outputs uncompressed true-color TGA files.
    /// Writes 32-bit BGRA when the source has non-opaque alpha, or 24-bit BGR otherwise.
    /// Fast paths exist for BGRA and RGBA source formats; all other formats are decoded
    /// through the <see cref="RedFox.Graphics2D.Codecs.PixelCodecRegistry"/>.</para>
    /// </remarks>
    public sealed class TgaImageTranslator : ImageTranslator
    {
        /// <inheritdoc/>
        public override string Name => "TGA";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions { get; } = [".tga"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            Span<byte> headerBytes = stackalloc byte[TgaHeader.SizeInBytes];
            stream.ReadExactly(headerBytes);

            TgaHeader header = TgaHeader.Parse(headerBytes);

            if (header.ImageType is not (TgaImageType.TrueColor or TgaImageType.RleTrueColor))
            {
                throw new NotSupportedException(
                    $"Unsupported TGA image type: {header.ImageType}. Only uncompressed (2) and RLE (10) true-color are supported.");
            }

            if (header.BitsPerPixel is not (24 or 32))
            {
                throw new NotSupportedException(
                    $"Unsupported TGA bits per pixel: {header.BitsPerPixel}. Only 24 and 32 are supported.");
            }

            if (header.ColorMapType != 0)
            {
                throw new NotSupportedException("Color-mapped TGA files are not supported.");
            }

            // Skip the image identification field.
            if (header.IdLength > 0)
            {
                stream.Seek(header.IdLength, SeekOrigin.Current);
            }

            int bytesPerPixel = header.BytesPerPixel;
            int pixelCount = header.Width * header.Height;
            var rawPixels = new byte[pixelCount * bytesPerPixel];

            if (header.ImageType == TgaImageType.TrueColor)
            {
                stream.ReadExactly(rawPixels);
            }
            else
            {
                TgaRleDecoder.Decode(stream, rawPixels, pixelCount, bytesPerPixel);
            }

            // Convert BGR/BGRA → RGBA8.
            var output = new byte[pixelCount * 4];
            TgaPixelConverter.BgrToRgba(rawPixels, output, pixelCount, bytesPerPixel);

            // Flip vertically if stored bottom-to-top (the default TGA orientation).
            if (!header.IsTopToBottom)
            {
                TgaPixelConverter.FlipVertical(output, header.Width, header.Height, bytesPerPixel: 4);
            }

            return new Image(header.Width, header.Height, ImageFormat.R8G8B8A8Unorm, output);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image)
        {
            TgaWriter.Save(stream, image);
        }

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            // TGA has no magic number — rely on extension and basic header sanity.
            if (!IsValid(filePath, extension))
            {
                return false;
            }

            if (header.Length < TgaHeader.SizeInBytes)
            {
                return false;
            }

            int imageType = header[2];
            int bitsPerPixel = header[16];

            return imageType is 2 or 10
                && bitsPerPixel is 24 or 32;
        }
    }
}
