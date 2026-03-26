namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Computes the row-pitch or linear-size values stored in the DDS header
    /// for the top-level surface.
    /// </summary>
    public static class DdsPitchCalculator
    {
        /// <summary>
        /// Computes the top-level pitch (uncompressed) or linear size (compressed) for the given format.
        /// </summary>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="format">The pixel format of the image.</param>
        /// <param name="isBlockCompressed"><see langword="true"/> if the format uses block compression.</param>
        /// <returns>The pitch or linear size in bytes.</returns>
        public static uint GetTopLevelPitchOrLinearSize(int width, ImageFormat format, bool isBlockCompressed)
        {
            if (isBlockCompressed)
            {
                int blockCount = Math.Max(1, (width + 3) / 4);
                int blockSize = ImageFormatInfo.GetBlockSize(format);
                return checked((uint)(blockCount * blockSize));
            }

            int bitsPerPixel = ImageFormatInfo.GetBitsPerPixel(format);
            return checked((uint)((width * bitsPerPixel + 7) / 8));
        }
    }
}
