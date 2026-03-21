namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Represents the parsed EXR header fields used by the scanline reader.
    /// </summary>
    internal sealed record ExrHeader(int Width, int Height, int MinX, int MinY, ExrCompressionType Compression, IReadOnlyList<ExrChannel> Channels);
}