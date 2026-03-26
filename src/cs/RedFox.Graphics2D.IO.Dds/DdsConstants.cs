namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Magic numbers and fixed sizes used when reading and writing DDS files.
    /// </summary>
    public static class DdsConstants
    {
        /// <summary>
        /// The DDS file magic number (<c>0x20534444</c>, ASCII "DDS ").
        /// </summary>
        public const uint Magic = 0x20534444;

        /// <summary>
        /// The required size of the DDS header structure in bytes.
        /// </summary>
        public const uint HeaderSize = 124;

        /// <summary>
        /// The required size of the DDS pixel format structure in bytes.
        /// </summary>
        public const uint PixelFormatSize = 32;

        /// <summary>
        /// The DX10 misc flag indicating a cubemap resource.
        /// </summary>
        public const uint Dxt10CubemapFlag = 0x4;
    }
}
