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
        public AnimationCurve<float, FloatInterpolator>? TranslationXCurve { get; set; }

        /// <summary>
        /// Gets or Sets the translation Y curve.
        /// </summary>
        public AnimationCurve<float, FloatInterpolator>? TranslationYCurve { get; set; }

        /// <summary>
        /// Gets or Sets the translation Z curve.
        /// </summary>
        public AnimationCurve<float, FloatInterpolator>? TranslationZCurve { get; set; }

        /// <summary>
        /// Gets or Sets the scale X curve.
        /// </summary>
        public AnimationCurve<float, FloatInterpolator>? ScaleXCurve { get; set; }

        /// <summary>
        /// Gets or Sets the scale Y curve.
        /// </summary>
        public AnimationCurve<float, FloatInterpolator>? ScaleYCurve { get; set; }

        /// <summary>
        /// Gets or Sets the scale Z curve.
        /// </summary>
        public AnimationCurve<float, FloatInterpolator>? ScaleZCurve { get; set; }

        /// <summary>
        /// Gets or Sets the rotation curve.
        /// </summary>
        public AnimationCurve<Quaternion, QuaternionInterpolator>? RotationCurve { get; set; }

        public void AddTranslationFrame(float time, Vector3 value)
        {
            TranslationXCurve ??= new();
            TranslationYCurve ??= new();
            TranslationZCurve ??= new();

            TranslationXCurve.KeyFrames.Add(new(time, value.X));
            TranslationYCurve.KeyFrames.Add(new(time, value.Y));
            TranslationZCurve.KeyFrames.Add(new(time, value.Z));
        }

        public void AddTranslationXFrame(float time, float value)
        {
            TranslationYCurve ??= new();
            TranslationYCurve.KeyFrames.Add(new(time, value));
        }

        public void AddTranslationZFrame(float time, float value)
        {
            TranslationYCurve ??= new();
            TranslationYCurve.KeyFrames.Add(new(time, value));
        }

        public void AddTranslationYFrame(float time, float value)
        {
            TranslationZCurve ??= new();
            TranslationZCurve.KeyFrames.Add(new(time, value));
        }

        public void AddRotationFrame(float time, Quaternion value)
        {
            RotationCurve ??= new();
            RotationCurve.KeyFrames.Add(new(time, value));
        }
    }
}
