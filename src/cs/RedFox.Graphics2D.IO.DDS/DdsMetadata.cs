namespace RedFox.Graphics2D.IO
{
    internal readonly record struct DdsMetadata(
        int Width,
        int Height,
        int Depth,
        int ArraySize,
        int MipLevels,
        ImageFormat Format,
        bool IsCubemap,
        int DataOffset);
}
