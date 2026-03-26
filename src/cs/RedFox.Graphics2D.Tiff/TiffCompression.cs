namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Specifies the compression algorithm to use when writing TIFF image data.
    /// </summary>
    public enum TiffCompression
    {
        /// <summary>
        /// No compression — pixel data is stored uncompressed.
        /// </summary>
        None = 1,

        /// <summary>
        /// LZW (Lempel-Ziv-Welch) compression with MSB-first bit packing.
        /// </summary>
        LZW = 5,

        /// <summary>
        /// PackBits byte-oriented run-length encoding.
        /// </summary>
        PackBits = 32773
    }
}
