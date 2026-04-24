using System;

namespace RedFox.Graphics3D;

/// <summary>
/// Describes a material-owned texture binding.
/// </summary>
public sealed record class MaterialTextureBinding
{
    /// <summary>
    /// Gets the bound texture node.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the numeric texture slot.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// Gets the sampler uniform name associated with the binding.
    /// </summary>
    public string SamplerUniform { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTextureBinding"/> class.
    /// </summary>
    /// <param name="texture">The bound texture node.</param>
    /// <param name="slot">The numeric texture slot.</param>
    /// <param name="samplerUniform">The sampler uniform name.</param>
    public MaterialTextureBinding(Texture texture, int slot, string samplerUniform)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        SamplerUniform = samplerUniform ?? throw new ArgumentNullException(nameof(samplerUniform));
        Slot = slot;
    }
}