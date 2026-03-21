// ------------------------------------------------------------------------
// AimConstraint.cs — Points a specific axis of a node toward a target
// ------------------------------------------------------------------------
// Unlike LookAtConstraint which aligns the forward axis, AimConstraint
// allows specifying which local axis (aim axis) should point toward the
// target, with a configurable up axis for twist control.
// Useful for arms, mechanical joints, and non-standard orientations.
// ------------------------------------------------------------------------

using System.Numerics;

namespace RedFox.Graphics3D.Solvers
{
    /// <summary>
    /// A post-animation constraint solver that aligns a configurable local axis
    /// of the source node toward a target position or node, with an independent
    /// up-axis for twist control.
    /// <para>
    /// More flexible than <see cref="LookAtConstraint"/>: lets you specify which
    /// local axis (X, Y, Z, or negated) should aim at the target. Useful for
    /// mechanical joints, gun barrels, or hierarchies with non-standard orientations.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="AimConstraint"/>.
    /// </remarks>
    /// <param name="name">Name of this solver.</param>
    /// <param name="source">The node to orient.</param>
    public class AimConstraint(string name, SceneNode source) : AnimationSamplerSolver(name)
    {
        /// <summary>
        /// Gets or sets the node whose rotation is modified to satisfy the constraint.
        /// </summary>
        public SceneNode Source { get; set; } = source;

        /// <summary>
        /// Gets or sets the target node. When not <see langword="null"/>, its active
        /// world position is read each frame. Overrides <see cref="TargetPosition"/>.
        /// </summary>
        public SceneNode? TargetNode { get; set; }

        /// <summary>
        /// Gets or sets a static world-space target position.
        /// Used when <see cref="TargetNode"/> is <see langword="null"/>.
        /// </summary>
        public Vector3 TargetPosition { get; set; }

        /// <summary>
        /// Gets or sets the local-space axis of the source node that should point
        /// toward the target. Defaults to <see cref="Vector3.UnitZ"/> (Z-forward).
        /// Example: <c>-Vector3.UnitX</c> to aim the negative-X axis at the target.
        /// </summary>
        public Vector3 AimAxis { get; set; } = Vector3.UnitZ;

        /// <summary>
        /// Gets or sets the world-space up reference vector for twist control.
        /// The solver minimizes the twist around the aim axis relative to this up.
        /// Defaults to <see cref="Vector3.UnitY"/>.
        /// </summary>
        public Vector3 UpVector { get; set; } = Vector3.UnitY;

        /// <summary>
        /// Computes the rotation that aligns <see cref="AimAxis"/> toward the target
        /// and blends with the source's current rotation.
        /// </summary>
        protected override void OnSolve(float time)
        {
            var sourcePos = Source.GetActiveWorldPosition();
            var targetPos = TargetNode is not null
                ? TargetNode.GetActiveWorldPosition()
                : TargetPosition;

            var aimDir = Vector3.Normalize(targetPos - sourcePos);
            if (float.IsNaN(aimDir.X)) return;

            // Compute the rotation that takes AimAxis to aimDir
            var fromDir = Vector3.Normalize(AimAxis);
            var aimRotation = RotationBetween(fromDir, aimDir);

            // Apply twist correction using the up vector
            var rotatedUp = Vector3.Transform(Vector3.UnitY, aimRotation);
            var projectedUp = Vector3.Normalize(UpVector - Vector3.Dot(UpVector, aimDir) * aimDir);

            if (!float.IsNaN(projectedUp.X) && !float.IsNaN(rotatedUp.X))
            {
                var twistRotation = RotationBetween(
                    Vector3.Normalize(rotatedUp - Vector3.Dot(rotatedUp, aimDir) * aimDir),
                    projectedUp);
                aimRotation = twistRotation * aimRotation;
            }

            var current = Source.GetActiveWorldRotation();
            Source.LiveTransform.WorldRotation = Quaternion.Slerp(current, Quaternion.Normalize(aimRotation), CurrentWeight);
            Source.LiveTransform.LocalRotation = null;
        }

        /// <summary>
        /// Computes the shortest rotation quaternion that takes vector <paramref name="from"/>
        /// to vector <paramref name="to"/>. Both inputs must be normalized.
        /// </summary>
        private static Quaternion RotationBetween(Vector3 from, Vector3 to)
        {
            var dot = Vector3.Dot(from, to);

            if (dot >= 1.0f - 1e-6f)
                return Quaternion.Identity;

            if (dot <= -1.0f + 1e-6f)
            {
                // 180-degree rotation: find an arbitrary perpendicular axis
                var perp = Vector3.Cross(Vector3.UnitX, from);
                if (perp.LengthSquared() < 1e-6f)
                    perp = Vector3.Cross(Vector3.UnitY, from);
                perp = Vector3.Normalize(perp);
                return new Quaternion(perp, 0f);
            }

            var axis = Vector3.Cross(from, to);
            return Quaternion.Normalize(new Quaternion(axis.X, axis.Y, axis.Z, 1f + dot));
        }
    }
}
