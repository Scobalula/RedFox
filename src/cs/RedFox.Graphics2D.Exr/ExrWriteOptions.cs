namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Configures how an image is serialized to the OpenEXR scanline format.
    /// </summary>
    public sealed class ExrWriteOptions
    {
        /// <summary>
        /// Gets or sets the scanline compression mode.
        /// </summary>
        public ExrWriteCompression Compression { get; set; } = ExrWriteCompression.Zip;

        /// <summary>
        /// Gets or sets the EXR channel sample type.
        /// </summary>
        public ExrWritePixelType PixelType { get; set; } = ExrWritePixelType.Auto;
    }
}
