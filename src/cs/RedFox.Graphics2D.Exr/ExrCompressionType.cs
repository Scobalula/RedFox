namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Identifies the EXR scanline compression mode.
    /// </summary>
    internal enum ExrCompressionType : byte
    {
        None = 0,
        Rle = 1,
        Zips = 2,
        Zip = 3,
        Piz = 4,
        Pxr24 = 5,
        B44 = 6,
        B44A = 7,
        Dwaa = 8,
        Dwab = 9,
    }
}