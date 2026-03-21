using RedFox.Graphics2D;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// An <see cref="IImageLoader"/> that resolves texture file paths relative to a base
    /// directory (typically the model file's directory). Supports both absolute and
    /// relative paths including parent-directory references (e.g. <c>..\images\tex.png</c>).
    /// </summary>
    public sealed class FileSystemImageLoader : IImageLoader
    {
        private readonly string _baseDirectory;
        private readonly string _filePath;
        private readonly Func<string, Image?> _readImage;

        /// <summary>
        /// Creates a loader that will resolve <paramref name="filePath"/> relative to
        /// <paramref name="baseDirectory"/> and load it via the given reader function.
        /// </summary>
        /// <param name="baseDirectory">The directory to resolve relative paths against.</param>
        /// <param name="filePath">The texture file path (absolute or relative).</param>
        /// <param name="readImage">
        /// A function that reads an image from an absolute file path.
        /// Typically wraps <c>ImageTranslatorManager.Read</c>.
        /// </param>
        public FileSystemImageLoader(string baseDirectory, string filePath, Func<string, Image?> readImage)
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _readImage = readImage ?? throw new ArgumentNullException(nameof(readImage));
        }

        /// <inheritdoc/>
        public Image? Load()
        {
            // Try the path as given first (absolute or already resolved).
            if (File.Exists(_filePath))
                return _readImage(_filePath);

            // Resolve relative to the base directory.
            var resolved = Path.GetFullPath(Path.Combine(_baseDirectory, _filePath));
            if (File.Exists(resolved))
                return _readImage(resolved);

            // Try just the filename in the base directory (strip any subdirectory path).
            var fileNameOnly = Path.GetFileName(_filePath);
            var fallback = Path.Combine(_baseDirectory, fileNameOnly);
            if (File.Exists(fallback))
                return _readImage(fallback);

            return null;
        }
    }
}
