namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Specifies the predictor to apply when compressing TIFF pixel data.
    /// </summary>
    public enum TiffPredictor
    {
        /// <summary>
        /// No predictor is applied.
        /// </summary>
        None = 1,

        /// <summary>
        /// Horizontal differencing predictor.
        /// </summary>
        Horizontal = 2,
    }
}