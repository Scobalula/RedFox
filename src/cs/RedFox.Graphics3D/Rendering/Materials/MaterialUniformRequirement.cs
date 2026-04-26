using System;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Describes a uniform input required or accepted by a material type.
/// </summary>
public sealed record class MaterialUniformRequirement
{
    /// <summary>
    /// Gets the shader uniform name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the required uniform value type.
    /// </summary>
    public MaterialValueType ValueType { get; }

    /// <summary>
    /// Gets a value indicating whether the uniform is required.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Initializes a new <see cref="MaterialUniformRequirement"/> value.
    /// </summary>
    /// <param name="name">The shader uniform name.</param>
    /// <param name="valueType">The required uniform value type.</param>
    /// <param name="required">Whether the uniform is required.</param>
    public MaterialUniformRequirement(string name, MaterialValueType valueType, bool required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        ValueType = valueType;
        Required = required;
    }
}