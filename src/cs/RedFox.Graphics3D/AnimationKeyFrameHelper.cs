using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public class AnimationKeyFrameHelper
    {
        /// <summary>
        /// Gets the previous and next frame pair indices at the given time. If no frame pair could be resolved, -1 is returned.
        /// </summary>
        public static (int, int) GetFramePairIndex<TFrame, TValue>(List<AnimationKeyFrame<TFrame, TValue>>? list, TFrame time, TFrame startTime, int cursor = 0) where TFrame : INumber<TFrame>
        {
            // Early quit for lists that we can't "pair"
            if (list == null)
                return (-1, -1);
            if (list.Count == 0)
                return (-1, -1);
            if (list.Count == 1)
                return (0, 0);
            if (time > (startTime + list.Last().Frame))
                return (list.Count - 1, list.Count - 1);
            if (time < (startTime + list.First().Frame))
                return (0, 0);

            int i;

            // First pass from cursor
            for (i = cursor; i < list.Count - 1; i++)
                if (time < (startTime + list[i + 1].Frame))
                    return (i, i + 1);
            // Second pass up to cursor
            for (i = 0; i < list.Count - 1 && i < cursor; i++)
                if (time < (startTime + list[i + 1].Frame))
                    return (i, i + 1);

            return (list.Count - 1, list.Count - 1);
        }

        public static IEnumerable<AnimationKeyFrame<TFrame, TValue>> EnumerateKeyFrames<TFrame, TValue>(IEnumerable<AnimationKeyFrame<TFrame, TValue>>? keyFrames) where TFrame : INumber<TFrame>
        {
            if(keyFrames != null)
            {
                foreach (var keyFrame in keyFrames)
                {
                    yield return keyFrame;
                }
            }
        }
    }
}
