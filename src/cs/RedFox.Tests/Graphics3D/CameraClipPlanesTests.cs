using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL.Viewing;

namespace RedFox.Tests.Graphics3D;

public sealed class CameraClipPlanesTests
{
    [Fact]
    public void Configure_TopDownView_UsesConservativeNearPlane()
    {
        Camera camera = new()
        {
            Position = new Vector3(0.0f, 10.0f, 0.0f),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
        };

        SceneBoundsInfo bounds = new(
            new Vector3(-5.0f, -5.0f, -5.0f),
            new Vector3(5.0f, 5.0f, 5.0f),
            Vector3.Zero,
            8.6602545f);

        CameraClipPlanes.Configure(camera, bounds);

        Assert.True(camera.NearPlane < 0.01f);
        Assert.True(camera.FarPlane > 10.0f);
    }

    [Fact]
    public void Configure_CameraInsideBounds_KeepsNearPlaneTiny()
    {
        Camera camera = new()
        {
            Position = new Vector3(0.0f, 1.0f, 0.0f),
            Target = new Vector3(0.0f, 1.0f, -1.0f),
            Up = Vector3.UnitY,
        };

        SceneBoundsInfo bounds = new(
            new Vector3(-5.0f, -5.0f, -5.0f),
            new Vector3(5.0f, 5.0f, 5.0f),
            Vector3.Zero,
            8.6602545f);

        CameraClipPlanes.Configure(camera, bounds);

        Assert.True(camera.NearPlane < 0.01f);
        Assert.True(camera.FarPlane > 1.0f);
    }
}
