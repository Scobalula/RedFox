using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace RedFox.Graphics3D.Skeletal
{
    public class SkeletonAnimationSampler : AnimationSampler
    {
        public List<KeyValuePair<SkeletonBone, SkeletonAnimationTrack>> Tracks { get; set; }

        public SkeletonAnimationSampler(string name, SkeletonAnimation animation, Skeleton skeleton) : base(name, animation)
        {
            Tracks = [];

            foreach (var bone in skeleton.EnumerateHierarchy())
            {
                // Find the corrosponding track for this bone
                var track = animation.Tracks.Find(x => x.Name.Equals(bone.Name));

                //if (track is not null)
                if (track is not null)
                    Tracks.Add(new(bone, track));
            }
        }

        public override void UpdateObjects()
        {
            var currentTimeRelative = CurrentTime - StartFrame;

            foreach (var (bone, track) in Tracks)
            {
                if (track.TranslationCurve is not null)
                {
                    var value = track.TranslationCurve.Sample(currentTimeRelative, Vector3.Lerp);

                    if (track.TranslationCurve.TransformSpace == TransformSpace.World)
                    {
                        var result = track.TranslationCurve.TransformType switch
                        {
                            TransformType.Additive => bone.GetActiveWorldPosition() + value,
                            TransformType.Relative => bone.GetBaseWorldPosition() + value,
                            TransformType.Absolute => value,
                            _ => throw new NotSupportedException()
                        };

                        bone.LiveTransform.WorldPosition = Vector3.Lerp(bone.GetActiveWorldPosition(), result, CurrentWeight);
                        bone.LiveTransform.LocalPosition = null;
                    }
                    else
                    {
                        var result = track.TranslationCurve.TransformType switch
                        {
                            TransformType.Additive => bone.GetLiveLocalPosition() + value,
                            TransformType.Relative => bone.GetBaseLocalPosition() + value,
                            TransformType.Absolute => value,
                            _ => throw new NotSupportedException()
                        };

                        bone.LiveTransform.LocalPosition = Vector3.Lerp(bone.GetLiveLocalPosition(), result, CurrentWeight);
                        bone.LiveTransform.WorldPosition = null;
                    }
                }

                if (track.RotationCurve is not null)
                {
                    var value = track.RotationCurve.Sample(currentTimeRelative, Quaternion.Slerp);

                    if (track.RotationCurve.TransformSpace == TransformSpace.World)
                    {
                        var result = track.RotationCurve.TransformType switch
                        {
                            TransformType.Additive => bone.GetActiveWorldRotation() * value,
                            TransformType.Relative => bone.GetBaseWorldRotation() * value,
                            TransformType.Absolute => value,
                            _ => throw new NotSupportedException()
                        };

                        bone.LiveTransform.WorldRotation = Quaternion.Slerp(bone.GetActiveWorldRotation(), Quaternion.Normalize(result), CurrentWeight);
                        bone.LiveTransform.LocalRotation = null;
                    }
                    else
                    {
                        var result = track.RotationCurve.TransformType switch
                        {
                            TransformType.Additive => bone.GetLiveLocalRotation() * value,
                            TransformType.Relative => bone.GetBaseLocalRotation() * value,
                            TransformType.Absolute => value,
                            _ => throw new NotSupportedException()
                        };

                        bone.LiveTransform.LocalRotation = Quaternion.Slerp(bone.GetLiveLocalRotation(), Quaternion.Normalize(result), CurrentWeight);
                        bone.LiveTransform.WorldRotation = null;
                    }
                }
            }
        }

        public void SetTransformType(TransformType transformType)
        {
            foreach (var (_, track) in Tracks)
            {
                if (track is null)
                    continue;

                if (track.TranslationCurve is not null)
                    track.TranslationCurve.TransformType = transformType;
                if (track.RotationCurve is not null)
                    track.RotationCurve.TransformType = transformType;
                if (track.ScaleCurve is not null)
                    track.ScaleCurve.TransformType = transformType;
            }
        }

        public override bool IsObjectAnimated(string objectName) => Tracks.FindIndex(x => x.Key.Name == objectName) != -1;
    }
}
