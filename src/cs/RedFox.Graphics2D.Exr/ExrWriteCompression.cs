namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Identifies the scanline compression mode used when writing OpenEXR images.
    /// </summary>
    public enum ExrWriteCompression : byte
    {
        /// <summary>
        /// Writes uncompressed scanline blocks.
        /// </summary>
        None = 0,

        /// <summary>
        /// Writes run-length encoded scanline blocks.
        /// </summary>
        Rle = 1,

        /// <summary>
        /// Writes zlib-compressed single-scanline blocks.
        /// </summary>
        Zips = 2,

        /// <summary>
        /// Writes zlib-compressed 16-scanline blocks.
        /// </summary>
        Zip = 3,

        /// <summary>
        /// Writes PXR24-compressed 16-scanline blocks.
        /// </summary>
        Pxr24 = 5,

        /// <summary>
        /// Writes B44-compressed HALF scanline blocks.
        /// </summary>
        B44 = 6,

        /// <summary>
        /// Writes B44A-compressed HALF scanline blocks.
        /// </summary>
        B44A = 7,
    }
}
