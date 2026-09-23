using System;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Describes a named material type and the pipeline factory used to build it.
/// </summary>
public sealed record class MaterialTypeDefinition
{
    /// <summary>
    /// Gets the material type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the backend-neutral material type descriptor.
    /// </summary>
    public MaterialTypeDescriptor Descriptor { get; }

    /// <summary>
    /// Initializes a new <see cref="MaterialTypeDefinition"/> value from a backend-neutral descriptor.
    /// </summary>
    /// <param name="descriptor">The material type descriptor.</param>
    public MaterialTypeDefinition(MaterialTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Name = descriptor.Name;
        Descriptor = descriptor;
    }
}