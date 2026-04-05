using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Viewing;

public static class CameraClipPlanes
{
    public static void Configure(Camera camera, in SceneBoundsInfo bounds)
    {
        ArgumentNullException.ThrowIfNull(camera);

        Vector3 forward = camera.GetForward();
        if (!IsFinite(forward))
            forward = Vector3.Normalize(camera.Target - camera.Position);

        if (!IsFinite(forward))
            forward = -Vector3.UnitZ;

        float maxDepth = float.NegativeInfinity;
        float radius = MathF.Max(bounds.Radius, 0.1f);

        foreach (Vector3 corner in EnumerateCorners(bounds))
        {
            float depth = Vector3.Dot(corner - camera.Position, forward);
            maxDepth = MathF.Max(maxDepth, depth);
        }

        float centerDepth = Vector3.Dot(bounds.Center - camera.Position, forward);
        float farPlane = MathF.Max(maxDepth, centerDepth + radius);

        if (!float.IsFinite(farPlane) || farPlane <= 0.0f)
        {
            ConfigureFromRadius(camera, bounds.Center, bounds.Radius);
            return;
        }

        farPlane = MathF.Max(farPlane + (radius * 0.1f), radius * 2.0f);
        // Depth precision comes from the logarithmic fragment depth path, so keep
        // the near plane intentionally conservative to avoid clipping close views.
        float nearPlane = MathF.Max(MathF.Max(radius * 1e-5f, farPlane / 1_000_000.0f), 1e-5f);
        nearPlane = MathF.Min(nearPlane, farPlane * 0.25f);

        camera.NearPlane = nearPlane;
        camera.FarPlane = MathF.Max(nearPlane + 1.0f, farPlane);
    }

    private static void ConfigureFromRadius(Camera camera, Vector3 sceneCenter, float sceneRadius)
    {
        float radius = MathF.Max(sceneRadius, 0.1f);
        Vector3 forward = camera.GetForward();
        if (!IsFinite(forward))
            forward = Vector3.Normalize(camera.Target - camera.Position);

        if (!IsFinite(forward))
            forward = -Vector3.UnitZ;

        float centerDepth = Vector3.Dot(sceneCenter - camera.Position, forward);
        float farPlane = MathF.Max(centerDepth + radius, radius * 2.0f);
        farPlane = MathF.Max(farPlane + (radius * 0.1f), 1.0f);
        float nearPlane = MathF.Max(MathF.Max(radius * 1e-5f, farPlane / 1_000_000.0f), 1e-5f);
        nearPlane = MathF.Min(nearPlane, farPlane * 0.25f);

        camera.NearPlane = nearPlane;
        camera.FarPlane = MathF.Max(nearPlane + 1.0f, farPlane);
    }

    private static IEnumerable<Vector3> EnumerateCorners(SceneBoundsInfo bounds)
    {
        yield return new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z);
        yield return new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Max.Z);
        yield return new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Min.Z);
        yield return new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Max.Z);
        yield return new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Min.Z);
        yield return new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Max.Z);
        yield return new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Min.Z);
        yield return new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
