using System;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Describes a texture input required or accepted by a material type.
/// </summary>
public sealed record class MaterialTextureRequirement
{
    /// <summary>
    /// Gets the material texture binding name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the texture slot expected by the shader.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// Gets a value indicating whether the texture is required.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Initializes a new <see cref="MaterialTextureRequirement"/> value.
    /// </summary>
    /// <param name="name">The material texture binding name.</param>
    /// <param name="slot">The texture slot expected by the shader.</param>
    /// <param name="required">Whether the texture is required.</param>
    public MaterialTextureRequirement(string name, int slot, bool required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (slot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Texture slot cannot be negative.");
        }

        Name = name;
        Slot = slot;
        Required = required;
    }
}