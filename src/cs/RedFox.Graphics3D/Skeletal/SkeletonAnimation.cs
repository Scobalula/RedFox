using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RedFox.Graphics3D.Skeletal
{
    
    public class SkeletonAnimation : Animation
    {
        /// <summary>
        /// Gets or Sets the skeleton tied to this animation, if any.
        /// </summary>
        public Skeleton? Skeleton { get; set; }

        /// <summary>
        /// Gets or Sets the targets that contain animation frames.
        /// </summary>
        public List<SkeletonAnimationTarget> Targets { get; set; }

        /// <summary>
        /// Gets or Sets the transform type.
        /// </summary>
        public TransformType TransformType { get; set; }

        /// <summary>
        /// Gets or Sets the transform space.
        /// </summary>
        public TransformSpace TransformSpace { get; set; }

        public SkeletonAnimation(string name) : base(name)
        {
            Targets = [];
            TransformType = TransformType.Unknown;
        }

        public SkeletonAnimation(string name, Skeleton? skeleton) : base(name)
        {
            Targets = [];
            Skeleton = skeleton;
            Skeleton?.CreateAnimationTargets(this);
        }

        public SkeletonAnimation(string name, Skeleton? skeleton, int targetCount, TransformType type) : base(name)
        {
            Skeleton = skeleton;
            Targets = new(targetCount);
            TransformType = type;
        }

        /// <summary>
        /// Checks whether or not any of the skeletal targets has translation frames.
        /// </summary>
        /// <returns>True if any of the targets has frames, otherwise false.</returns>
        public bool HasTranslationFrames() => Targets.Any(x => x.TranslationFrameCount > 0);

        /// <summary>
        /// Checks whether or not any of the skeletal targets has rotation frames.
        /// </summary>
        /// <returns>True if any of the targets has frames, otherwise false.</returns>
        public bool HasRotationFrames() => Targets.Any(x => x.RotationFrameCount > 0);

        /// <summary>
        /// Checks whether or not any of the skeletal targets has scales frames.
        /// </summary>
        /// <returns>True if any of the targets has frames, otherwise false.</returns>
        public bool HasScalesFrames() => Targets.Any(x => x.ScaleFrameCount > 0);

        /// <inheritdoc/>
        public override float GetAnimationFrameCount()
        {
            var minFrame = float.MaxValue;
            var maxFrame = float.MinValue;

            foreach (var target in Targets)
            {
                foreach (var f in AnimationKeyFrameHelper.EnumerateKeyFrames(target.TranslationFrames))
                {
                    minFrame = MathF.Min(minFrame, f.Frame);
                    maxFrame = MathF.Max(maxFrame, f.Frame);
                }

                foreach (var f in AnimationKeyFrameHelper.EnumerateKeyFrames(target.RotationFrames))
                {
                    minFrame = MathF.Min(minFrame, f.Frame);
                    maxFrame = MathF.Max(maxFrame, f.Frame);
                }

                foreach (var f in AnimationKeyFrameHelper.EnumerateKeyFrames(target.ScaleFrames))
                {
                    minFrame = MathF.Min(minFrame, f.Frame);
                    maxFrame = MathF.Max(maxFrame, f.Frame);
                }
            }

            //foreach (var action in Actions)
            //{
            //    foreach (var f in action.KeyFrames)
            //    {
            //        minFrame = MathF.Min(minFrame, f.Frame);
            //        maxFrame = MathF.Max(maxFrame, f.Frame);
            //    }
            //}

            return (maxFrame - minFrame) + 1;
        }
    }
}
