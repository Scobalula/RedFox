using RedFox.Graphics2D.IO;

namespace RedFox.Graphics2D.Iwi
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for IWI image files.
    /// </summary>
    public sealed class IwiTranslator : ImageTranslator
    {
        /// <inheritdoc/>
        public override string Name => "IWI";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions => [".iwi"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            return IsValid(filePath, extension);
        }
    }
}
