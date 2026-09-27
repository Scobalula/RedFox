using System;

namespace RedFox.Graphics3D;

/// <summary>
/// Describes a material-owned texture binding.
/// </summary>
/// <param name="texture">The bound texture node.</param>
/// <param name="slot">The numeric texture slot.</param>
/// <param name="samplerUniform">The sampler uniform name.</param>
public sealed record class MaterialTextureBinding(Texture texture, int slot, string samplerUniform)
{
    /// <summary>
    /// Gets the bound texture node.
    /// </summary>
    public Texture Texture { get; } = texture ?? throw new ArgumentNullException(nameof(texture));

    /// <summary>
    /// Gets the numeric texture slot.
    /// </summary>
    public int Slot { get; } = slot;

    /// <summary>
    /// Gets the sampler uniform name associated with the binding.
    /// </summary>
    public string SamplerUniform { get; } = samplerUniform ?? throw new ArgumentNullException(nameof(samplerUniform));
}
