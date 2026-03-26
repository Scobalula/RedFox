namespace RedFox.Graphics2D.Tga
{
    /// <summary>
    /// Identifies the image type stored in a TGA file header.
    /// Only true-color types (uncompressed and RLE-compressed) are supported
    /// by the RedFox TGA translator.
    /// </summary>
    public enum TgaImageType : byte
    {
        /// <summary>
        /// No image data is present.
        /// </summary>
        NoData = 0,

        /// <summary>
        /// Uncompressed color-mapped (palette) image.
        /// </summary>
        ColorMapped = 1,

        /// <summary>
        /// Uncompressed true-color image (supported).
        /// </summary>
        TrueColor = 2,

        /// <summary>
        /// Uncompressed grayscale image.
        /// </summary>
        Grayscale = 3,

        /// <summary>
        /// Run-length encoded color-mapped image.
        /// </summary>
        RleColorMapped = 9,

        /// <summary>
        /// Run-length encoded true-color image (supported).
        /// </summary>
        RleTrueColor = 10,

        /// <summary>
        /// Run-length encoded grayscale image.
        /// </summary>
        RleGrayscale = 11,
    }
}
