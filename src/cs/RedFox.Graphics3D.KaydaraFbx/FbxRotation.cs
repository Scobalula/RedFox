using System.Numerics;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Provides FBX Euler and quaternion conversion helpers using XYZ order in degrees.
/// </summary>
public static class FbxRotation
{
    /// <summary>
    /// Creates a quaternion from FBX XYZ Euler angles in degrees.
    /// </summary>
    /// <param name="eulerDegrees">Euler rotation in degrees.</param>
    /// <returns>The equivalent quaternion.</returns>
    public static Quaternion FromEulerDegreesXyz(Vector3 eulerDegrees)
    {
        float x = MathF.PI * eulerDegrees.X / 180f;
        float y = MathF.PI * eulerDegrees.Y / 180f;
        float z = MathF.PI * eulerDegrees.Z / 180f;
        Quaternion qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);
        Quaternion qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, y);
        Quaternion qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z);
        return Quaternion.Normalize(qz * qy * qx);
    }

    /// <summary>
    /// Converts a quaternion to FBX XYZ Euler angles in degrees.
    /// </summary>
    /// <param name="rotation">The source quaternion.</param>
    /// <returns>Euler rotation in degrees.</returns>
    public static Vector3 ToEulerDegreesXyz(Quaternion rotation)
    {
        Quaternion target = Quaternion.Normalize(rotation);
        Vector3 best = Vector3.Zero;
        float bestDot = MathF.Abs(Quaternion.Dot(FromEulerDegreesXyz(best), target));
        ReadOnlySpan<Vector3> seeds =
        [
            Vector3.Zero,
            CreateSeedFromMatrix(target, useAlternateSolution: false),
            CreateSeedFromMatrix(target, useAlternateSolution: true),
        ];

        for (int i = 0; i < seeds.Length; i++)
        {
            Vector3 candidate = RefineEulerCandidate(seeds[i], target);
            float candidateDot = MathF.Abs(Quaternion.Dot(FromEulerDegreesXyz(candidate), target));
            if (candidateDot > bestDot)
            {
                best = candidate;
                bestDot = candidateDot;
            }
        }

        return NormalizeEulerDegrees(best);
    }

    /// <summary>
    /// Refines an initial Euler-angle guess towards the target quaternion using hill-climbing.
    /// </summary>
    /// <param name="initialGuess">The starting Euler angles in degrees.</param>
    /// <param name="target">The target quaternion to match.</param>
    /// <returns>The refined Euler angles in degrees.</returns>
    public static Vector3 RefineEulerCandidate(Vector3 initialGuess, Quaternion target)
    {
        Vector3 best = NormalizeEulerDegrees(initialGuess);
        float bestDot = MathF.Abs(Quaternion.Dot(FromEulerDegreesXyz(best), target));
        float[] stepSizes = [180f, 90f, 45f, 22.5f, 10f, 5f, 1f, 0.25f, 0.05f, 0.01f];

        for (int stepIndex = 0; stepIndex < stepSizes.Length; stepIndex++)
        {
            float step = stepSizes[stepIndex];
            bool improved;
            do
            {
                improved = false;
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 positive = OffsetAxis(best, axis, step);
                    float positiveDot = MathF.Abs(Quaternion.Dot(FromEulerDegreesXyz(positive), target));
                    if (positiveDot > bestDot)
                    {
                        best = positive;
                        bestDot = positiveDot;
                        improved = true;
                        continue;
                    }

                    Vector3 negative = OffsetAxis(best, axis, -step);
                    float negativeDot = MathF.Abs(Quaternion.Dot(FromEulerDegreesXyz(negative), target));
                    if (negativeDot > bestDot)
                    {
                        best = negative;
                        bestDot = negativeDot;
                        improved = true;
                    }
                }
            }
            while (improved);
        }

        return best;
    }

    /// <summary>
    /// Extracts a closed-form Euler seed from a quaternion using the XYZ decomposition formula.
    /// </summary>
    /// <param name="rotation">The source quaternion.</param>
    /// <param name="useAlternateSolution">When <see langword="true"/> uses the alternate Asin branch.</param>
    /// <returns>Euler angles in degrees.</returns>
    public static Vector3 CreateSeedFromMatrix(Quaternion rotation, bool useAlternateSolution)
    {
        Matrix4x4 m = Matrix4x4.CreateFromQuaternion(rotation);
        float y = useAlternateSolution
            ? MathF.PI - MathF.Asin(Math.Clamp(m.M13, -1f, 1f))
            : MathF.Asin(Math.Clamp(m.M13, -1f, 1f));
        float cosY = MathF.Cos(y);
        float x;
        float z;

        if (MathF.Abs(cosY) > 0.00001f)
        {
            x = MathF.Atan2(-m.M23 / cosY, m.M33 / cosY);
            z = MathF.Atan2(-m.M12 / cosY, m.M11 / cosY);
        }
        else
        {
            x = MathF.Atan2(m.M32, m.M22);
            z = 0f;
        }

        return NormalizeEulerDegrees(new Vector3(x, y, z) * (180f / MathF.PI));
    }

    /// <summary>
    /// Returns a copy of <paramref name="value"/> with the specified axis component shifted by <paramref name="delta"/> and normalised.
    /// </summary>
    /// <param name="value">The base Euler angles in degrees.</param>
    /// <param name="axis">The axis index (0=X, 1=Y, 2=Z).</param>
    /// <param name="delta">The degree offset to apply.</param>
    /// <returns>The modified and normalised Euler angles in degrees.</returns>
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
    /// Normalises each Euler component to the range (-180, 180].
    /// </summary>
    /// <param name="eulerDegrees">The raw Euler angles in degrees.</param>
    /// <returns>Normalised Euler angles in degrees.</returns>
    public static Vector3 NormalizeEulerDegrees(Vector3 eulerDegrees)
    {
        return new Vector3(NormalizeAngle(eulerDegrees.X), NormalizeAngle(eulerDegrees.Y), NormalizeAngle(eulerDegrees.Z));
    }

    /// <summary>
    /// Normalises a single angle in degrees to the range (-180, 180].
    /// </summary>
    /// <param name="angle">The raw angle in degrees.</param>
    /// <returns>The normalised angle in degrees.</returns>
    public static float NormalizeAngle(float angle)
    {
        float wrapped = angle % 360f;
        if (wrapped <= -180f)
        {
            wrapped += 360f;
        }
        else if (wrapped > 180f)
        {
            wrapped -= 360f;
        }

        return wrapped;
    }
}
