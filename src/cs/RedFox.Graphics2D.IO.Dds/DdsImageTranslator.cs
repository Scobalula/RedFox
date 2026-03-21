using System.Buffers.Binary;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Provides DDS translation support for <see cref="ImageTranslatorManager"/> using <see cref="DdsLoader"/> and <see cref="DdsWriter"/>.
    /// </summary>
    public sealed class DdsImageTranslator : ImageTranslator
    {
        /// <inheritdoc />
        public override string Name => "DDS";

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override IReadOnlyList<string> Extensions { get; } = [".dds"];

        /// <inheritdoc />
        public override Image Read(string filePath)
        {
            return DdsLoader.Load(filePath);
        }

        /// <inheritdoc />
        public override Image Read(Stream stream)
        {
            return DdsLoader.Load(stream);
        }

        /// <inheritdoc />
        public override void Write(Stream stream, Image image)
        {
            DdsWriter.Save(stream, image);
        }

        /// <inheritdoc />
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            if (header.Length >= sizeof(uint))
            {
                uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
                return magic == DdsConstants.Magic;
            }

            return IsValid(filePath, extension);
        }
    }
}
