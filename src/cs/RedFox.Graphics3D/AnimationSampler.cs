using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// A class to handle sampling an <see cref="Animation"/> at arbitrary frames or in a linear fashion.
    /// </summary>
    public abstract class AnimationSampler(string name, Animation animation) : Graphics3DObject(name)
    {
        /// <summary>
        /// Gets or Sets the animation that is being sampled.
        /// </summary>
        public Animation Animation { get; set; } = animation;

        /// <summary>
        /// Gets the current time.
        /// </summary>
        public float CurrentTime { get; private set; }

        public int Cursor { get; set; }

        /// <summary>
        /// Gets or Sets the weights for this layer.
        /// </summary>
        public List<AnimationKeyFrame<float, float>> Weights { get; set; } = [];

        /// <summary>
        /// Gets or Sets the current weight
        /// </summary>
        public float CurrentWeight { get; set; } = 1.0f;

        /// <summary>
        /// Gets the number of frames in the animation.
        /// </summary>
        public float FrameCount { get; set; } = animation.GetAnimationFrameRange().Item1;

        /// <summary>
        /// Gets or Sets the playback framerate.
        /// </summary>
        public float FrameRate { get; set; } = animation.Framerate;

        /// <summary>
        /// Gets the length of the animation.
        /// </summary>
        public float Length => FrameCount / FrameRate;

        /// <summary>
        /// Gets the length of each frame.
        /// </summary>
        public float Frametime => 1.0f / Animation.Framerate;

        /// <summary>
        /// Gets or Sets the frame this sampler starts at.
        /// </summary>
        public float StartFrame { get; set; }

        public void Update() => Update(CurrentTime + Frametime);
        public void Update(AnimationSampleType type) => Update(CurrentTime + Frametime, type);

        public void Update(float time) => Update(time, AnimationSampleType.AbsoluteFrameTime);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateWeight()
        {
            var (firstIndex, secondIndex) = Animation.GetFramePairIndex(Weights, CurrentTime, 0, Cursor);
            var result = 1.0f;

            if (firstIndex != -1)
            {
                if (firstIndex == secondIndex)
                {
                    result = Weights[firstIndex].Value;
                }
                else
                {
                    var firstFrame = Weights[firstIndex];
                    var secondFrame = Weights[secondIndex];

                    var lerpAmount = (CurrentTime - (0 + firstFrame.Frame)) / ((0 + secondFrame.Frame) - (0 + firstFrame.Frame));

                    result = (firstFrame.Value * (1 - lerpAmount)) + (secondFrame.Value * lerpAmount);
                }

                Cursor = firstIndex;
            }

            CurrentWeight = result;
        }

        public AnimationSampler Update(float time, AnimationSampleType type)
        {
            switch(type)
            {
                case AnimationSampleType.Percentage:
                    CurrentTime = FrameCount * time;
                    break;
                case AnimationSampleType.AbsoluteFrameTime:
                    CurrentTime = time;
                    break;
                case AnimationSampleType.AbsoluteTime:
                    CurrentTime = time * FrameRate;
                    break;
                case AnimationSampleType.DeltaTime:
                    CurrentTime += time * FrameRate;
                    break;
            }

            UpdateWeight();

            //// Update sub-samplers
            //SkeletonAnimationSampler?.Update();

            if (CurrentTime >= StartFrame)
                UpdateObjects();

            return this;
        }

        public abstract bool IsObjectAnimated(string objectName);
        public abstract void UpdateObjects();
    }
}
