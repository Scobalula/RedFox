using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.IO;

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
        public Image? Load(Texture texture, ImageTranslatorManager translatorManager)
        {
            string filePath = texture.EffectiveFilePath;
            if (File.Exists(filePath))
                return translatorManager.Read(filePath);

            return null;
        }
    }
}
