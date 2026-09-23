using System.Runtime.CompilerServices;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides static helper methods for common animation operations such as
/// weight interpolation from keyframe lists and DataBuffer-backed curves.
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// Interpolates the weight at the given time from a legacy keyframe list,
    /// using a cursor hint for efficient sequential access.
    /// </summary>
    /// <param name="weights">Sorted weight keyframes.</param>
    /// <param name="time">Current playback time.</param>
    /// <param name="startTime">Offset added to each keyframe's time.</param>
    /// <param name="defaultWeight">Value returned when no keyframes exist.</param>
    /// <param name="cursor">Scan-start hint; updated on output.</param>
    /// <returns>The interpolated weight at the given time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetWeight(
        List<AnimationKeyFrame<float, float>> weights,
        float time,
        float startTime,
        float defaultWeight,
        ref int cursor)
    {
        var (firstIndex, secondIndex) = Animation.GetFramePairIndex(weights, time, startTime, cursor: cursor);
        var result = defaultWeight;

        if (firstIndex != -1)
        {
            if (firstIndex == secondIndex)
            {
                result = weights[firstIndex].Value;
            }
            else
            {
                var first = weights[firstIndex];
                var second = weights[secondIndex];
                var t = (time - (startTime + first.Frame)) / ((startTime + second.Frame) - (startTime + first.Frame));
                result = first.Value * (1f - t) + second.Value * t;
            }

            cursor = firstIndex;
        }

        return result;
    }

    /// <summary>
    /// Interpolates the weight at the given time from a DataBuffer-backed
    /// <see cref="AnimationCurve"/>.
    /// </summary>
    /// <param name="curve">The weight curve (1-component scalar).</param>
    /// <param name="time">Current playback time.</param>
    /// <returns>The interpolated weight at the given time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetWeight(AnimationCurve? curve, float time)
        => GetWeight(curve, time, defaultWeight: 1.0f);

    /// <summary>
    /// Interpolates the weight at the given time from a scalar animation curve.
    /// </summary>
    /// <param name="curve">The scalar curve to sample. A null or empty curve returns <paramref name="defaultWeight"/>.</param>
    /// <param name="time">The playback time to sample.</param>
    /// <param name="defaultWeight">The value returned when <paramref name="curve"/> has no keyframes.</param>
    /// <returns>The interpolated curve value or <paramref name="defaultWeight"/>.</returns>
    public static float GetWeight(AnimationCurve? curve, float time, float defaultWeight)
    {
        if (curve is null || curve.KeyFrameCount == 0)
            return defaultWeight;

        return curve.SampleScalar(time);
    }
}
