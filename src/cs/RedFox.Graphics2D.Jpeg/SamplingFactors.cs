namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Represents the horizontal and vertical sampling factors for each component (Y, Cb, Cr)
/// in a JPEG image, defining the chroma subsampling configuration.
/// </summary>
/// <param name="YH">Horizontal sampling factor for the luminance (Y) component.</param>
/// <param name="YV">Vertical sampling factor for the luminance (Y) component.</param>
/// <param name="CbH">Horizontal sampling factor for the blue-difference chroma (Cb) component.</param>
/// <param name="CbV">Vertical sampling factor for the blue-difference chroma (Cb) component.</param>
/// <param name="CrH">Horizontal sampling factor for the red-difference chroma (Cr) component.</param>
/// <param name="CrV">Vertical sampling factor for the red-difference chroma (Cr) component.</param>
public readonly record struct SamplingFactors(
    int YH,
    int YV,
    int CbH,
    int CbV,
    int CrH,
    int CrV)
{
    /// <summary>
    /// Gets the maximum horizontal sampling factor across all components.
    /// </summary>
    public int MaxH => YH;

    /// <summary>
    /// Gets the maximum vertical sampling factor across all components.
    /// </summary>
    public int MaxV => YV;

    /// <summary>
    /// Creates sampling factors from the specified chroma subsampling mode.
    /// </summary>
    /// <param name="subsampling">The chroma subsampling configuration to use.</param>
    /// <returns>A <see cref="SamplingFactors"/> instance with the corresponding sampling factors.</returns>
    public static SamplingFactors FromSubsampling(JpegChromaSubsampling subsampling) => subsampling switch
    {
        JpegChromaSubsampling.Yuv444 => new SamplingFactors(1, 1, 1, 1, 1, 1),
        JpegChromaSubsampling.Yuv422 => new SamplingFactors(2, 1, 1, 1, 1, 1),
        _ => new SamplingFactors(2, 2, 1, 1, 1, 1), // Yuv420
    };
}
