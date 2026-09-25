namespace RedFox.Imaging.Processing;

/// <summary>
/// Specifies how source pixels are sampled when an image is resized.
/// </summary>
public enum ResamplingFilter
{
    /// <summary>
    /// Picks the single closest source pixel. Preserves hard edges and exact values.
    /// </summary>
    NearestNeighbor,

    /// <summary>
    /// Interpolates linearly between the four closest source pixels. Suited to upscaling and mild downscaling.
    /// </summary>
    Bilinear,

    /// <summary>
    /// Averages every source pixel covered by the destination pixel. Suited to downscaling.
    /// </summary>
    Box,
}
