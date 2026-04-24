namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Identifies the primitive topology used for draw calls.
/// </summary>
public enum PrimitiveTopology
{
    /// <summary>
    /// Draws isolated points.
    /// </summary>
    Points = 0,

    /// <summary>
    /// Draws isolated line segments.
    /// </summary>
    Lines = 1,

    /// <summary>
    /// Draws a connected line strip.
    /// </summary>
    LineStrip = 2,

    /// <summary>
    /// Draws isolated triangles.
    /// </summary>
    Triangles = 3,

    /// <summary>
    /// Draws a connected triangle strip.
    /// </summary>
    TriangleStrip = 4,
}