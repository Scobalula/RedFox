using System.Numerics;
using RedFox.Graphics3D;

namespace RedFox.Tests.Graphics3D;

public sealed class FpsCameraTests
{
    [Fact]
    public void UpdateInput_AppliesDollyMagnitudeAlongsideMovement()
    {
        FpsCamera camera = new()
        {
            Position = Vector3.Zero,
            Target = -Vector3.UnitZ,
            MoveSpeed = 4f
        };
        CameraControllerInput input = new(Vector2.Zero, 0f, Vector2.Zero, 0.25f, Vector3.Zero);

        camera.UpdateInput(0.5f, input);

        Assert.Equal(new Vector3(0f, 0f, -0.5f), camera.Position);
    }
}
