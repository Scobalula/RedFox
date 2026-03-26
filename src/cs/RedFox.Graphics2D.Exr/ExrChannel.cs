namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Represents a single channel entry parsed from an EXR file header.
    /// </summary>
    /// <param name="Name">The channel name (e.g. "R", "G", "B", "A").</param>
    /// <param name="PixelType">The pixel data type for this channel.</param>
    /// <param name="IsLinear">Whether the channel stores linear (non-perceptual) data.</param>
    /// <param name="XSampling">Horizontal sub-sampling factor (1 = full resolution).</param>
    /// <param name="YSampling">Vertical sub-sampling factor (1 = full resolution).</param>
    public sealed record ExrChannel(string Name, ExrPixelType PixelType, bool IsLinear, int XSampling, int YSampling);
}
