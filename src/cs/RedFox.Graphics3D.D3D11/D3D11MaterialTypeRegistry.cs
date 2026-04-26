using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the Direct3D 11 material-type registry.
/// </summary>
public sealed class D3D11MaterialTypeRegistry : MaterialTypeRegistry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11MaterialTypeRegistry"/> class.
    /// </summary>
    public D3D11MaterialTypeRegistry()
        : base(BuiltInMaterialTypes.CreateDefinitions(D3D11MaterialShaderFactory.Instance))
    {
    }
}