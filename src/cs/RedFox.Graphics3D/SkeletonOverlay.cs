using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Controls skeleton-bone visualization for a scene.
/// </summary>
public sealed class SkeletonOverlay : SceneNode
{
    /// <summary>
    /// Gets or sets a value indicating whether skeleton bones should be rendered.
    /// </summary>
    public bool ShowSkeletonBones { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether bones should render on top of scene geometry.
    /// </summary>
    public bool BonesRenderOnTop { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum local axis size rendered at each bone.
    /// </summary>
    public float BoneAxisSize { get; set; } = 0.2f;

    /// <summary>
    /// Gets or sets the parent-length multiplier used to scale bone axes.
    /// </summary>
    public float BoneAxisScaleFromParent { get; set; } = 0.2f;

    /// <summary>
    /// Gets or sets the maximum rendered axis size. Set to 0 to disable clamping.
    /// </summary>
    public float BoneAxisMaxSize { get; set; } = 2.0f;

    /// <summary>
    /// Gets or sets the pixel width of rendered bone lines.
    /// </summary>
    public float BoneLineWidth { get; set; } = 1.15f;

    /// <summary>
    /// Gets or sets the color of the rendered X axis at each bone.
    /// </summary>
    public Vector4 BoneAxisXColor { get; set; } = new(0.9f, 0.25f, 0.25f, 0.95f);

    /// <summary>
    /// Gets or sets the color of the rendered Y axis at each bone.
    /// </summary>
    public Vector4 BoneAxisYColor { get; set; } = new(0.25f, 0.9f, 0.25f, 0.95f);

    /// <summary>
    /// Gets or sets the color of the rendered Z axis at each bone.
    /// </summary>
    public Vector4 BoneAxisZColor { get; set; } = new(0.25f, 0.45f, 0.95f, 0.95f);

    /// <summary>
    /// Gets or sets the color of the parent-to-child bone connection line.
    /// </summary>
    public Vector4 BoneConnectionColor { get; set; } = new(0.88f, 0.88f, 0.76f, 0.9f);

    /// <summary>
    /// Gets or sets a value indicating whether bone names should determine connection colors.
    /// </summary>
    public bool UseBoneNameHashColor { get; set; } = true;

    /// <summary>
    /// Gets or sets the saturation used for deterministic bone-name colors.
    /// </summary>
    public float BoneNameColorSaturation { get; set; } = 0.62f;

    /// <summary>
    /// Gets or sets the value used for deterministic bone-name colors.
    /// </summary>
    public float BoneNameColorValue { get; set; } = 0.95f;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonOverlay"/> class.
    /// </summary>
    public SkeletonOverlay() : this("SkeletonOverlay")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonOverlay"/> class.
    /// </summary>
    /// <param name="name">The node name.</param>
    public SkeletonOverlay(string name) : base(name)
    {
    }
}