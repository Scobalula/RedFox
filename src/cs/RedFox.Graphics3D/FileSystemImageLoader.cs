using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// An <see cref="ITextureLoader"/> that resolves texture file paths relative to a base
    /// directory (typically the model file's directory). Supports both absolute and
    /// relative paths including parent-directory references (e.g. <c>..\images\tex.png</c>).
    /// </summary>
    public sealed class FileSystemImageLoader : ITextureLoader
    {
        /// <summary>
        /// Gets the shared file-system image loader instance.
        /// </summary>
        public static FileSystemImageLoader Shared { get; } = new();

        /// <inheritdoc/>
        public Image Load(Texture texture, ImageTranslatorManager translatorManager)
        {
            // Try the path as given first (absolute or already resolved).
            if (File.Exists(texture.FilePath))
                return translatorManager.Read(texture.FilePath);

            return null;
        }
    }
}
