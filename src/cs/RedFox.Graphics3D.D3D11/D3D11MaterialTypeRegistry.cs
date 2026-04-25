using System.Collections.Generic;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the placeholder D3D11 material-type registry for the backend skeleton.
/// </summary>
public sealed class D3D11MaterialTypeRegistry : IMaterialTypeRegistry
{
    internal D3D11MaterialTypeRegistry()
    {
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> RegisteredNames => throw D3D11BackendSkeleton.NotImplemented();

    /// <inheritdoc/>
    public void Register(MaterialTypeDefinition definition)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public MaterialTypeDefinition Get(string name)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public bool Contains(string name)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }
}