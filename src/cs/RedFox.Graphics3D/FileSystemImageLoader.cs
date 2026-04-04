using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// An <see cref="IImageLoader"/> that resolves texture file paths relative to a base
    /// directory (typically the model file's directory). Supports both absolute and
    /// relative paths including parent-directory references (e.g. <c>..\images\tex.png</c>).
    /// </summary>
    public sealed class FileSystemImageLoader : IImageLoader
    {
        public static FileSystemImageLoader Shared => new();

        /// <inheritdoc/>
        public Image? Load(string filePath, ImageTranslatorManager translatorManager)
        {
            // Try the path as given first (absolute or already resolved).
            if (File.Exists(filePath))
                return translatorManager.Read(filePath);

            return null;
        }
    }
}
