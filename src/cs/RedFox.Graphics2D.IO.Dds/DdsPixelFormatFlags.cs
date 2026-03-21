namespace RedFox.Graphics2D.IO
{
    [Flags]
    internal enum DdsPixelFormatFlags : uint
    {
        AlphaPixels = 0x1,
        Alpha = 0x2,
        FourCc = 0x4,
        Rgb = 0x40,
        Yuv = 0x200,
        Luminance = 0x20000,
    }
}
