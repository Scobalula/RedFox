using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents the concrete OpenGL material-type registry.
/// </summary>
internal sealed class OpenGlMaterialTypeRegistry : MaterialTypeRegistry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlMaterialTypeRegistry"/> class.
    /// </summary>
    public OpenGlMaterialTypeRegistry()
        : base(BuiltInMaterialTypes.CreateDefinitions(OpenGlMaterialShaderFactory.Instance))
    {
    }
}