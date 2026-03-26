namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Parsed metadata describing a DDS image's dimensions, format, and layout.
    /// </summary>
    /// <param name="Width">Image width in pixels.</param>
    /// <param name="Height">Image height in pixels.</param>
    /// <param name="Depth">Depth for volume textures; 1 for 2D textures.</param>
    /// <param name="ArraySize">Number of textures in the array (includes all cubemap faces).</param>
    /// <param name="MipLevels">Number of mipmap levels.</param>
    /// <param name="Format">The DXGI-based pixel format of the image data.</param>
    /// <param name="IsCubemap">Whether the image is a cubemap.</param>
    /// <param name="DataOffset">Byte offset to the start of the pixel payload.</param>
    public readonly record struct DdsMetadata(
        int Width,
        int Height,
        int Depth,
        int ArraySize,
        int MipLevels,
        ImageFormat Format,
        bool IsCubemap,
        int DataOffset);
}
