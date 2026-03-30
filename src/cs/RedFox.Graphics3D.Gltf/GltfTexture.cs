namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// References a <see cref="GltfImage"/> via an optional sampler for filtering and wrapping.
/// </summary>
public sealed class GltfTexture
{
    /// <summary>
    /// Gets or sets the optional name of this texture.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the index of the image used by this texture, or -1 if none.
    /// </summary>
    public int Source { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the sampler used by this texture, or -1 for default.
    /// </summary>
    public int Sampler { get; set; } = -1;
}
