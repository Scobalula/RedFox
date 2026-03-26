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
            ExrWriter.Save(stream, image);
        }

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            if (!IsValid(filePath, extension) || header.Length < sizeof(uint))
                return false;

            return BinaryPrimitives.ReadUInt32LittleEndian(header) == ExrFileLayout.Magic;
        }
    }
}
