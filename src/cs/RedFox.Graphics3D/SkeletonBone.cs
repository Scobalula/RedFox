using System.Numerics;

namespace RedFox.Graphics3D;

public class SkeletonBone : SceneNode
{
    private const float DefaultAxisSize = 0.2f;
    private const float AxisScaleFromParent = 0.2f;
    private const float AxisMaxSize = 2.0f;

    public SkeletonBone(string name) : base(name) { }

    public SkeletonBone(string name, SceneNodeFlags flags) : base(name, flags) { }

    /// <inheritdoc/>
    public override bool TryGetSceneBounds(out SceneBounds bounds)
    {
        Matrix4x4 world = GetActiveWorldMatrix();
        Vector3 origin = Vector3.Transform(Vector3.Zero, world);
        Vector3 min = origin;
        Vector3 max = origin;

        // Mirror the same local-space line construction used by OpenGL bone rendering.
        float axisSize = DefaultAxisSize;
        if (Parent is not null && Matrix4x4.Invert(GetActiveLocalMatrix(), out Matrix4x4 inverseLocal))
        {
            Vector3 parentOriginInBoneLocal = Vector3.Transform(Vector3.Zero, inverseLocal);
            Vector3 parentWorld = Vector3.Transform(parentOriginInBoneLocal, world);
            Expand(ref min, ref max, parentWorld);

            float parentLength = parentOriginInBoneLocal.Length();
            if (parentLength > 1e-6f)
            {
                axisSize = MathF.Max(axisSize, parentLength * AxisScaleFromParent);
            }
        }

        if (AxisMaxSize > 0.0f)
        {
            axisSize = MathF.Min(axisSize, AxisMaxSize);
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
