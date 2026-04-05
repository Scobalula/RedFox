using System.Numerics;
using OpenGlSceneBounds = RedFox.Graphics3D.OpenGL.Viewing.SceneBounds;
using OpenGlSceneBoundsInfo = RedFox.Graphics3D.OpenGL.Viewing.SceneBoundsInfo;

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
        => TryGetBounds(scene, Matrix4x4.Identity, out bounds);

    public static bool TryGetBounds(Scene scene, Matrix4x4 sceneTransform, out SceneBoundsInfo bounds)
    {
        bool hasBounds = OpenGlSceneBounds.TryGetBounds(scene, sceneTransform, out OpenGlSceneBoundsInfo openGlBounds);
        bounds = new SceneBoundsInfo(openGlBounds.Min, openGlBounds.Max, openGlBounds.Center, openGlBounds.Radius);
        return hasBounds;
    }
}

public readonly record struct SceneBoundsInfo(Vector3 Min, Vector3 Max, Vector3 Center, float Radius);
