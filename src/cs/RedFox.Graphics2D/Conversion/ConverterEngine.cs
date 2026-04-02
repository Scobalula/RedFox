namespace RedFox.Graphics2D.Conversion
{
    /// <summary>
    /// Base type for pluggable conversion engines used by <see cref="Image.Convert(ImageFormat, ImageConvertFlags, ConverterEngine?)"/>.
    /// </summary>
    public abstract class ConverterEngine
    {
        /// <summary>
        /// Gets a converter engine instance that performs no custom conversion.
        /// </summary>
        public static ConverterEngine None { get; } = new NoOpConverterEngine();

        /// <summary>
        /// Gets a human-readable engine name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Attempts to convert a single 2D slice from one image format to another.
        /// Return <see langword="true"/> only when conversion succeeds and destination contains valid output.
        /// </summary>
        public abstract bool TryConvert(
            ReadOnlySpan<byte> source,
            ImageFormat sourceFormat,
            Span<byte> destination,
            ImageFormat destinationFormat,
            int width,
            int height,
            ImageConvertFlags flags);
    }
}
