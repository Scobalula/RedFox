using RedFox.Graphics2D;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Provides a pluggable mechanism for loading image data into a <see cref="Texture"/>
    /// from an arbitrary source (file system, archive, network, etc.).
    /// </summary>
    public interface IImageLoader
    {
        /// <summary>
        /// Loads and returns the image data, or <see langword="null"/> if the source is unavailable.
        /// </summary>
        Image? Load();
    }
}
