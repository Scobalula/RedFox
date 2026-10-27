namespace RedFox.Graphics3D;

/// <summary>
/// Identifies how the PBR material types decode the two components of their normal inputs.
/// </summary>
public enum PbrNormalEncoding
{
    /// <summary>
    /// The tangent-space X and Y, with Z rebuilt.
    /// </summary>
    TwoChannel,

    /// <summary>
    /// A hemi-octahedral encoding whose components are stored as signed squares, as used by RE Engine.
    /// </summary>
    SignedSquareHemiOctahedral,
}
