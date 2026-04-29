using System.Numerics;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a skeleton bone scene node and owns its debug-bone rendering settings.
/// </summary>
public class SkeletonBone : SceneNode
{
    private bool _showSkeletonBone = true;

    /// <summary>
    /// Gets or sets a value indicating whether this skeleton bone should be rendered.
    /// </summary>
    public bool ShowSkeletonBone
    {
        get => _showSkeletonBone;
        set
        {
            if (_showSkeletonBone == value)
            {
                return;
            }

            _showSkeletonBone = value;
            Scene?.NotifyChanged(SceneChangeKind.NodeChanged, this);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this bone should render on top of scene geometry.
    /// </summary>
    public bool RenderBoneOnTop { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum local axis size rendered at this bone.
    /// </summary>
    public float BoneAxisSize { get; set; } = 0.2f;

    /// <summary>
    /// Gets or sets the parent-length multiplier used to scale this bone's axes.
    /// </summary>
    public float BoneAxisScaleFromParent { get; set; } = 0.2f;

    /// <summary>
    /// Gets or sets the maximum rendered axis size. Set to 0 to disable clamping.
    /// </summary>
    public float BoneAxisMaxSize { get; set; } = 2.0f;

    /// <summary>
    /// Gets or sets the pixel width of this bone's rendered lines.
    /// </summary>
    public float BoneLineWidth { get; set; } = 1.15f;

    /// <summary>
    /// Gets or sets the color of this bone's rendered X axis.
    /// </summary>
    public Vector4 BoneAxisXColor { get; set; } = new(0.9f, 0.25f, 0.25f, 0.95f);

    /// <summary>
    /// Gets or sets the color of this bone's rendered Y axis.
    /// </summary>
    public Vector4 BoneAxisYColor { get; set; } = new(0.25f, 0.9f, 0.25f, 0.95f);

    /// <summary>
    /// Gets or sets the color of this bone's rendered Z axis.
    /// </summary>
    public Vector4 BoneAxisZColor { get; set; } = new(0.25f, 0.45f, 0.95f, 0.95f);

    /// <summary>
    /// Gets or sets the color of this bone's parent-to-child connection line.
    /// </summary>
    public Vector4 BoneConnectionColor { get; set; } = new(0.88f, 0.88f, 0.76f, 0.9f);

    /// <summary>
    /// Gets or sets a value indicating whether this bone's name should determine its connection color.
    /// </summary>
    public bool UseBoneNameHashColor { get; set; } = true;

    /// <summary>
    /// Gets or sets the saturation used for this bone's deterministic name color.
    /// </summary>
    public float BoneNameColorSaturation { get; set; } = 0.62f;

    /// <summary>
    /// Gets or sets the value used for this bone's deterministic name color.
    /// </summary>
    public float BoneNameColorValue { get; set; } = 0.95f;

    public SkeletonBone(string name) : base(name) { }

    public SkeletonBone(string name, SceneNodeFlags flags) : base(name, flags) { }

    /// <inheritdoc/>
    public override IRenderHandle? CreateRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
    {
        return new SkeletonBoneRenderHandle(graphicsDevice, materialTypes, this);
    }

    /// <inheritdoc/>
    public override bool TryGetSceneBounds(out SceneBounds bounds)
    {
        Matrix4x4 world = GetActiveWorldMatrix();
        Vector3 origin = Vector3.Transform(Vector3.Zero, world);
        Vector3 min = origin;
        Vector3 max = origin;

        // Mirror the same local-space line construction used by OpenGL bone rendering.
        float axisSize = MathF.Max(BoneAxisSize, 0.0f);
        if (Parent is not null && Matrix4x4.Invert(GetActiveLocalMatrix(), out Matrix4x4 inverseLocal))
        {
            Vector3 parentOriginInBoneLocal = Vector3.Transform(Vector3.Zero, inverseLocal);
            Vector3 parentWorld = Vector3.Transform(parentOriginInBoneLocal, world);
            Expand(ref min, ref max, parentWorld);

            float parentLength = parentOriginInBoneLocal.Length();
            if (parentLength > 1e-6f)
            {
                axisSize = MathF.Max(axisSize, parentLength * MathF.Max(BoneAxisScaleFromParent, 0.0f));
            }
        }

        if (BoneAxisMaxSize > 0.0f)
        {
            axisSize = MathF.Min(axisSize, BoneAxisMaxSize);
        }

        if (axisSize > 0.0f)
        {
            Expand(ref min, ref max, Vector3.Transform(Vector3.UnitX * axisSize, world));
            Expand(ref min, ref max, Vector3.Transform(Vector3.UnitY * axisSize, world));
            Expand(ref min, ref max, Vector3.Transform(Vector3.UnitZ * axisSize, world));
        }

        bounds = new SceneBounds(min, max);
        return true;
    }

    private static void Expand(ref Vector3 min, ref Vector3 max, Vector3 point)
    {
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
    }
}
