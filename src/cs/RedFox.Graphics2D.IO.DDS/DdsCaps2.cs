namespace RedFox.Graphics2D.IO
{
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
}
