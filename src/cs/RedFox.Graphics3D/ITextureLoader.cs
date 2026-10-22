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
        /// Loads image data using the requested mip preference, or returns <see langword="null"/> if the source is unavailable.
        /// </summary>
        /// <param name="texture">The texture to load.</param>
        /// <param name="translatorManager">The image translators used to decode the texture.</param>
        /// <param name="mipType">The preferred resolution of the loaded image.</param>
        /// <returns>The loaded image, or <see langword="null"/> if the source is unavailable.</returns>
        Image? Load(Texture texture, ImageTranslatorManager translatorManager, MipType mipType);
    }
}
