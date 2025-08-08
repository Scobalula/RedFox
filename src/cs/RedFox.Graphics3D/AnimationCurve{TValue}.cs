using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public class AnimationCurve<TValue> where TValue : unmanaged
    {
        /// <summary>
        /// Gets or Sets the transform space.
        /// </summary>
        public TransformSpace TransformSpace { get; set; }

        /// <summary>
        /// Gets or Sets the transform type.
        /// </summary>
        public TransformType TransformType { get; set; }

        /// <summary>
        /// Gets or Sets the key frames stored in this curve.
        /// </summary>
        public List<AnimationKeyFrame<float, TValue>> KeyFrames { get; set; } = [];

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
            TransformSpace = space;
            TransformType = type;
        }

        public void Add(float time, TValue value) => KeyFrames.Add(new(time, value));

        public TValue Sample(float time, Func<TValue, TValue, float, TValue> interpolationMethod)
        {
            var t = default(TValue);
            var (i0, i1) = Animation.GetFramePairIndex(KeyFrames, time, 0.0f);

            if (i0 != -1 && i1 != -1)
            {
                var firstFrame = KeyFrames[i0];
                var secondFrame = KeyFrames[i1];

                if (i0 == i1)
                    t = firstFrame.Value;
                else
                    t = interpolationMethod(firstFrame.Value, secondFrame.Value, (time - firstFrame.Frame) / (secondFrame.Frame - firstFrame.Frame));
            }

            return t;
        }
    }
}
