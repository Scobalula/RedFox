namespace RedFox.Graphics2D.Jpeg
{
    /// <summary>
    /// Container for a fully decoded JPEG image before conversion to the final <see cref="Image"/> format.
    /// </summary>
    public sealed class DecodedJpegImage
    {
        /// <summary>Image width in pixels.</summary>
        public int Width { get; init; }

        /// <summary>Image height in pixels.</summary>
        public int Height { get; init; }

        /// <summary>Number of color components (1 = grayscale, 3 = YCbCr/RGB).</summary>
        public int ComponentCount { get; init; }

        /// <summary>Detected color space.</summary>
        public JpegColorSpace ColorSpace { get; init; }

        /// <summary>
        /// Per-component sample planes. Each byte array contains one sample per pixel
        /// for the given component at its native (possibly subsampled) resolution,
        /// after IDCT and level shift.
        /// </summary>
        public byte[][] ComponentData { get; init; } = [];

        /// <summary>Per-component width in samples.</summary>
        public int[] ComponentWidths { get; init; } = [];

        /// <summary>Per-component height in samples.</summary>
        public int[] ComponentHeights { get; init; } = [];

        /// <summary>Per-component horizontal sampling factors.</summary>
        public int[] ComponentHSamples { get; init; } = [];

        /// <summary>Per-component vertical sampling factors.</summary>
        public int[] ComponentVSamples { get; init; } = [];

        /// <summary>Maximum horizontal sampling factor.</summary>
        public int MaxHSample { get; init; }

        /// <summary>Maximum vertical sampling factor.</summary>
        public int MaxVSample { get; init; }
    }
}
