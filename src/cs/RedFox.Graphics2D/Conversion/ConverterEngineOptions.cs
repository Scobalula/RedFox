namespace RedFox.Graphics2D.Conversion
{
    /// <summary>
    /// Common options for custom converter engines.
    /// </summary>
    public sealed class ConverterEngineOptions
    {
        /// <summary>
        /// When true, conversion callers should continue with built-in CPU codecs when an engine cannot convert.
        /// </summary>
        public bool AllowCpuFallback { get; init; } = true;
    }
}
