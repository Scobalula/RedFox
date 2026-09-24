using System;
using System.Collections.Generic;
using System.IO;
using RedFox.Imaging.IO;

namespace RedFox.Imaging.Formats.Ktx
{
    /// <summary>
    /// Provides KTX 1.0 translation support for <see cref="ImageTranslatorManager"/>.
    /// </summary>
    public sealed class KtxImageTranslator : ImageTranslator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KtxImageTranslator"/> class.
        /// </summary>
        public KtxImageTranslator()
        {
        }
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
