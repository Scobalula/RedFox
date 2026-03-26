namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Describes the chroma subsampling layout used during YCbCr-to-RGBA conversion,
/// grouping the chroma plane width and the sampling factors needed to compute
/// upsampling ratios.
/// </summary>
/// <param name="ChromaWidth">The width of the chroma component planes in samples.</param>
/// <param name="MaxHSample">The maximum horizontal sampling factor across all components.</param>
/// <param name="MaxVSample">The maximum vertical sampling factor across all components.</param>
/// <param name="CompHSample">The horizontal sampling factor of the chroma components.</param>
/// <param name="CompVSample">The vertical sampling factor of the chroma components.</param>
public readonly record struct ChromaSampling(
    int ChromaWidth,
    int MaxHSample,
    int MaxVSample,
    int CompHSample,
    int CompVSample)
{
    /// <summary>
    /// Gets the horizontal upsampling ratio (how many luma columns per chroma column).
    /// </summary>
    public int HRatio => MaxHSample / CompHSample;

    /// <summary>
    /// Gets the vertical upsampling ratio (how many luma rows per chroma row).
    /// </summary>
    public int VRatio => MaxVSample / CompVSample;
}
