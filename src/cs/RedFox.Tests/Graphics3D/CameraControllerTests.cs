using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL.Cameras;

namespace RedFox.Tests.Graphics3D;

public sealed class CameraControllerTests
{
    [Fact]
    public void Update_ArcballOrbitDragRight_UsesInvertedHorizontalResponse()
    {
        Camera camera = new()
        {
            Position = new Vector3(0.0f, 0.0f, 5.0f),
            Target = Vector3.Zero,
        };

        CameraController controller = new(camera)
        {
            Mode = CameraMode.Arcball,
            OrbitSensitivity = 0.01f,
        };

        controller.Update(new CameraInputState(
            new Vector2(10.0f, 0.0f),
            0.0f,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false), 0.0f);

        Assert.True(camera.Position.X < 0.0f);
    }

    [Fact]
    public void Update_ArcballPositiveWheelDelta_MovesCameraCloser()
    {
        Camera camera = new()
        {
            Position = new Vector3(0.0f, 0.0f, 5.0f),
            Target = Vector3.Zero,
        };

        CameraController controller = new(camera)
        {
            Mode = CameraMode.Arcball,
            ZoomSensitivity = 0.15f,
        };

        float distanceBefore = Vector3.Distance(camera.Position, camera.Target);

        controller.Update(new CameraInputState(
            Vector2.Zero,
            1.0f,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false), 0.0f);

        float distanceAfter = Vector3.Distance(camera.Position, camera.Target);
        Assert.True(distanceAfter < distanceBefore);
    }
}
