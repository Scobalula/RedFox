namespace RedFox.Imaging.IO
{
    /// <summary>
    /// Provides general encoding hints for image translators.
    /// </summary>
    public sealed class ImageTranslatorOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageTranslatorOptions"/> class.
        /// </summary>
        public ImageTranslatorOptions()
        {
        }

        /// <summary>
        /// Gets or sets the quality hint from 1 to 100.
        /// </summary>
        public int? Quality { get; set; }

        /// <summary>
        /// Gets or sets the compression preference.
        /// </summary>
        public ImageCompressionPreference Compression { get; set; } = ImageCompressionPreference.Default;

        /// <summary>
        /// Gets or sets the preferred number of bits per output channel.
        /// </summary>
        public int? BitsPerChannel { get; set; }
    }
}
