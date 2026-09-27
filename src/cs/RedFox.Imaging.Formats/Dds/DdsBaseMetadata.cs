using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Formats.Dds;

/// <summary>
/// Intermediate metadata parsed from the primary DDS header before format-specific
/// processing (DX10 or legacy) determines the remaining fields.
/// </summary>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="MipLevels">Number of mipmap levels.</param>
internal readonly record struct DdsBaseMetadata(int Width, int Height, int MipLevels)
{
    /// <summary>
    /// Combines this base metadata with the remaining format-specific fields
    /// to produce the final <see cref="DdsMetadata"/>.
    /// </summary>
    /// <param name="depth">Depth for volume textures; 1 for 2D textures.</param>
    /// <param name="arraySize">Number of textures in the array.</param>
    /// <param name="format">The pixel format of the image data.</param>
    /// <param name="isCubemap">Whether the image is a cubemap.</param>
    /// <param name="dataOffset">Byte offset to the start of the pixel payload.</param>
    /// <param name="isLegacyRgb24">Whether the legacy header describes 24-bit RGB data.</param>
    /// <returns>A fully populated <see cref="DdsMetadata"/>.</returns>
    public DdsMetadata ToMetadata(int depth, int arraySize, ImageFormat format, bool isCubemap, int dataOffset, bool isLegacyRgb24) => new(Width, Height, depth, arraySize, MipLevels, format, isCubemap, dataOffset, isLegacyRgb24);
}
