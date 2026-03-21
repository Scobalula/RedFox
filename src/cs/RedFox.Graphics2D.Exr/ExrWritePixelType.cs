namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Identifies the sample type used when writing EXR channels.
    /// </summary>
    public enum ExrWritePixelType
    {
        /// <summary>
        /// Chooses a sensible default based on the source image format.
        /// </summary>
        Auto,

        /// <summary>
        /// Writes 16-bit HALF channel samples.
        /// </summary>
        Half,

        /// <summary>
        /// Writes 32-bit FLOAT channel samples.
        /// </summary>
        Float,
    }
}