using System.Collections.Generic;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Represents a registry of backend material pipeline definitions.
/// </summary>
public interface IMaterialTypeRegistry
{
    /// <summary>
    /// Gets the names of all registered material types.
    /// </summary>
    IReadOnlyCollection<string> RegisteredNames { get; }

    /// <summary>
    /// Registers a material type definition.
    /// </summary>
    /// <param name="definition">The definition to register.</param>
    void Register(MaterialTypeDefinition definition);

    /// <summary>
    /// Resolves a material type definition by name.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <returns>The resolved material type definition.</returns>
    MaterialTypeDefinition Get(string name);

    /// <summary>
    /// Returns whether a material type has been registered.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <returns><see langword="true"/> when the material type has been registered; otherwise <see langword="false"/>.</returns>
    bool Contains(string name);
}