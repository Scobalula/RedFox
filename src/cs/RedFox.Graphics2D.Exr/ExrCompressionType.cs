namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Identifies the EXR scanline compression mode.
    /// </summary>
    public enum ExrCompressionType : byte
    {
        /// <summary>
        /// No compression — scanline data is stored uncompressed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Run-length encoding applied to differences between adjacent pixel values.
        /// </summary>
        Rle = 1,

        /// <summary>
        /// Zlib deflate applied to a single scanline.
        /// </summary>
        Zips = 2,

        /// <summary>
        /// Zlib deflate applied to blocks of up to 16 scanlines.
        /// </summary>
        Zip = 3,

        /// <summary>
        /// Wavelet-based compression using the PIZ algorithm.
        /// </summary>
        Piz = 4,

        /// <summary>
        /// Lossy 24-bit compression for FLOAT channels, lossless for HALF and UINT.
        /// </summary>
        Pxr24 = 5,

        /// <summary>
        /// Fixed-rate lossy compression for HALF channels using 4×4 blocks.
        /// </summary>
        B44 = 6,

        /// <summary>
        /// B44 variant that compresses flat 4×4 blocks more efficiently.
        /// </summary>
        B44A = 7,

        /// <summary>
        /// JPEG-based lossy compression for FLOAT/HALF data (DreamWorks Animation, type A).
        /// </summary>
        Dwaa = 8,

        /// <summary>
        /// JPEG-based lossy compression for FLOAT/HALF data (DreamWorks Animation, type B).
        /// </summary>
        Dwab = 9,
    }
}
