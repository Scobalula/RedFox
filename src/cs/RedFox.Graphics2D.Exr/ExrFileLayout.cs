namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Describes the byte layout and block sizing rules for scanline OpenEXR files.
    /// </summary>
    internal static class ExrFileLayout
    {
        /// <summary>
        /// The OpenEXR file magic value.
        /// </summary>
        public const uint Magic = 0x01312F76;

        /// <summary>
        /// The supported EXR file format version.
        /// </summary>
        public const uint Version = 2;

        /// <summary>
        /// Gets the byte width of a single EXR channel sample.
        /// </summary>
        public static int GetBytesPerSample(ExrPixelType pixelType)
        {
            return pixelType switch
            {
                ExrPixelType.Uint => 4,
                ExrPixelType.Half => 2,
                ExrPixelType.Float => 4,
                _ => throw new NotSupportedException($"EXR pixel type '{pixelType}' is not supported."),
            };
        }

        /// <summary>
        /// Computes the number of scanlines stored in each chunk for the specified compression mode.
        /// </summary>
        public static int GetLinesPerBlock(ExrCompressionType compression)
        {
            return compression switch
            {
                ExrCompressionType.None => 1,
                ExrCompressionType.Rle => 1,
                ExrCompressionType.Zips => 1,
                ExrCompressionType.Zip => 16,
                ExrCompressionType.Piz => 32,
                ExrCompressionType.Pxr24 => 16,
                ExrCompressionType.B44 => 32,
                ExrCompressionType.B44A => 32,
                ExrCompressionType.Dwaa => 32,
                ExrCompressionType.Dwab => 256,
                _ => throw new NotSupportedException($"EXR compression mode '{compression}' is not supported."),
            };
        }

        /// <summary>
        /// Computes the expected byte size of an uncompressed scanline block.
        /// </summary>
        public static int CalculateBlockSize(IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock)
        {
            int total = 0;

            foreach (var channel in channels)
                total = checked(total + width * rowsInBlock * GetBytesPerSample(channel.PixelType));

            return total;
        }
    }
}