namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Flags describing the contents of the DDS pixel format structure.
    /// </summary>
    [Flags]
    public enum DdsPixelFormatFlags : uint
    {
        /// <summary>
        /// The texture contains alpha data; <c>ABitMask</c> is valid.
        /// </summary>
        AlphaPixels = 0x1,

        /// <summary>
        /// The texture contains only alpha data (used for older DDS files).
        /// </summary>
        Alpha = 0x2,

        /// <summary>
        /// The texture is compressed; <c>FourCC</c> is valid.
        /// </summary>
        FourCc = 0x4,

        /// <summary>
        /// The texture contains uncompressed RGB data; bit masks are valid.
        /// </summary>
        Rgb = 0x40,

        /// <summary>
        /// The texture contains YUV data (used in older DDS files).
        /// </summary>
        Yuv = 0x200,

        /// <summary>
        /// The texture contains luminance data; <c>RBitMask</c> holds the luminance mask.
        /// </summary>
        Luminance = 0x20000,
    }
}
