namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Provides generic per-call encoding hints for image translators.
    /// Translators may honor or ignore individual values based on what the target format supports.
    /// For fine-grained, format-specific control, call the translator or writer API directly.
    /// </summary>
    public sealed class ImageTranslatorOptions
    {
        /// <summary>
        /// Gets or sets an optional quality hint in the range 1-100.
        /// Formats without a quality concept may ignore this value.
        /// </summary>
        public int? Quality { get; set; }

        /// <summary>
        /// Gets or sets a generic compression preference.
        /// Each translator maps this hint to the closest behavior its format can support.
        /// </summary>
        public ImageCompressionPreference Compression { get; set; } = ImageCompressionPreference.Default;

        /// <summary>
        /// Gets or sets the preferred number of bits per channel in the written output.
        /// Typical values are 8, 16, and 32. Translators may ignore unsupported values.
        /// </summary>
        public int? BitsPerChannel { get; set; }
    }
}