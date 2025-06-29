using System.Diagnostics;
using System.Numerics;

namespace RedFox.Graphics3D.Skeletal
{
    /// <summary>
    /// A class that holds a data stored within a <see cref="SkeletonAnimation"/> instance that transforms the target.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SkeletonAnimationTarget"/> class with the provided data.
    /// </remarks>
    /// <param name="boneName">Name of the bone that we are targeting.</param>
    [DebuggerDisplay("Name = {BoneName}")]
    public class SkeletonAnimationTarget(string boneName)
    {
        /// <summary>
        /// Gets or Sets the name of the bone this channel targets
        /// </summary>
        public string BoneName { get; set; } = boneName;

        /// <summary>
        /// Gets or Sets the translation frames.
        /// </summary>
        public List<AnimationKeyFrame<float, Vector3>>? TranslationFrames { get; set; }

        /// <summary>
        /// Gets or Sets the rotation frames.
        /// </summary>
        public List<AnimationKeyFrame<float, Quaternion>>? RotationFrames { get; set; }

        /// <summary>
        /// Gets or Sets the scale frames.
        /// </summary>
        public List<AnimationKeyFrame<float, Vector3>>? ScaleFrames { get; set; }

        /// <summary>
        /// Gets or Sets the transform type for this bone.
        /// </summary>
        public TransformType TransformType { get; set; } = TransformType.Parent;

        /// <summary>
        /// Gets or Sets the transform type applied to bones that are children of this bone that are set to parent.
        /// </summary>
        public TransformType ChildTransformType { get; set; }

        /// <summary>
        /// Gets the number of translations frames.
        /// </summary>
        public int TranslationFrameCount => TranslationFrames != null ? TranslationFrames.Count : 0;

        /// <summary>
        /// Gets the number of rotation frames.
        /// </summary>
        public int RotationFrameCount => RotationFrames != null ? RotationFrames.Count : 0;

        /// <summary>
        /// Gets the number of scale frames.
        /// </summary>
        public int ScaleFrameCount => ScaleFrames != null ? ScaleFrames.Count : 0;

        public Vector3 SampleTranslation(float time)
        {
            var sample = Vector3.Zero;

            var (i0, i1) = AnimationKeyFrameHelper.GetFramePairIndex(TranslationFrames, time, 0.0f);

            if (i0 != -1 && i1 != -1)
            {
                var firstFrame = TranslationFrames![i0];
                var secondFrame = TranslationFrames![i1];

                if (i0 == i1)
                    sample = firstFrame.Value;
                else
                    sample = Vector3.Lerp(firstFrame.Value, secondFrame.Value, (time - firstFrame.Frame) / (secondFrame.Frame - firstFrame.Frame));
            }

            return sample;
        }

        public Quaternion SampleRotation(float time)
        {
            var sample = Quaternion.Identity;

            var (i0, i1) = AnimationKeyFrameHelper.GetFramePairIndex(RotationFrames, time, 0.0f);

            if (i0 != -1 && i1 != -1)
            {
                var firstFrame = RotationFrames![i0];
                var secondFrame = RotationFrames![i1];

                if (i0 == i1)
                    sample = firstFrame.Value;
                else
                    sample = Quaternion.Slerp(firstFrame.Value, secondFrame.Value, (time - firstFrame.Frame) / (secondFrame.Frame - firstFrame.Frame));
            }

            return sample;
        }

        public void AddTranslationFrame(float time, Vector3 value)
        {
            TranslationFrames ??= [];
            TranslationFrames.Add(new(time, value));
        }

        public void AddRotationFrame(float time, Quaternion value)
        {
            RotationFrames ??= [];
            RotationFrames.Add(new(time, value));
        }

        public void AddScaleFrame(float time, Vector3 value)
        {
            ScaleFrames ??= [];
            ScaleFrames.Add(new(time, value));
        }
    }
}
