namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Defines an animation channel that connects a sampler to a target node property.
/// </summary>
public sealed class GltfAnimationChannel
{
    /// <summary>
    /// Gets or sets the index of the sampler that provides keyframe data for this channel.
    /// </summary>
    public int Sampler { get; set; }

    /// <summary>
    /// Gets or sets the index of the target node, or -1 if unspecified.
    /// </summary>
    public int TargetNode { get; set; } = -1;

    /// <summary>
    /// Gets or sets the target path string (e.g., "translation", "rotation", "scale", "weights").
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;
}
