using System;
using System.Collections.Generic;
using System.IO;
using RedFox.Imaging.IO;

namespace RedFox.Imaging.Formats.Iwi
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for IWI image files.
    /// </summary>
    public sealed class IwiTranslator : ImageTranslator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IwiTranslator"/> class.
        /// </summary>
        public IwiTranslator()
        {
        }
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
