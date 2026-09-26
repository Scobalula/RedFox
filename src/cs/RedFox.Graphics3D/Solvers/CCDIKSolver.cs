// ------------------------------------------------------------------------
// CCDIKSolver.cs — Cyclic Coordinate Descent inverse kinematics solver
// ------------------------------------------------------------------------
// CCD IK is an iterative solver for N-bone chains of arbitrary length.
// It works by iterating from the tip toward the root, rotating each joint
// so the end-effector moves closer to the target. This makes it ideal for
// tails, tentacles, spines, and other multi-segment chains.
// Extends AnimationSamplerSolver for pipeline integration.
// ------------------------------------------------------------------------

using System.Numerics;

namespace RedFox.Graphics3D.Solvers;

/// <summary>
/// A Cyclic Coordinate Descent (CCD) inverse kinematics solver for arbitrary-
/// length bone chains. Iteratively adjusts joint rotations from the tip toward
/// the root until the end-effector converges on the target.
/// </summary>
/// <param name="name">Name of this solver node.</param>
public class CCDIKSolver(string name) : AnimationSamplerSolver(name)
{
    /// <summary>
    /// Gets the ordered joint chain from root (index 0) to tip (last index).
    /// The last node in the list is the end-effector driven toward the target.
    /// </summary>
    public List<SceneNode> Chain { get; } = [];

    /// <summary>
    /// Gets or sets a target node whose active world position the end-effector
    /// is driven toward. Overrides <see cref="TargetPosition"/> when not
    /// <see langword="null"/>.
    /// </summary>
    public SceneNode? TargetNode { get; set; }

    /// <summary>
    /// Gets or sets a static world-space target position used when
    /// <see cref="TargetNode"/> is <see langword="null"/>.
    /// </summary>
    public Vector3 TargetPosition { get; set; }

    /// <summary>
    /// Gets or sets the world space offset added to the target.
    /// </summary>
    public Vector3 TargetOffset { get; set; }

    /// <summary>
    /// Gets or sets whether the end of the chain takes the world rotation of <see cref="TargetNode"/>.
    /// </summary>
    public bool UseTargetRotation { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of full CCD iterations (tip-to-root sweeps).
    /// Higher values improve accuracy at the cost of performance.
    /// Default: <c>10</c>.
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// Gets or sets the distance tolerance (in world units). If the end-effector
    /// is within this distance of the target the solver terminates early.
    /// Default: <c>0.001</c>.
    /// </summary>
    public float Tolerance { get; set; } = 0.001f;

    /// <summary>
    /// Gets or sets an optional per-joint angle limit (in radians) per iteration.
    /// When set to a positive value, each CCD step is clamped to at most this
    /// rotation. Set to <c>0</c> or negative to disable clamping.
    /// Default: <c>0</c> (disabled).
    /// </summary>
    public float AngleLimit { get; set; }

    /// <summary>
    /// Runs the CCD iterations, adjusting each joint's rotation so the tip
    /// converges on the target.
    /// </summary>
    protected override void OnSolve(float time)
    {
        if (Chain.Count < 2) return;

        var target = (TargetNode is not null
            ? TargetNode.GetActiveWorldPosition()
            : TargetPosition) + TargetOffset;

        int tipIndex = Chain.Count - 1;
        float toleranceSq = Tolerance * Tolerance;

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            // Check convergence
            var tipPos = Chain[tipIndex].GetActiveWorldPosition();
            if (Vector3.DistanceSquared(tipPos, target) <= toleranceSq)
                break;

            // Iterate from the joint just before the tip toward the root
            for (int i = tipIndex - 1; i >= 0; i--)
            {
                var joint = Chain[i];
                var jointPos = joint.GetActiveWorldPosition();

                // Current tip position (recalculated after previous joint adjustments)
                tipPos = Chain[tipIndex].GetActiveWorldPosition();

                // Direction from this joint to the current tip and to the target
                var toTip    = tipPos - jointPos;
                var toTarget = target - jointPos;

                float toTipLen = toTip.Length();
                float toTargetLen = toTarget.Length();
                if (toTipLen < 1e-7f || toTargetLen < 1e-7f) continue;

                toTip    /= toTipLen;
                toTarget /= toTargetLen;

                // Compute the shortest-arc rotation
                var rotation = RotationFromTo(toTip, toTarget);

                // Optionally clamp the rotation angle
                if (AngleLimit > 0f)
                    rotation = ClampRotation(rotation, AngleLimit);

                // Apply blended rotation
                var currentRot = joint.GetActiveWorldRotation();
                var desired = Quaternion.Normalize(rotation * currentRot);
                joint.LiveTransform.WorldRotation = Quaternion.Slerp(currentRot, desired, CurrentWeight);
                joint.LiveTransform.LocalRotation = null;
            }
        }

        if (UseTargetRotation && TargetNode is not null)
        {
            Chain[^1].LiveTransform.WorldRotation = Quaternion.Slerp(Chain[^1].GetActiveWorldRotation(), TargetNode.GetActiveWorldRotation(), CurrentWeight);
            Chain[^1].LiveTransform.LocalRotation = null;
        }
    }

    private static Quaternion RotationFromTo(Vector3 from, Vector3 to)
    {
        var dot = Vector3.Dot(from, to);
        if (dot >= 1f - 1e-6f) return Quaternion.Identity;

        if (dot <= -1f + 1e-6f)
        {
            var perp = Vector3.Cross(Vector3.UnitX, from);
            if (perp.LengthSquared() < 1e-6f)
                perp = Vector3.Cross(Vector3.UnitY, from);
            return new Quaternion(Vector3.Normalize(perp), 0f);
        }

        var cross = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(cross.X, cross.Y, cross.Z, 1f + dot));
    }

    private static Quaternion ClampRotation(Quaternion q, float maxRadians)
    {
        // Ensure the quaternion is in the short-arc hemisphere
        if (q.W < 0f)
            q = new Quaternion(-q.X, -q.Y, -q.Z, -q.W);

        float halfAngle = MathF.Acos(Math.Clamp(q.W, -1f, 1f));
        float fullAngle = 2f * halfAngle;

        if (fullAngle <= maxRadians)
            return q;

        // Scale the axis component down to the clamped angle
        float scale = MathF.Sin(maxRadians * 0.5f) / MathF.Sin(halfAngle);
        return new Quaternion(q.X * scale, q.Y * scale, q.Z * scale, MathF.Cos(maxRadians * 0.5f));
    }
}
