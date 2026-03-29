namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Configuration options for the TIFF encoder.
    /// Set on <see cref="TiffImageTranslator.EncoderOptions"/> before calling
    /// <see cref="TiffImageTranslator.Write(System.IO.Stream, Image)"/>.
    /// </summary>
    public sealed class TiffEncoderOptions
    {
        /// <summary>
        /// Gets or sets the compression algorithm to use when writing TIFF data.
        /// Defaults to <see cref="TiffCompression.None"/> (uncompressed).
        /// </summary>
        public TiffCompression Compression { get; set; } = TiffCompression.None;

        /// <summary>
        /// Gets or sets the predictor used when compressing TIFF sample data.
        /// Defaults to <see cref="TiffPredictor.None"/>.
        /// </summary>
        public TiffPredictor Predictor { get; set; } = TiffPredictor.None;
    }
}
