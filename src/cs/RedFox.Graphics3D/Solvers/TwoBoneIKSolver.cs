// ------------------------------------------------------------------------
// TwoBoneIKSolver.cs — Analytic two-bone inverse kinematics solver
// ------------------------------------------------------------------------
// Classic two-bone IK for limbs (arm = shoulder→elbow→wrist, leg =
// hip→knee→ankle). Uses the law of cosines for an exact solution,
// with a pole vector to control the bend plane (elbow/knee direction).
// Extends AnimationSamplerSolver so it participates in the animation
// pipeline and respects weight / enable controls.
// ------------------------------------------------------------------------

using System.Numerics;

namespace RedFox.Graphics3D.Solvers
{
    /// <summary>
    /// An analytic two-bone inverse kinematics solver that positions a three-joint
    /// chain so that the tip reaches a target position.
    /// <para>
    /// The solver uses the law of cosines to compute exact joint angles for a
    /// two-segment chain (e.g. upper-arm → forearm → hand). A pole vector
    /// controls the bend plane to prevent flipping and give directional control
    /// (e.g. elbow points outward, knee points forward).
    /// </para>
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <see cref="Root"/>, <see cref="Mid"/>, and <see cref="Tip"/> must form a
    ///       direct parent chain in the scene graph (Root → Mid → Tip).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       The solver writes world-space rotations via <see cref="SceneNode.LiveTransform"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       When the target is unreachable the chain stretches fully toward it.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <remarks>
    /// Initializes a new <see cref="TwoBoneIKSolver"/>.
    /// </remarks>
    /// <param name="name">Name of this solver node.</param>
    /// <param name="root">The root joint (e.g. shoulder / hip).</param>
    /// <param name="mid">The middle joint (e.g. elbow / knee).</param>
    /// <param name="tip">The tip / end-effector (e.g. wrist / ankle).</param>
    public class TwoBoneIKSolver(string name, SceneNode root, SceneNode mid, SceneNode tip) : AnimationSamplerSolver(name)
    {
        /// <summary>
        /// Gets or sets the root (uppermost) joint of the two-bone chain.
        /// For a human arm this is the shoulder.
        /// </summary>
        public SceneNode Root { get; set; } = root;

        /// <summary>
        /// Gets or sets the middle joint of the two-bone chain.
        /// For a human arm this is the elbow.
        /// </summary>
        public SceneNode Mid { get; set; } = mid;

        /// <summary>
        /// Gets or sets the tip (end-effector) of the two-bone chain.
        /// For a human arm this is the wrist / hand.
        /// </summary>
        public SceneNode Tip { get; set; } = tip;

        /// <summary>
        /// Gets or sets a target node whose active world position defines
        /// where the tip should reach. Overrides <see cref="TargetPosition"/>
        /// when not <see langword="null"/>.
        /// </summary>
        public SceneNode? TargetNode { get; set; }

        /// <summary>
        /// Gets or sets a static world-space target position.
        /// Used when <see cref="TargetNode"/> is <see langword="null"/>.
        /// </summary>
        public Vector3 TargetPosition { get; set; }

        /// <summary>
        /// Gets or sets a pole-vector node whose position defines the bend
        /// plane. Overrides <see cref="PoleVector"/> when not <see langword="null"/>.
        /// </summary>
        public SceneNode? PoleNode { get; set; }

        /// <summary>
        /// Gets or sets a static world-space pole vector that controls the
        /// direction of the mid-joint bend (e.g. which way the elbow points).
        /// Defaults to <see cref="Vector3.UnitY"/>.
        /// </summary>
        public Vector3 PoleVector { get; set; } = Vector3.UnitY;

        /// <summary>
        /// Solves the two-bone IK chain using the law of cosines and applies
        /// the resulting rotations to the root and mid joints.
        /// </summary>
        protected override void OnSolve(float time)
        {
            // Gather current world positions
            var rootPos = Root.GetActiveWorldPosition();
            var midPos  = Mid.GetActiveWorldPosition();
            var tipPos  = Tip.GetActiveWorldPosition();

            var target = TargetNode is not null
                ? TargetNode.GetActiveWorldPosition()
                : TargetPosition;

            var pole = PoleNode is not null
                ? PoleNode.GetActiveWorldPosition()
                : PoleVector;

            // Bone lengths
            float upperLen = Vector3.Distance(rootPos, midPos);
            float lowerLen = Vector3.Distance(midPos, tipPos);
            float chainLen = upperLen + lowerLen;

            // Direction and distance to target
            var rootToTarget = target - rootPos;
            float targetDist = rootToTarget.Length();

            if (targetDist < 1e-6f) return;

            // Clamp targetDist so the chain can reach (fully extend if too far)
            targetDist = MathF.Min(targetDist, chainLen - 1e-4f);
            targetDist = MathF.Max(targetDist, MathF.Abs(upperLen - lowerLen) + 1e-4f);

            // --- Root joint angle via law of cosines ---
            // angle at root between rootToTarget and rootToMid
            float cosRootAngle = (upperLen * upperLen + targetDist * targetDist - lowerLen * lowerLen)
                                 / (2f * upperLen * targetDist);
            cosRootAngle = Math.Clamp(cosRootAngle, -1f, 1f);
            float rootAngle = MathF.Acos(cosRootAngle);

            // --- Build the bend plane from the pole vector ---
            var aimDir = Vector3.Normalize(rootToTarget);

            // Project pole onto the plane perpendicular to aimDir
            var rootToPole = pole - rootPos;
            var polePlane = rootToPole - Vector3.Dot(rootToPole, aimDir) * aimDir;
            if (polePlane.LengthSquared() < 1e-8f)
                polePlane = GetFallbackPerpendicular(aimDir);
            polePlane = Vector3.Normalize(polePlane);

            // Compute mid position on the bend plane
            var bendAxis = Vector3.Normalize(Vector3.Cross(aimDir, polePlane));

            // Rotate aimDir by rootAngle around bendAxis to get desired upper-bone dir
            var upperDir = Vector3.Transform(aimDir, Quaternion.CreateFromAxisAngle(bendAxis, rootAngle));
            var desiredMid = rootPos + upperDir * upperLen;

            // --- Compute rotations ---
            // Root rotation: swing current upper bone to desired direction
            var currentUpper = Vector3.Normalize(midPos - rootPos);
            var desiredUpper = Vector3.Normalize(desiredMid - rootPos);
            var rootRotDelta = RotationFromTo(currentUpper, desiredUpper);

            var currentRootRot = Root.GetActiveWorldRotation();
            var newRootRot = Quaternion.Normalize(rootRotDelta * currentRootRot);

            // Mid rotation: after root rotation, recalculate mid-to-tip
            var rotatedTip = Vector3.Transform(tipPos - midPos, rootRotDelta) + desiredMid;
            var currentLower = Vector3.Normalize(rotatedTip - desiredMid);
            var desiredLower = Vector3.Normalize(target - desiredMid);
            var midRotDelta = RotationFromTo(currentLower, desiredLower);

            var currentMidRot = Mid.GetActiveWorldRotation();
            var newMidRot = Quaternion.Normalize(midRotDelta * currentMidRot);

            // Blend with weight
            Root.LiveTransform.WorldRotation = Quaternion.Slerp(currentRootRot, newRootRot, CurrentWeight);
            Root.LiveTransform.LocalRotation = null;

            Mid.LiveTransform.WorldRotation = Quaternion.Slerp(currentMidRot, newMidRot, CurrentWeight);
            Mid.LiveTransform.LocalRotation = null;
        }

        /// <summary>
        /// Computes the shortest-arc quaternion from one direction to another.
        /// Both inputs are assumed to be unit-length.
        /// </summary>
        private static Quaternion RotationFromTo(Vector3 from, Vector3 to)
        {
            var dot = Vector3.Dot(from, to);
            if (dot >= 1f - 1e-6f) return Quaternion.Identity;

            if (dot <= -1f + 1e-6f)
            {
                var perp = GetFallbackPerpendicular(from);
                return new Quaternion(perp, 0f);
            }

            var cross = Vector3.Cross(from, to);
            return Quaternion.Normalize(new Quaternion(cross.X, cross.Y, cross.Z, 1f + dot));
        }

        /// <summary>
        /// Returns a unit vector perpendicular to <paramref name="v"/>.
        /// </summary>
        private static Vector3 GetFallbackPerpendicular(Vector3 v)
        {
            var candidate = Vector3.Cross(Vector3.UnitX, v);
            if (candidate.LengthSquared() < 1e-6f)
                candidate = Vector3.Cross(Vector3.UnitY, v);
            return Vector3.Normalize(candidate);
        }
    }
}
