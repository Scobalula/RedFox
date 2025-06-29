using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Skeletal
{
    /// <summary>
    /// A class to handle sampling an <see cref="SkeletonAnimationTarget"/> at arbitrary frames or in a linear fashion.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SkeletonAnimationTargetSampler"/> class.
    /// </remarks>
    /// <param name="owner"></param>
    /// <param name="bone"></param>
    /// <param name="target"></param>
    [DebuggerDisplay("Name = {Bone.Name}")]
    public class SkeletonAnimationTargetSampler(SkeletonAnimationSampler owner, SkeletonBone bone, SkeletonAnimationTarget? target, TransformType transformType, TransformSpace transformSpace)
    {
        /// <summary>
        /// Gets the sampler that owns this.
        /// </summary>
        public SkeletonAnimationSampler Owner { get; private set; } = owner;

        /// <summary>
        /// Gets or Sets the bone this is targeting.
        /// </summary>
        public SkeletonBone Bone { get; private set; } = bone;

        /// <summary>
        /// Gets the target.
        /// </summary>
        public SkeletonAnimationTarget? Target { get; private set; } = target;

        /// <summary>
        /// Gets the transform type.
        /// </summary>
        public TransformType TransformType { get; internal set; } = transformType;

        /// <summary>
        /// Gets the transform space.
        /// </summary>
        public TransformSpace TransformSpace { get; internal set; } = transformSpace;

        /// <summary>
        /// Gets or Sets the Translations Cursor
        /// </summary>
        private int CurrentTranslationsCursor { get; set; }

        /// <summary>
        /// Gets or Sets the Rotations Cursor
        /// </summary>
        private int CurrentRotationsCursor { get; set; }

        /// <summary>
        /// Updates the current animation bone sampler
        /// </summary>
        public void Update()
        {
            var isLocal = TransformSpace == TransformSpace.Local;

            if (Target != null)
            {
                var bone = Target;
                var time = Owner.CurrentTime;

                var (firstRIndex, secondRIndex) = AnimationKeyFrameHelper.GetFramePairIndex(
                    bone.RotationFrames,
                    time,
                    Owner.StartFrame,
                    cursor: CurrentRotationsCursor);
                var (firstTIndex, secondTIndex) = AnimationKeyFrameHelper.GetFramePairIndex(
                    bone.TranslationFrames,
                    time,
                    Owner.StartFrame,
                    cursor: CurrentTranslationsCursor);

                // We have a rotation
                if (firstRIndex != -1)
                {
                    var firstFrame = bone.RotationFrames![firstRIndex];
                    var secondFrame = bone.RotationFrames![secondRIndex];

                    Quaternion rot;

                    // Identical Frames, no interpolating
                    if (firstRIndex == secondRIndex)
                        rot = bone.RotationFrames[firstRIndex].Value;
                    else
                        rot = Quaternion.Slerp(firstFrame.Value, secondFrame.Value, (time - (Owner.StartFrame + firstFrame.Frame)) / ((Owner.StartFrame + secondFrame.Frame) - (Owner.StartFrame + firstFrame.Frame)));

                    Quaternion result = TransformType switch
                    {
                        TransformType.Additive => Bone.LocalRotation * rot,
                        _                      => rot,
                    };

                    // Blend between
                    if(isLocal)
                        Bone.LocalRotation = Quaternion.Slerp(Bone.LocalRotation, result, Owner.CurrentWeight);
                    else
                        Bone.WorldRotation = Quaternion.Slerp(Bone.WorldRotation, result, Owner.CurrentWeight);
                    //// Update cursor (to speed up linear sampling if we're going forward)
                    //CurrentRotationsCursor = firstRIndex;
                }

                if (firstTIndex != -1)
                {
                    var firstFrame = bone.TranslationFrames![firstTIndex];
                    var secondFrame = bone.TranslationFrames![secondTIndex];

                    Vector3 translation;

                    // Identical Frames, no interpolating
                    if (firstTIndex == secondTIndex)
                        translation = bone.TranslationFrames[firstTIndex].Value;
                    else
                        translation = Vector3.Lerp(firstFrame.Value, secondFrame.Value, (time - (Owner.StartFrame + firstFrame.Frame)) / ((Owner.StartFrame + secondFrame.Frame) - (Owner.StartFrame + firstFrame.Frame)));
                    
                    var result = TransformType switch
                    {
                        TransformType.Additive => Bone.LocalTranslation + translation,
                        TransformType.Relative => Bone.BaseLocalTranslation + translation,
                        _                      => translation,
                    };

                    // Blend between
                    if(isLocal)
                        Bone.LocalTranslation = Vector3.Lerp(Bone.LocalTranslation, result, Owner.CurrentWeight);
                    else
                        Bone.WorldTranslation = Vector3.Lerp(Bone.WorldTranslation, result, Owner.CurrentWeight);
                    //// Update cursor (to speed up linear sampling if we're going forward)
                    //CurrentTranslationsCursor = firstTIndex;
                }
            }

            if (isLocal)
                Bone.GenerateCurrentWorldTransform();
            else
                Bone.GenerateCurrentLocalTransform();
        }
    }
}
