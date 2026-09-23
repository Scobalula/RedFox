namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies the stage targeted by a GPU shader.
/// </summary>
public enum ShaderStage
{
    /// <summary>
    /// Vertex-processing stage.
    /// </summary>
    Vertex = 0,

    /// <summary>
    /// Fragment or pixel-processing stage.
    /// </summary>
    Fragment = 1,

    /// <summary>
    /// Compute stage.
    /// </summary>
    Compute = 2,
}