using RedFox.Graphics3D.Interpolators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Skeletal
{
    /// <summary>
    /// A class to hold a track that contains <see cref="AnimationCurve{T}"/> data that applies to a <see cref="SkeletonBone"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="SkeletonAnimationTrack"/> that applies to the provided bone name.
    /// </remarks>
    /// <param name="name">Name of the bone this applies to.</param>
    [DebuggerDisplay("Name = {Name}")]
    public sealed class SkeletonAnimationTrack(string name)
    {
        /// <summary>
        /// Gets or Sets the name of the bone this track applies to.
        /// </summary>
        public string Name { get; set; } = name;

        /// <summary>
        /// Gets or Sets the translation X curve.
        /// </summary>
        public AnimationCurve<Vector3>? TranslationCurve { get; set; }

        /// <summary>
        /// Gets or Sets the scale X curve.
        /// </summary>
        public AnimationCurve<Vector3>? ScaleCurve { get; set; }

        /// <summary>
        /// Gets or Sets the rotation curve.
        /// </summary>
        public AnimationCurve<Quaternion>? RotationCurve { get; set; }

        /// <summary>
        /// Gets or Sets the transform space.
        /// </summary>
        public TransformSpace TransformSpace { get; set; }

        /// <summary>
        /// Gets or Sets the transform type.
        /// </summary>
        public TransformType TransformType { get; set; }

        public void AddTranslationFrame(float time, Vector3 value)
        {
            TranslationCurve ??= new(TransformSpace, TransformType);
            TranslationCurve.KeyFrames.Add(new(time, value));
        }

        public void AddScaleFrame(float time, Vector3 value)
        {
            ScaleCurve ??= new(TransformSpace, TransformType);
            ScaleCurve.KeyFrames.Add(new(time, value));
        }

        public void AddRotationFrame(float time, Quaternion value)
        {
            RotationCurve ??= new(TransformSpace, TransformType);
            RotationCurve.KeyFrames.Add(new(time, value));
        }
    }
}
