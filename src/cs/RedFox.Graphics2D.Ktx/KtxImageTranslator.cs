using RedFox.Graphics2D.IO;

namespace RedFox.Graphics2D.Ktx
{
    /// <summary>
    /// Provides KTX 1.0 translation support for <see cref="ImageTranslatorManager"/>.
    /// </summary>
    public sealed class KtxImageTranslator : ImageTranslator
    {
        /// <inheritdoc />
        public override string Name => "KTX";

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override IReadOnlyList<string> Extensions { get; } = [".ktx"];

        /// <inheritdoc />
        public override Image Read(Stream stream)
        {
            return KtxLoader.Load(stream);
        }

        /// <inheritdoc />
        public override void Write(Stream stream, Image image)
        {
            KtxWriter.Save(stream, image);
        }

        /// <inheritdoc />
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            return IsValid(filePath, extension)
                && header.Length >= KtxConstants.Identifier.Length
                && header[..KtxConstants.Identifier.Length].SequenceEqual(KtxConstants.Identifier);
        }
    }
}
