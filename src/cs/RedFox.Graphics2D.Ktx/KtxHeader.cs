namespace RedFox.Graphics2D.Ktx
{
    internal readonly record struct KtxHeader(
        uint Endianness,
        uint GlType,
        uint GlTypeSize,
        uint GlFormat,
        uint GlInternalFormat,
        uint GlBaseInternalFormat,
        uint PixelWidth,
        uint PixelHeight,
        uint PixelDepth,
        uint NumberOfArrayElements,
        uint NumberOfFaces,
        uint NumberOfMipmapLevels,
        uint BytesOfKeyValueData);
}
