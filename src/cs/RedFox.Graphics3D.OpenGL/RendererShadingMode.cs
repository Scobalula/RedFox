namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Specifies the shading mode used by the geometry render pass.
/// </summary>
public enum RendererShadingMode
{
    /// <summary>Physically-based rendering with metallic-roughness workflow.</summary>
    Pbr,

    /// <summary>Unlit rendering showing diffuse colour only.</summary>
    Fullbright
}
