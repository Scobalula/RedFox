namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Represents a generic compression preference passed to an image translator.
    /// Translators interpret these values according to the capabilities of their target format.
    /// </summary>
    public enum ImageCompressionPreference
    {
        /// <summary>
        /// Use the translator's default compression behavior.
        /// </summary>
        Default,

        /// <summary>
        /// Prefer the least compressed form the format can reasonably produce.
        /// </summary>
        None,

        /// <summary>
        /// Prefer faster encoding over smaller files.
        /// </summary>
        Fast,

        /// <summary>
        /// Prefer a balanced tradeoff between speed and size.
        /// </summary>
        Balanced,

        /// <summary>
        /// Prefer the smallest file size the translator can reasonably target.
        /// </summary>
        SmallestSize,
    }
}