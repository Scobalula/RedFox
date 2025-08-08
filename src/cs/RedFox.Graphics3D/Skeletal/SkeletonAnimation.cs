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
        /// Gets or Sets the bone tracks.
        /// </summary>
        public List<SkeletonAnimationTrack> Tracks { get; set; }

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
            Tracks = [];
            TransformType = TransformType.Unknown;
        }

        public SkeletonAnimation(string name, Skeleton? skeleton) : base(name)
        {
            Tracks = [];
            Skeleton = skeleton;
            //Skeleton?.CreateAnimationTargets(this);
        }

        public SkeletonAnimation(string name, Skeleton? skeleton, int targetCount, TransformType type) : base(name)
        {
            Skeleton = skeleton;
            Tracks = new(targetCount);
            TransformType = type;
        }

        ///// <summary>
        ///// Checks whether or not any of the skeletal targets has translation frames.
        ///// </summary>
        ///// <returns>True if any of the targets has frames, otherwise false.</returns>
        //public bool HasTranslationFrames() => Targets.Any(x => x.TranslationFrameCount > 0);

        ///// <summary>
        ///// Checks whether or not any of the skeletal targets has rotation frames.
        ///// </summary>
        ///// <returns>True if any of the targets has frames, otherwise false.</returns>
        //public bool HasRotationFrames() => Targets.Any(x => x.RotationFrameCount > 0);

        ///// <summary>
        ///// Checks whether or not any of the skeletal targets has scales frames.
        ///// </summary>
        ///// <returns>True if any of the targets has frames, otherwise false.</returns>
        //public bool HasScalesFrames() => Targets.Any(x => x.ScaleFrameCount > 0);

        /// <inheritdoc/>
        public override (float, float) GetAnimationFrameRange()
        {
            var minFrame = float.MaxValue;
            var maxFrame = float.MinValue;

            foreach (var target in Tracks)
            {
                if (target.TranslationCurve is not null)
                {
                    foreach (var f in target.TranslationCurve.KeyFrames)
                    {
                        minFrame = MathF.Min(minFrame, f.Frame);
                        maxFrame = MathF.Max(maxFrame, f.Frame);
                    }
                }

                if (target.RotationCurve is not null)
                {
                    foreach (var f in target.RotationCurve.KeyFrames)
                    {
                        minFrame = MathF.Min(minFrame, f.Frame);
                        maxFrame = MathF.Max(maxFrame, f.Frame);
                    }
                }

                if (target.ScaleCurve is not null)
                {
                    foreach (var f in target.ScaleCurve.KeyFrames)
                    {
                        minFrame = MathF.Min(minFrame, f.Frame);
                        maxFrame = MathF.Max(maxFrame, f.Frame);
                    }
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

            return (minFrame, maxFrame);
        }

        public void SetTransformType(TransformType transformType)
        {
            foreach (var track in Tracks)
            {
                if (track.TranslationCurve is not null)
                    track.TranslationCurve.TransformType = transformType;
                if (track.RotationCurve is not null)
                    track.RotationCurve.TransformType = transformType;
                if (track.ScaleCurve is not null)
                    track.ScaleCurve.TransformType = transformType;
            }
        }
    }
}
