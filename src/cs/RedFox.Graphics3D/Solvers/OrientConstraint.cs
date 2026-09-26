using System.Numerics;

namespace RedFox.Graphics3D.Solvers;

/// <summary>
/// Represents a Maya-style orient constraint that drives only the rotation of a constrained node from a source node.
/// </summary>
public class OrientConstraint(string name, SceneNode constrainedNode, SceneNode sourceNode) : AnimationSamplerSolver(name)
{
    /// <summary>
    /// Gets or sets the node whose rotation is driven by this constraint.
    /// </summary>
    public SceneNode ConstrainedNode { get; set; } = constrainedNode;

    /// <summary>
    /// Gets or sets the source node whose active rotation is used as the orient-constraint driver.
    /// </summary>
    public SceneNode SourceNode { get; set; } = sourceNode;

    /// <summary>
    /// Gets or sets the local rotation offset preserved between the source and constrained nodes.
    /// </summary>
    public Quaternion RotationOffset { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Gets or sets whether rotation about the local X axis is left unconstrained.
    /// </summary>
    public bool SkipX { get; set; }

    /// <summary>
    /// Gets or sets whether rotation about the local Y axis is left unconstrained.
    /// </summary>
    public bool SkipY { get; set; }

    /// <summary>
    /// Gets or sets whether rotation about the local Z axis is left unconstrained.
    /// </summary>
    public bool SkipZ { get; set; }

    /// <summary>
    /// Solves the orient constraint by matching the constrained node rotation to the source node with the configured offset.
    /// Skipped axes keep their current rotation, measured as XYZ Euler angles in the constrained node's local space.
    /// </summary>
    /// <param name="time">The current frame time.</param>
    protected override void OnSolve(float time)
    {
        _ = time;

        Quaternion targetWorldRotation = Quaternion.Normalize(SourceNode.GetActiveWorldRotation() * RotationOffset);
        Quaternion blendedWorldRotation = Quaternion.Slerp(ConstrainedNode.GetActiveWorldRotation(), targetWorldRotation, CurrentWeight);
        Quaternion parentWorldRotation = ConstrainedNode.Parent?.GetActiveWorldRotation() ?? Quaternion.Identity;
        Quaternion localRotation = Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * blendedWorldRotation);

        if (SkipX || SkipY || SkipZ)
        {
            Vector3 current = ToEulerXYZ(Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * ConstrainedNode.GetActiveWorldRotation()));
            Vector3 solved = ToEulerXYZ(localRotation);

            localRotation = FromEulerXYZ(new Vector3(SkipX ? current.X : solved.X, SkipY ? current.Y : solved.Y, SkipZ ? current.Z : solved.Z));
        }

        if (ConstrainedNode.Parent is not null)
        {
            ConstrainedNode.LiveTransform.LocalRotation = localRotation;
            ConstrainedNode.LiveTransform.WorldRotation = null;
            return;
        }

        ConstrainedNode.LiveTransform.WorldRotation = localRotation;
        ConstrainedNode.LiveTransform.LocalRotation = null;
    }

    private static Vector3 ToEulerXYZ(Quaternion rotation)
    {
        Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(rotation);

        return new Vector3(MathF.Atan2(matrix.M23, matrix.M33), MathF.Asin(Math.Clamp(-matrix.M13, -1.0f, 1.0f)), MathF.Atan2(matrix.M12, matrix.M11));
    }

    private static Quaternion FromEulerXYZ(Vector3 angles) => Quaternion.Concatenate(Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(Vector3.UnitX, angles.X), Quaternion.CreateFromAxisAngle(Vector3.UnitY, angles.Y)), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angles.Z));
}
