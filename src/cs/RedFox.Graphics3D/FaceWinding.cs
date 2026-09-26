namespace RedFox.Graphics3D;

/// <summary>
/// Identifies the winding order that defines the front face of a triangle, used by mesh data and pipeline state.
/// </summary>
public enum FaceWinding
{
    /// <summary>
    /// Clockwise vertices define a front-facing primitive.
    /// </summary>
    Clockwise = 0,

    /// <summary>
    /// Counter-clockwise vertices define a front-facing primitive.
    /// </summary>
    CounterClockwise = 1,
}