namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies how mesh wireframes are drawn.
/// </summary>
public enum WireframeMode
{
    /// <summary>
    /// No wireframe is drawn.
    /// </summary>
    None,

    /// <summary>
    /// Visible edges are drawn over the shaded meshes.
    /// </summary>
    Overlay,

    /// <summary>
    /// Only the edges are drawn, without the shaded meshes.
    /// </summary>
    WireOnly,
}
