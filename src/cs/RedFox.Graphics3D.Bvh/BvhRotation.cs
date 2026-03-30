using System.Numerics;

namespace RedFox.Graphics3D.Bvh;

/// <summary>
/// Provides BVH Euler-order rotation conversion helpers.
/// </summary>
public static class BvhRotation
{
    /// <summary>
    /// Composes a quaternion from Euler degrees using the supplied BVH channel order.
    /// BVH rotation channels are concatenated using the format's reversed matrix application order rather than naive Euler multiplication order.
    /// </summary>
    /// <param name="eulerDegrees">The Euler angles in degrees, stored as X, Y, and Z components.</param>
    /// <param name="channels">The channel sequence that defines the rotation order.</param>
    /// <returns>The normalized quaternion represented by <paramref name="eulerDegrees"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="channels"/> is <see langword="null"/>.</exception>
    public static Quaternion ComposeDegrees(Vector3 eulerDegrees, IReadOnlyList<BvhChannelType> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        Quaternion composed = Quaternion.Identity;
        bool hasRotationChannel = false;

        for (int i = 0; i < channels.Count; i++)
        {
            BvhChannelType channel = channels[i];
            if (!BvhFormat.IsRotationChannel(channel))
            {
                continue;
            }

            Quaternion channelRotation = CreateAxisRotation(BvhFormat.GetAxisIndex(channel), GetAxisComponent(eulerDegrees, channel));
            composed = composed * channelRotation;
            hasRotationChannel = true;
        }

        return hasRotationChannel ? Quaternion.Normalize(composed) : Quaternion.Identity;
    }

    /// <summary>
    /// Converts a quaternion to Euler degrees using the supplied BVH channel order.
    /// </summary>
    /// <param name="rotation">The quaternion to convert.</param>
    /// <param name="channels">The channel sequence that defines the rotation order.</param>
    /// <returns>The Euler angles in degrees.</returns>
    public static Vector3 ToEulerDegrees(Quaternion rotation, IReadOnlyList<BvhChannelType> channels)
    {
        return ToEulerDegrees(rotation, channels, Vector3.Zero);
    }

    /// <summary>
    /// Converts a quaternion to Euler degrees using the supplied BVH channel order and a preferred initial guess.
    /// </summary>
    /// <param name="rotation">The quaternion to convert.</param>
    /// <param name="channels">The channel sequence that defines the rotation order.</param>
    /// <param name="initialGuess">An initial Euler guess in degrees, typically the previous animation sample.</param>
    /// <returns>The Euler angles in degrees.</returns>
    public static Vector3 ToEulerDegrees(Quaternion rotation, IReadOnlyList<BvhChannelType> channels, Vector3 initialGuess)
    {
        ArgumentNullException.ThrowIfNull(channels);

        int[] usedAxes = GetUsedRotationAxes(channels);
        if (usedAxes.Length == 0)
        {
            return Vector3.Zero;
        }

        Quaternion target = Quaternion.Normalize(rotation);
        Vector3 best = RefineEulerCandidate(initialGuess, target, channels, usedAxes);
        float bestScore = ScoreCandidate(target, best, channels);
        if (bestScore >= 0.999999f)
        {
            return MatchContinuity(NormalizeEulerDegrees(best), initialGuess, usedAxes);
        }

        ReadOnlySpan<Vector3> fallbackSeeds =
        [
            Vector3.Zero,
            new Vector3(180.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 180.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 180.0f),
            new Vector3(180.0f, 180.0f, 0.0f),
            new Vector3(180.0f, 0.0f, 180.0f),
            new Vector3(0.0f, 180.0f, 180.0f),
        ];

        for (int i = 0; i < fallbackSeeds.Length; i++)
        {
            Vector3 candidate = RefineEulerCandidate(fallbackSeeds[i], target, channels, usedAxes);
            float candidateScore = ScoreCandidate(target, candidate, channels);
            if (candidateScore > bestScore)
            {
                best = candidate;
                bestScore = candidateScore;
            }
        }

        return MatchContinuity(NormalizeEulerDegrees(best), initialGuess, usedAxes);
    }

    /// <summary>
    /// Adjusts Euler degrees to the equivalent representation nearest to a reference sample.
    /// </summary>
    /// <param name="eulerDegrees">The normalized Euler degrees to unwrap.</param>
    /// <param name="referenceDegrees">The reference Euler sample, typically the previous frame.</param>
    /// <param name="usedAxes">The rotation axes that participate in the BVH channel sequence.</param>
    /// <returns>The equivalent Euler degrees nearest to <paramref name="referenceDegrees"/>.</returns>
    public static Vector3 MatchContinuity(Vector3 eulerDegrees, Vector3 referenceDegrees, IReadOnlyList<int> usedAxes)
    {
        Vector3 result = eulerDegrees;

        for (int i = 0; i < usedAxes.Count; i++)
        {
            int axis = usedAxes[i];
            float reference = axis switch
            {
                0 => referenceDegrees.X,
                1 => referenceDegrees.Y,
                _ => referenceDegrees.Z,
            };

            float value = axis switch
            {
                0 => result.X,
                1 => result.Y,
                _ => result.Z,
            };

            float unwrapped = UnwrapAngleDegrees(value, reference);
            result = axis switch
            {
                0 => new Vector3(unwrapped, result.Y, result.Z),
                1 => new Vector3(result.X, unwrapped, result.Z),
                _ => new Vector3(result.X, result.Y, unwrapped),
            };
        }

        return result;
    }

    /// <summary>
    /// Adjusts one normalized angle to the equivalent representation nearest to a reference angle.
    /// </summary>
    /// <param name="angleDegrees">The normalized angle to unwrap.</param>
    /// <param name="referenceDegrees">The reference angle to stay close to.</param>
    /// <returns>The equivalent angle nearest to <paramref name="referenceDegrees"/>.</returns>
    public static float UnwrapAngleDegrees(float angleDegrees, float referenceDegrees)
    {
        float wrapCount = MathF.Floor((referenceDegrees - angleDegrees + 180.0f) / 360.0f);
        return angleDegrees + (wrapCount * 360.0f);
    }

    /// <summary>
    /// Refines an Euler candidate toward the supplied quaternion for the given BVH channel order.
    /// </summary>
    /// <param name="initialGuess">The starting Euler angles in degrees.</param>
    /// <param name="target">The target quaternion to match.</param>
    /// <param name="channels">The BVH channel sequence that defines the rotation order.</param>
    /// <param name="usedAxes">The unique rotation axes used by the channel sequence.</param>
    /// <returns>The refined Euler angles in degrees.</returns>
    public static Vector3 RefineEulerCandidate(Vector3 initialGuess, Quaternion target, IReadOnlyList<BvhChannelType> channels, IReadOnlyList<int> usedAxes)
    {
        Vector3 best = NormalizeEulerDegrees(initialGuess);
        float bestScore = ScoreCandidate(target, best, channels);
        ReadOnlySpan<float> refinementSteps = [180.0f, 90.0f, 45.0f, 22.5f, 10.0f, 5.0f, 1.0f, 0.25f, 0.05f, 0.01f];

        for (int stepIndex = 0; stepIndex < refinementSteps.Length; stepIndex++)
        {
            float step = refinementSteps[stepIndex];
            bool improved;

            do
            {
                improved = false;

                for (int axisIndex = 0; axisIndex < usedAxes.Count; axisIndex++)
                {
                    int axis = usedAxes[axisIndex];
                    Vector3 positive = OffsetAxis(best, axis, step);
                    float positiveScore = ScoreCandidate(target, positive, channels);
                    if (positiveScore > bestScore)
                    {
                        best = positive;
                        bestScore = positiveScore;
                        improved = true;
                        continue;
                    }

                    Vector3 negative = OffsetAxis(best, axis, -step);
                    float negativeScore = ScoreCandidate(target, negative, channels);
                    if (negativeScore > bestScore)
                    {
                        best = negative;
                        bestScore = negativeScore;
                        improved = true;
                    }
                }
            }
            while (improved);
        }

        return NormalizeEulerDegrees(best);
    }

    /// <summary>
    /// Scores an Euler candidate against a target quaternion using absolute quaternion alignment.
    /// </summary>
    /// <param name="target">The target quaternion.</param>
    /// <param name="candidate">The Euler candidate in degrees.</param>
    /// <param name="channels">The BVH channel sequence that defines the rotation order.</param>
    /// <returns>A value in the range <c>[0, 1]</c> where larger values indicate a better match.</returns>
    public static float ScoreCandidate(Quaternion target, Vector3 candidate, IReadOnlyList<BvhChannelType> channels)
    {
        Quaternion composed = ComposeDegrees(candidate, channels);
        return MathF.Abs(Quaternion.Dot(composed, target));
    }

    /// <summary>
    /// Gets the unique rotation axes used by the supplied BVH channel sequence.
    /// </summary>
    /// <param name="channels">The channel sequence to inspect.</param>
    /// <returns>An array containing the used axis indices in the same order they first appear.</returns>
    public static int[] GetUsedRotationAxes(IReadOnlyList<BvhChannelType> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        int[] usedAxes = new int[3];
        bool hasX = false;
        bool hasY = false;
        bool hasZ = false;
        int count = 0;

        for (int i = 0; i < channels.Count; i++)
        {
            BvhChannelType channel = channels[i];
            if (!BvhFormat.IsRotationChannel(channel))
            {
                continue;
            }

            int axis = BvhFormat.GetAxisIndex(channel);
            switch (axis)
            {
                case 0 when hasX:
                case 1 when hasY:
                case 2 when hasZ:
                    continue;
            }

            switch (axis)
            {
                case 0:
                    hasX = true;
                    break;

                case 1:
                    hasY = true;
                    break;

                default:
                    hasZ = true;
                    break;
            }

            usedAxes[count] = axis;
            count++;
        }

        Array.Resize(ref usedAxes, count);
        return usedAxes;
    }

    /// <summary>
    /// Offsets one Euler component and normalizes the result.
    /// </summary>
    /// <param name="value">The source Euler angles in degrees.</param>
    /// <param name="axis">The axis index to offset.</param>
    /// <param name="delta">The degree delta to apply.</param>
    /// <returns>The offset Euler angles in degrees.</returns>
    public static Vector3 OffsetAxis(Vector3 value, int axis, float delta)
    {
        return axis switch
        {
            0 => NormalizeEulerDegrees(new Vector3(value.X + delta, value.Y, value.Z)),
            1 => NormalizeEulerDegrees(new Vector3(value.X, value.Y + delta, value.Z)),
            _ => NormalizeEulerDegrees(new Vector3(value.X, value.Y, value.Z + delta)),
        };
    }

    /// <summary>
    /// Creates a quaternion representing a single-axis rotation in degrees.
    /// </summary>
    /// <param name="axisIndex">The axis index where <c>0</c> is X, <c>1</c> is Y, and <c>2</c> is Z.</param>
    /// <param name="degrees">The rotation angle in degrees.</param>
    /// <returns>The axis rotation quaternion.</returns>
    public static Quaternion CreateAxisRotation(int axisIndex, float degrees)
    {
        float radians = MathF.PI * degrees / 180.0f;
        return axisIndex switch
        {
            0 => Quaternion.CreateFromAxisAngle(Vector3.UnitX, radians),
            1 => Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians),
            _ => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, radians),
        };
    }

    /// <summary>
    /// Gets one Euler component based on a BVH rotation channel.
    /// </summary>
    /// <param name="eulerDegrees">The Euler angles in degrees.</param>
    /// <param name="channel">The BVH rotation channel whose component should be returned.</param>
    /// <returns>The requested Euler component.</returns>
    public static float GetAxisComponent(Vector3 eulerDegrees, BvhChannelType channel)
    {
        return channel switch
        {
            BvhChannelType.Xrotation => eulerDegrees.X,
            BvhChannelType.Yrotation => eulerDegrees.Y,
            BvhChannelType.Zrotation => eulerDegrees.Z,
            _ => 0.0f,
        };
    }

    /// <summary>
    /// Normalizes Euler degrees to the range <c>(-180, 180]</c> per component.
    /// </summary>
    /// <param name="eulerDegrees">The Euler degrees to normalize.</param>
    /// <returns>The normalized Euler degrees.</returns>
    public static Vector3 NormalizeEulerDegrees(Vector3 eulerDegrees)
    {
        return new Vector3(NormalizeAngleDegrees(eulerDegrees.X), NormalizeAngleDegrees(eulerDegrees.Y), NormalizeAngleDegrees(eulerDegrees.Z));
    }

    /// <summary>
    /// Normalizes one angle in degrees to the range <c>(-180, 180]</c>.
    /// </summary>
    /// <param name="angleDegrees">The angle to normalize.</param>
    /// <returns>The normalized angle in degrees.</returns>
    public static float NormalizeAngleDegrees(float angleDegrees)
    {
        float wrapped = angleDegrees % 360.0f;
        if (wrapped <= -180.0f)
        {
            wrapped += 360.0f;
        }
        else if (wrapped > 180.0f)
        {
            wrapped -= 360.0f;
        }

        return wrapped;
    }
}
