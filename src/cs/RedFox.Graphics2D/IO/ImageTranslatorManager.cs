using System.Diagnostics.CodeAnalysis;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Manages a collection of <see cref="ImageTranslator"/> instances and provides
    /// methods to read and write images by automatically selecting the appropriate translator.
    /// </summary>
    public class ImageTranslatorManager
    {
        private readonly List<ImageTranslator> _translators = [];

        private const int DefaultHeaderSize = 256;

        /// <summary>
        /// Gets a read-only list of all registered translators.
        /// </summary>
        public IReadOnlyList<ImageTranslator> Translators => _translators;

        /// <summary>
        /// Registers an image translator. Replaces any existing translator with the same name.
        /// </summary>
        /// <param name="translator">The translator to register.</param>
        public void Register(ImageTranslator translator)
        {
            _translators.RemoveAll(t => t.Name == translator.Name);
            _translators.Add(translator);
        }

        /// <summary>
        /// Removes a previously registered translator by name.
        /// </summary>
        /// <param name="name">The name of the translator to remove.</param>
        /// <returns><c>true</c> if a translator was removed; otherwise, <c>false</c>.</returns>
        public bool Unregister(string name)
        {
            return _translators.RemoveAll(t => t.Name == name) > 0;
        }

        /// <summary>
        /// Attempts to find a translator that supports the given file by extension only.
        /// </summary>
        public bool TryGetTranslator(string filePath, string extension, [NotNullWhen(true)] out ImageTranslator? translator)
        {
            foreach (var t in _translators)
            {
                if (t.IsValid(filePath, extension))
                {
                    translator = t;
                    return true;
                }
            }

            translator = null;
            return false;
        }

        /// <summary>
        /// Attempts to find a translator that supports the given file by extension and header bytes.
        /// </summary>
        public bool TryGetTranslator(string filePath, string extension, ReadOnlySpan<byte> header, [NotNullWhen(true)] out ImageTranslator? translator)
        {
            foreach (var t in _translators)
            {
                if (t.IsValid(header, filePath, extension))
                {
                    translator = t;
                    return true;
                }
            }

            translator = null;
            return false;
        }

        /// <summary>
        /// Reads an image from a file, automatically selecting the appropriate translator.
        /// </summary>
        /// <param name="filePath">The path to the image file.</param>
        /// <returns>The loaded <see cref="Image"/>.</returns>
        public Image Read(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            string extension = Path.GetExtension(filePath);

            Span<byte> header = stackalloc byte[DefaultHeaderSize];
            int headerSize;

            using (var stream = File.OpenRead(filePath))
            {
                headerSize = stream.Read(header);
            }

            if (headerSize <= 0)
                throw new IOException($"Failed to read header from file: {filePath}");
            if (!TryGetTranslator(filePath, extension, header[..headerSize], out var translator))
                throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

            return translator.Read(filePath);
        }

        /// <summary>
        /// Reads an image from a stream, using the file path for extension-based translator selection
        /// and the stream's initial bytes for header validation.
        /// </summary>
        /// <param name="stream">The stream containing image data.</param>
        /// <param name="filePath">The file path (used for extension matching).</param>
        /// <returns>The loaded <see cref="Image"/>.</returns>
        public Image Read(Stream stream, string filePath)
        {
            string extension = Path.GetExtension(filePath);

            Span<byte> header = stackalloc byte[DefaultHeaderSize];
            var headerSize = stream.Read(header);

            if (headerSize <= 0)
                throw new IOException($"Failed to read header from stream for file: {filePath}");
            if (!TryGetTranslator(filePath, extension, header[..headerSize], out var translator))
                throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

            stream.Position = 0;

            return translator.Read(stream);
        }

        /// <summary>
        /// Writes an image to a file, automatically selecting the appropriate translator based on extension.
        /// </summary>
        /// <param name="filePath">The destination file path.</param>
        /// <param name="image">The image to write.</param>
        public void Write(string filePath, Image image)
        {
            string extension = Path.GetExtension(filePath);

            if (!TryGetTranslator(filePath, extension, out var translator))
                throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

            translator.Write(filePath, image);
        }

        /// <summary>
        /// Writes an image to a file using the specified per-call translation options.
        /// </summary>
        /// <param name="filePath">The destination file path.</param>
        /// <param name="image">The image to write.</param>
        /// <param name="options">Per-call translation hints such as quality, compression preference, and bit depth.</param>
        public void Write(string filePath, Image image, ImageTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            string extension = Path.GetExtension(filePath);

            if (!TryGetTranslator(filePath, extension, out var translator))
                throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

            translator.Write(filePath, image, options);
        }

        /// <summary>
        /// Writes an image to a stream, using the file path for extension-based translator selection.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="filePath">The file path (used for extension matching).</param>
        /// <param name="image">The image to write.</param>
        public void Write(Stream stream, string filePath, Image image)
        {
            string extension = Path.GetExtension(filePath);

            if (!TryGetTranslator(filePath, extension, out var translator))
                throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

            translator.Write(stream, image);
        }

        /// <summary>
        /// Writes an image to a stream using the specified per-call translation options.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="filePath">The file path (used for extension matching).</param>
        /// <param name="image">The image to write.</param>
        /// <param name="options">Per-call translation hints such as quality, compression preference, and bit depth.</param>
        public void Write(Stream stream, string filePath, Image image, ImageTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            string extension = Path.GetExtension(filePath);

            if (!TryGetTranslator(filePath, extension, out var translator))
                throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

            translator.Write(stream, image, options);
        }
    }
}
