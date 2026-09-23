using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Creates backend shader resources for material type descriptors.
/// </summary>
public interface IMaterialShaderFactory
{
    /// <summary>
    /// Creates a shader for the supplied material shader name and stage.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that owns the shader.</param>
    /// <param name="shaderName">The shader source name.</param>
    /// <param name="stage">The shader stage to create.</param>
    /// <returns>The created GPU shader.</returns>
    IGpuShader CreateShader(IGraphicsDevice graphicsDevice, string shaderName, ShaderStage stage);
}