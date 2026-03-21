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

    [Flags]
    internal enum DdsHeaderFlags : uint
    {
        Caps = 0x1,
        Height = 0x2,
        Width = 0x4,
        Pitch = 0x8,
        PixelFormat = 0x1000,
        MipMapCount = 0x20000,
        LinearSize = 0x80000,
        Depth = 0x800000,
    }

    [Flags]
    internal enum DdsCaps : uint
    {
        Complex = 0x8,
        MipMap = 0x400000,
        Texture = 0x1000,
    }

    [Flags]
    internal enum DdsCaps2 : uint
    {
        None = 0,
        Cubemap = 0x200,
        CubemapPositiveX = 0x400,
        CubemapNegativeX = 0x800,
        CubemapPositiveY = 0x1000,
        CubemapNegativeY = 0x2000,
        CubemapPositiveZ = 0x4000,
        CubemapNegativeZ = 0x8000,
        CubemapAllFaces = CubemapPositiveX | CubemapNegativeX | CubemapPositiveY | CubemapNegativeY | CubemapPositiveZ | CubemapNegativeZ,
        Volume = 0x200000,
    }

    internal enum DdsResourceDimension : uint
    {
        Unknown = 0,
        Buffer = 1,
        Texture1D = 2,
        Texture2D = 3,
        Texture3D = 4,
    }
}
