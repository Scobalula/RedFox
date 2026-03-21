namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Represents an EXR channel entry.
    /// </summary>
    internal sealed record ExrChannel(string Name, ExrPixelType PixelType, bool IsLinear, int XSampling, int YSampling);
}