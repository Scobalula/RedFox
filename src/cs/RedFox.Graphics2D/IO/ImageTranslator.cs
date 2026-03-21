namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Provides an abstract base class for image translators, defining the contract
    /// for reading and writing <see cref="Image"/> data to and from various file formats.
    /// </summary>
    public abstract class ImageTranslator
    {
        /// <summary>
        /// Gets the display name of this translator.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets whether this translator supports reading (loading) images.
        /// </summary>
        public abstract bool CanRead { get; }

        /// <summary>
        /// Gets whether this translator supports writing (saving) images.
        /// </summary>
        public abstract bool CanWrite { get; }

        /// <summary>
        /// Gets the file extensions this translator handles (e.g. ".dds", ".tga").
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
        /// Writes an image to a stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="image">The image to write.</param>
        public abstract void Write(Stream stream, Image image);

        /// <summary>
        /// Determines whether this translator can handle the given file based on extension.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="extension">The file extension (including the leading dot).</param>
        /// <returns><c>true</c> if this translator supports the extension.</returns>
        public virtual bool IsValid(string filePath, string extension) =>
            Extensions.Contains(extension);

        /// <summary>
        /// Determines whether this translator can handle the given file based on extension and header bytes.
        /// </summary>
        /// <param name="header">The first bytes of the file for magic number validation.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="extension">The file extension (including the leading dot).</param>
        /// <returns><c>true</c> if this translator supports the file.</returns>
        public virtual bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension) =>
            IsValid(filePath, extension);
    }
}
