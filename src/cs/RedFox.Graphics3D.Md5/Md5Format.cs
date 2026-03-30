using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Provides constants and format-specific utilities for the id Tech 4 MD5 file format.
/// </summary>
public static class Md5Format
{
    /// <summary>
    /// The MD5 format version supported by this library.
    /// </summary>
    public const int Version = 10;

    /// <summary>
    /// Computes the W component of an MD5 quaternion from its X, Y, and Z components.
    /// <para>
    /// The MD5 format stores only three quaternion components and recovers the fourth
    /// using the unit-quaternion constraint <c>x² + y² + z² + w² = 1</c>.  The sign of
    /// W is always chosen to be non-positive.
    /// </para>
    /// </summary>
    /// <param name="x">The X component of the quaternion.</param>
    /// <param name="y">The Y component of the quaternion.</param>
    /// <param name="z">The Z component of the quaternion.</param>
    /// <returns>A fully reconstructed, normalized <see cref="Quaternion"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion ComputeQuaternion(float x, float y, float z)
    {
        float t = 1.0f - (x * x) - (y * y) - (z * z);
        float w = t < 0.0f ? 0.0f : -MathF.Sqrt(t);
        return new Quaternion(x, y, z, w);
    }

    /// <summary>
    /// Formats a float value with sufficient precision for MD5 file output using invariant culture.
    /// </summary>
    /// <param name="v">The value to format.</param>
    /// <returns>A fixed-precision invariant-culture numeric string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string F(float v) => v.ToString("G10", CultureInfo.InvariantCulture);
}
