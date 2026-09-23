namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Identifies the kind of pipeline described by a material type.
/// </summary>
public enum MaterialPipelineKind
{
    /// <summary>
    /// The material type describes a graphics pipeline.
    /// </summary>
    Graphics = 0,

    /// <summary>
    /// The material type describes a compute pipeline.
    /// </summary>
    Compute = 1,
}