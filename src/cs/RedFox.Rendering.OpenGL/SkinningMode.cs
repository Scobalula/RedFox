namespace RedFox.Rendering.OpenGL;

/// <summary>
/// Selects the skinning blend algorithm used by GPU skinning.
/// </summary>
public enum SkinningMode
{
    /// <summary>Uses weighted matrix blending.</summary>
    Linear = 0,

    /// <summary>Uses dual-quaternion blending for rotation/translation.</summary>
    DualQuaternion = 1
}
