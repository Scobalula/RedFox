using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Viewing;

/// <summary>
/// Provides line segment clipping against the perspective depth range in view space.
/// </summary>
public static class ViewSpaceLineClipper
{
    /// <summary>
    /// Clips a line segment in view space to the perspective near and far clipping planes.
    /// </summary>
    /// <param name="start">The line start position in view space. Updated in place if clipped.</param>
    /// <param name="end">The line end position in view space. Updated in place if clipped.</param>
    /// <param name="nearPlane">The near clipping plane distance (positive value).</param>
    /// <param name="farPlane">The far clipping plane distance (positive value).</param>
    /// <returns><c>true</c> if any portion of the line remains after clipping; <c>false</c> if the line is fully outside the frustum.</returns>
    public static bool TryClipToPerspectiveDepth(ref Vector3 start, ref Vector3 end, float nearPlane, float farPlane)
    {
        nearPlane = MathF.Max(nearPlane, 1e-5f);
        farPlane = MathF.Max(farPlane, nearPlane + 1e-3f);

        float nearZ = -nearPlane;
        float farZ = -farPlane;

        if (!ClipToZPlane(ref start, ref end, nearZ, keepLessOrEqual: true))
            return false;

        if (!ClipToZPlane(ref start, ref end, farZ, keepLessOrEqual: false))
            return false;

        return true;
    }

    private static bool ClipToZPlane(ref Vector3 start, ref Vector3 end, float planeZ, bool keepLessOrEqual)
    {
        bool startInside = keepLessOrEqual ? start.Z <= planeZ : start.Z >= planeZ;
        bool endInside = keepLessOrEqual ? end.Z <= planeZ : end.Z >= planeZ;

        if (startInside && endInside)
            return true;

        if (!startInside && !endInside)
            return false;

        float denominator = end.Z - start.Z;
        if (MathF.Abs(denominator) <= 1e-8f)
            return false;

        float t = Math.Clamp((planeZ - start.Z) / denominator, 0.0f, 1.0f);
        Vector3 intersection = Vector3.Lerp(start, end, t);

        if (!startInside)
            start = intersection;
        else
            end = intersection;

        return true;
    }
}
