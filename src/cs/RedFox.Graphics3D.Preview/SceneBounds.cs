using System.Numerics;

namespace RedFox.Graphics3D.Preview;

public static class SceneBounds
{
    public static bool TryGetBounds(Scene scene, out Vector3 center, out float radius)
    {
        bool hasBounds = TryGetBounds(scene, out SceneBoundsInfo bounds);
        center = bounds.Center;
        radius = bounds.Radius;
        return hasBounds;
    }

    public static bool TryGetBounds(Scene scene, out SceneBoundsInfo bounds)
    {
        ArgumentNullException.ThrowIfNull(scene);

        bool hasBounds = false;
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);

        foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
        {
            if (mesh.Positions is null)
                continue;

            Matrix4x4 world = mesh.GetActiveWorldMatrix();

            for (int vertexIndex = 0; vertexIndex < mesh.Positions.ElementCount; vertexIndex++)
            {
                Vector3 worldPosition = Vector3.Transform(mesh.Positions.GetVector3(vertexIndex, 0), world);

                min = Vector3.Min(min, worldPosition);
                max = Vector3.Max(max, worldPosition);
                hasBounds = true;
            }
        }

        if (!hasBounds)
        {
            bounds = new SceneBoundsInfo(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1.0f);
            return false;
        }

        Vector3 center = (min + max) * 0.5f;
        float radius = MathF.Max(0.5f * Vector3.Distance(min, max), 1.0f);
        bounds = new SceneBoundsInfo(min, max, center, radius);
        return true;
    }
}

public readonly record struct SceneBoundsInfo(Vector3 Min, Vector3 Max, Vector3 Center, float Radius);
