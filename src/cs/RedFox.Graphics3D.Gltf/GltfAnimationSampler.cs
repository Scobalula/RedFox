namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Defines an animation sampler that specifies input/output accessor pairs and
/// the interpolation method used between keyframes.
/// </summary>
public sealed class GltfAnimationSampler
{
    /// <summary>
    /// Gets or sets the index of the accessor containing keyframe timestamps (input).
    /// </summary>
    public int Input { get; set; }

    /// <summary>
    /// Gets or sets the index of the accessor containing keyframe values (output).
    /// </summary>
    public int Output { get; set; }

    /// <summary>
    /// Gets or sets the interpolation method (e.g., "LINEAR", "STEP", "CUBICSPLINE").
    /// </summary>
    public string Interpolation { get; set; } = GltfConstants.InterpolationLinear;
}
