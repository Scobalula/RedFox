namespace RedFox.Graphics2D.IO
{
    internal static class DdsPitchCalculator
    {
        internal static uint GetTopLevelPitchOrLinearSize(int width, ImageFormat format, bool isBlockCompressed)
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
