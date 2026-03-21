// ------------------------------------------------------------------------
// LookAtConstraint.cs — Orients a node to face a target position
// ------------------------------------------------------------------------
// A post-animation solver that rotates a source node so its forward axis
// points toward a target node or world position. Commonly used for head/
// eye tracking, turrets, cameras, etc.
// ------------------------------------------------------------------------

using System.Numerics;

namespace RedFox.Graphics3D.Solvers
{
    /// <summary>
    /// A post-animation constraint solver that orients a source <see cref="SceneNode"/>
    /// so that its forward axis points toward a target position or node.
    /// The constraint blends with the existing rotation using <see cref="AnimationSamplerSolver.CurrentWeight"/>.
    /// <para>
    /// Common use cases: head tracking, eye look-at, camera follow, turret aiming.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var lookAt = new LookAtConstraint("HeadLookAt", headBone)
    /// {
    ///     TargetNode = targetNode,
    ///     UpVector = Vector3.UnitY,
    /// };
    /// player.AddSolver(lookAt);
    /// </code>
    /// </example>
    /// <remarks>
    /// Initializes a new <see cref="LookAtConstraint"/> on the specified source node.
    /// </remarks>
    /// <param name="name">Name of this solver.</param>
    /// <param name="source">The node to rotate toward the target.</param>
    public class LookAtConstraint(string name, SceneNode source) : AnimationSamplerSolver(name)
    {
        /// <summary>
        /// Gets or sets the node whose rotation will be modified to face the target.
        /// </summary>
        public SceneNode Source { get; set; } = source;

        /// <summary>
        /// Gets or sets the target node to look at. When set, the solver
        /// reads this node's active world position each frame. Takes priority
        /// over <see cref="TargetPosition"/> when not <see langword="null"/>.
        /// </summary>
        public SceneNode? TargetNode { get; set; }

        /// <summary>
        /// Gets or sets a static world-space target position.
        /// Used when <see cref="TargetNode"/> is <see langword="null"/>.
        /// </summary>
        public Vector3 TargetPosition { get; set; }

        /// <summary>
        /// Gets or sets the world-space up vector for the look-at computation.
        /// Defaults to <see cref="Vector3.UnitY"/> (Y-up).
        /// </summary>
        public Vector3 UpVector { get; set; } = Vector3.UnitY;

        /// <summary>
        /// Computes a look-at rotation from the source toward the target and blends
        /// it with the source's current world rotation using the solver weight.
        /// </summary>
        /// <param name="time">The current frame time (unused for static constraints).</param>
        protected override void OnSolve(float time)
        {
            var sourcePos = Source.GetActiveWorldPosition();
            var targetPos = TargetNode is not null
                ? TargetNode.GetActiveWorldPosition()
                : TargetPosition;

            var direction = Vector3.Normalize(targetPos - sourcePos);

            // Skip if source and target overlap
            if (float.IsNaN(direction.X)) return;

            // Build a rotation matrix from the forward direction and up vector
            var forward = direction;
            var right = Vector3.Normalize(Vector3.Cross(UpVector, forward));
            if (float.IsNaN(right.X))
            {
                // Forward is parallel to up — pick an arbitrary right vector
                right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, forward));
                if (float.IsNaN(right.X))
                    right = Vector3.UnitX;
            }
            var correctedUp = Vector3.Cross(forward, right);

            var rotMatrix = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                correctedUp.X, correctedUp.Y, correctedUp.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1);

            var targetRotation = Quaternion.CreateFromRotationMatrix(rotMatrix);
            var currentRotation = Source.GetActiveWorldRotation();

            // Blend between current and target rotation using the solver weight
            Source.LiveTransform.WorldRotation = Quaternion.Slerp(currentRotation, targetRotation, CurrentWeight);
            Source.LiveTransform.LocalRotation = null;
        }
    }
}
