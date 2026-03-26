namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Represents the parsed EXR header fields used by the scanline reader.
    /// </summary>
    /// <param name="Width">Image width in pixels.</param>
    /// <param name="Height">Image height in pixels.</param>
    /// <param name="MinX">Minimum X coordinate of the data window.</param>
    /// <param name="MinY">Minimum Y coordinate of the data window.</param>
    /// <param name="Compression">The compression algorithm used for the pixel data.</param>
    /// <param name="Channels">The ordered list of channel definitions in the image.</param>
    public sealed record ExrHeader(int Width, int Height, int MinX, int MinY, ExrCompressionType Compression, IReadOnlyList<ExrChannel> Channels);
}
