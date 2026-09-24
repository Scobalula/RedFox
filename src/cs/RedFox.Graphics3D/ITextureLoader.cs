using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.IO;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Provides a pluggable mechanism for loading image data into a <see cref="Texture"/>
    /// from an arbitrary source (file system, archive, network, etc.).
    /// </summary>
    public interface ITextureLoader
    {
        /// <summary>
        /// Loads and returns the image data, or <see langword="null"/> if the source is unavailable.
        /// </summary>
        Image? Load(Texture texture, ImageTranslatorManager translatorManager);
    }
}
