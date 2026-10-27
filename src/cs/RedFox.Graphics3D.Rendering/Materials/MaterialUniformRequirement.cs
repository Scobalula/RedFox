using System;
using System.Text.Json.Serialization;

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
    /// Gets the components sent when the material has no attribute named <see cref="Name"/>, or <see langword="null"/> to leave the uniform unset.
    /// </summary>
    public IReadOnlyList<float>? DefaultValue { get; }

    /// <summary>
    /// Initializes a new <see cref="MaterialUniformRequirement"/> value.
    /// </summary>
    /// <param name="name">The shader uniform name.</param>
    /// <param name="valueType">The required uniform value type.</param>
    /// <param name="required">Whether the uniform is required.</param>
    public MaterialUniformRequirement(string name, MaterialValueType valueType, bool required) : this(name, valueType, required, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="MaterialUniformRequirement"/> value with a default value.
    /// </summary>
    /// <param name="name">The shader uniform name.</param>
    /// <param name="valueType">The required uniform value type.</param>
    /// <param name="required">Whether the uniform is required.</param>
    /// <param name="defaultValue">The one to four components sent when the material has no matching attribute, or <see langword="null"/> for none.</param>
    [JsonConstructor]
    public MaterialUniformRequirement(string name, MaterialValueType valueType, bool required, IReadOnlyList<float>? defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (defaultValue is { Count: < 1 or > 4 })
        {
            throw new ArgumentOutOfRangeException(nameof(defaultValue), defaultValue.Count, "A uniform default value has between one and four components.");
        }

        Name = name;
        ValueType = valueType;
        Required = required;
        DefaultValue = defaultValue is null ? null : [.. defaultValue];
    }
}