namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Represents a glTF animation composed of channels and samplers that define
/// how node properties change over time.
/// </summary>
public sealed class GltfAnimation
{
    /// <summary>
    /// Gets or sets the optional name of this animation.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the list of animation channels. Each channel targets a specific
    /// node property (translation, rotation, scale, or weights).
    /// </summary>
    public List<GltfAnimationChannel> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of animation samplers that define keyframe data
    /// and interpolation behavior.
    /// </summary>
    public List<GltfAnimationSampler> Samplers { get; set; } = [];
}
