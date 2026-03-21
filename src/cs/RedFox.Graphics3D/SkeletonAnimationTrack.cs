// ------------------------------------------------------------------------
// SkeletonAnimationTrack.cs — Bone-targeted animation track
// ------------------------------------------------------------------------
// Extends AnimationTrack with bone-specific semantics. Each track targets
// a single SkeletonBone and stores translation, rotation, and scale curves
// backed by DataBuffers (inherited from AnimationTrack).
// Now a SceneNode — tracks can be children of a SkeletonAnimation node.
// ------------------------------------------------------------------------

using System.Diagnostics;
using System.Numerics;

namespace RedFox.Graphics3D.Skeletal
{
    /// <summary>
    /// An animation track that targets a single <see cref="SkeletonBone"/>.
    /// Inherits translation, rotation, and scale curves from <see cref="AnimationTrack"/>,
    /// all backed by <see cref="AnimationCurve"/> (DataBuffer storage).
    /// <para>
    /// As a <see cref="SceneNode"/>, tracks participate in the scene graph and can be
    /// discovered via standard traversal of their parent <see cref="SkeletonAnimation"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="SkeletonAnimationTrack"/> targeting the bone
    /// with the specified name.
    /// </remarks>
    /// <param name="name">Name of the bone this track targets.</param>
    [DebuggerDisplay("SkeletonAnimationTrack: {Name}")]
    public sealed class SkeletonAnimationTrack(string name) : AnimationTrack(name)
    {
        /// <summary>
        /// Gets or sets the default transform space applied when lazily creating curves.
        /// Individual curves may override this via their own
        /// <see cref="AnimationCurve.TransformSpace"/> property.
        /// </summary>
        public TransformSpace TransformSpace { get; set; }

        /// <summary>
        /// Gets or sets the default transform type applied when lazily creating curves.
        /// Individual curves may override this via their own
        /// <see cref="AnimationCurve.TransformType"/> property.
        /// </summary>
        public TransformType TransformType { get; set; }

        /// <summary>
        /// Appends a translation keyframe at the specified time. Lazily creates the
        /// translation curve using this track's <see cref="TransformSpace"/> and
        /// <see cref="TransformType"/> defaults.
        /// </summary>
        /// <param name="time">The keyframe time (in frames or seconds).</param>
        /// <param name="value">The translation vector.</param>
        public void AddTranslationFrame(float time, Vector3 value)
        {
            TranslationCurve ??= AnimationCurve.CreateVector3(TransformSpace, TransformType);
            TranslationCurve.Add(time, value);
        }

        /// <summary>
        /// Appends a scale keyframe at the specified time. Lazily creates the
        /// scale curve using this track's default space and type.
        /// </summary>
        /// <param name="time">The keyframe time.</param>
        /// <param name="value">The scale vector.</param>
        public void AddScaleFrame(float time, Vector3 value)
        {
            ScaleCurve ??= AnimationCurve.CreateVector3(TransformSpace, TransformType);
            ScaleCurve.Add(time, value);
        }

        /// <summary>
        /// Appends a rotation keyframe at the specified time. Lazily creates the
        /// rotation curve using this track's default space and type.
        /// </summary>
        /// <param name="time">The keyframe time.</param>
        /// <param name="value">The rotation quaternion.</param>
        public void AddRotationFrame(float time, Quaternion value)
        {
            RotationCurve ??= AnimationCurve.CreateQuaternion(TransformSpace, TransformType);
            RotationCurve.Add(time, value);
        }
    }
}
