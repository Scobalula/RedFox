namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Identifies the EXR on-disk pixel type for a channel.
    /// </summary>
    public enum ExrPixelType
    {
        /// <summary>
        /// 32-bit unsigned integer samples.
        /// </summary>
        Uint = 0,

        /// <summary>
        /// 16-bit IEEE 754 half-precision floating-point samples.
        /// </summary>
        Half = 1,

        /// <summary>
        /// 32-bit IEEE 754 single-precision floating-point samples.
        /// </summary>
        Float = 2,
    }
}
