namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Defines texture sampling parameters such as filtering and wrapping modes.
/// </summary>
public sealed class GltfSampler
{
    /// <summary>
    /// Gets or sets the optional name of this sampler.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the magnification filter mode.
    /// </summary>
    public int MagFilter { get; set; }

    /// <summary>
    /// Gets or sets the minification filter mode.
    /// </summary>
    public int MinFilter { get; set; }

    /// <summary>
    /// Gets or sets the S (U) wrapping mode. Defaults to REPEAT (10497).
    /// </summary>
    public int WrapS { get; set; } = 10497;

    /// <summary>
    /// Gets or sets the T (V) wrapping mode. Defaults to REPEAT (10497).
    /// </summary>
    public int WrapT { get; set; } = 10497;
}
