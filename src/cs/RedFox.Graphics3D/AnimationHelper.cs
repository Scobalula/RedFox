using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public class AnimationHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetWeight(List<AnimationKeyFrame<float, float>> weights, float time, float startTime, float defaultWeight, ref int cursor)
        {
            var (firstIndex, secondIndex) = Animation.GetFramePairIndex(weights, time, startTime, cursor:cursor);
            var result = defaultWeight;

            if (firstIndex != -1)
            {
                if (firstIndex == secondIndex)
                {
                    result = weights[firstIndex].Value;
                }
                else
                {
                    var firstFrame = weights[firstIndex];
                    var secondFrame = weights[secondIndex];

                    var lerpAmount = (time - (startTime + firstFrame.Frame)) / ((startTime + secondFrame.Frame) - (startTime + firstFrame.Frame));

                    result = (firstFrame.Value * (1 - lerpAmount)) + (secondFrame.Value * lerpAmount);
                }

                cursor = firstIndex;
            }

            return result;
        }
    }
}
