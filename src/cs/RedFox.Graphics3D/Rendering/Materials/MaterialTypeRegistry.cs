using System;
using System.Collections.Generic;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Provides a backend-neutral material type registry implementation.
/// </summary>
public class MaterialTypeRegistry : IMaterialTypeRegistry
{
    private readonly Dictionary<string, MaterialTypeDefinition> _definitions = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTypeRegistry"/> class.
    /// </summary>
    public MaterialTypeRegistry()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTypeRegistry"/> class with material definitions.
    /// </summary>
    /// <param name="definitions">The material definitions to register.</param>
    public MaterialTypeRegistry(IEnumerable<MaterialTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (MaterialTypeDefinition definition in definitions)
        {
            Register(definition);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> RegisteredNames => _definitions.Keys;

    /// <inheritdoc/>
    public void Register(MaterialTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Name] = definition;
    }

    /// <inheritdoc/>
    public MaterialTypeDefinition Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions[name];
    }

    /// <inheritdoc/>
    public bool Contains(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions.ContainsKey(name);
    }
}