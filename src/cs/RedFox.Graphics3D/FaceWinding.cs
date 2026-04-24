namespace RedFox.Graphics3D;

/// <summary>
/// Identifies the front-face winding used by the scene or pipeline state.
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