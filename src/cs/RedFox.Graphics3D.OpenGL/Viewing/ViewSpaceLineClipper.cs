using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Viewing;

public static class ViewSpaceLineClipper
{
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
