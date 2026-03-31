namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Provides the base contract for reading and writing <see cref="Image"/> data in a file format.
    /// </summary>
    public abstract class ImageTranslator
    {
        /// <summary>
        /// Gets the display name of this translator.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets whether this translator supports reading images.
        /// </summary>
        public abstract bool CanRead { get; }

        /// <summary>
        /// Gets whether this translator supports writing images.
        /// </summary>
        public abstract bool CanWrite { get; }

        /// <summary>
        /// Gets the file extensions this translator handles.
        /// </summary>
        public abstract IReadOnlyList<string> Extensions { get; }

        /// <summary>
        /// Reads an image from the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the image file.</param>
        /// <returns>The loaded <see cref="Image"/>.</returns>
        public virtual Image Read(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Read(stream);
        }

        /// <summary>
        /// Reads an image from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the image data.</param>
        /// <returns>The loaded <see cref="Image"/>.</returns>
        public abstract Image Read(Stream stream);

        /// <summary>
        /// Writes an image to the specified file path.
        /// </summary>
        /// <param name="filePath">The destination file path.</param>
        /// <param name="image">The image to write.</param>
        public virtual void Write(string filePath, Image image)
        {
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            Write(stream, image);
        }

        /// <summary>
        /// Writes an image to the specified file path using the provided translation options.
        /// </summary>
        /// <param name="filePath">The destination file path.</param>
        /// <param name="image">The image to write.</param>
        /// <param name="options">Per-call translation hints such as quality, compression preference, and bit depth.</param>
        public virtual void Write(string filePath, Image image, ImageTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            Write(stream, image, options);
        }

        /// <summary>
        /// Writes an image to a stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="image">The image to write.</param>
        public abstract void Write(Stream stream, Image image);

        /// <summary>
        /// Writes an image to a stream using the provided translation options.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="image">The image to write.</param>
        /// <param name="options">Per-call translation hints such as quality, compression preference, and bit depth.</param>
        public virtual void Write(Stream stream, Image image, ImageTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Write(stream, image);
        }

        /// <summary>
        /// Determines whether this translator can handle the given file based on extension.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="extension">The file extension, including the leading dot.</param>
        /// <returns><see langword="true"/> when this translator supports the extension; otherwise <see langword="false"/>.</returns>
        public virtual bool IsValid(string filePath, string extension) =>
            Extensions.Contains(extension);

        /// <summary>
        /// Determines whether this translator can handle the given file based on extension and header bytes.
        /// </summary>
        /// <param name="header">The first bytes of the file for magic number validation.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="extension">The file extension, including the leading dot.</param>
        /// <returns><see langword="true"/> when this translator supports the file; otherwise <see langword="false"/>.</returns>
        public virtual bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension) =>
            IsValid(filePath, extension);
    }
}
