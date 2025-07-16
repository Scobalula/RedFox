using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public class AnimationCurve<T, I> where T : unmanaged where I : IValueManipulator<T>, new()
    {
        /// <summary>
        /// Gets or Sets the transform space.
        /// </summary>
        public TransformSpace Space { get; set; }

        /// <summary>
        /// Gets or Sets the transform type.
        /// </summary>
        public TransformType Type { get; set; }

        /// <summary>
        /// Gets or Sets the key frames stored in this curve.
        /// </summary>
        public List<AnimationKeyFrame<float, T>> KeyFrames { get; set; } = [];

        /// <summary>
        /// Gets the interpolator.
        /// </summary>
        public static IValueManipulator<T> Interpolator { get; } = new I();

        /// <summary>
        /// Initializes a new <see cref="AnimationCurve{T}"/>.
        /// </summary>
        public AnimationCurve()
        {
        }

        /// <summary>
        /// Initializes a new <see cref="AnimationCurve{T}"/> with the given space and type.
        /// </summary>
        /// <param name="space">The transform space which the curve applies</param>
        /// <param name="type">The transform type which the curve applies.</param>
        public AnimationCurve(TransformSpace space, TransformType type)
        {
            Space = space;
            Type = type;
        }

        public T Sample(float time)
        {
            var t = default(T);
            var (i0, i1) = AnimationKeyFrameHelper.GetFramePairIndex(KeyFrames, time, 0.0f);

            if (i0 != -1 && i1 != -1)
            {
                var firstFrame = KeyFrames[i0];
                var secondFrame = KeyFrames[i1];

                if (i0 == i1)
                    t = firstFrame.Value;
                else
                    t = Interpolator.Interpolate(firstFrame.Value, secondFrame.Value, (time - firstFrame.Frame) / (secondFrame.Frame - firstFrame.Frame));
            }

            return t;
        }

        public T Apply(T input) => Interpolator.Modify(input, Type);
    }
}
