namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies which triangle faces are discarded during rasterization.
/// </summary>
public enum CullMode
{
    /// <summary>
    /// No triangle faces are culled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Front-facing triangles are culled.
    /// </summary>
    Front = 1,

    /// <summary>
    /// Back-facing triangles are culled.
    /// </summary>
    Back = 2,
}