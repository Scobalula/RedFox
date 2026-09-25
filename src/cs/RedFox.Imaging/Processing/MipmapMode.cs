namespace RedFox.Imaging.Processing;

/// <summary>
/// Specifies how the contents of an image are treated when generating mip maps.
/// </summary>
public enum MipmapMode
{
    /// <summary>
    /// Box-filters each channel independently. sRGB formats are filtered in linear space.
    /// </summary>
    Standard,

    /// <summary>
    /// Treats RGB as a tangent-space normal, box-filters the vectors, and renormalises them.
    /// Unsigned formats are expanded from [0, 1] to [-1, 1]; two-channel formats reconstruct Z before filtering.
    /// </summary>
    NormalMap,
}
