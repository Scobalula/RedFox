using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Preview;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneBoundsTests
{
    [Fact]
    public void TryGetBounds_AppliesSceneTransform()
    {
        Scene scene = new("BoundsScene");
        Mesh mesh = scene.RootNode.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([1f, 2f, 3f], 1, 3);

        Matrix4x4 sceneTransform = Matrix4x4.CreateTranslation(5.0f, -2.0f, 1.0f);

        bool hasBounds = SceneBounds.TryGetBounds(scene, sceneTransform, out SceneBoundsInfo bounds);

        Assert.True(hasBounds);
        Assert.Equal(6.0f, bounds.Center.X, 4);
        Assert.Equal(0.0f, bounds.Center.Y, 4);
        Assert.Equal(4.0f, bounds.Center.Z, 4);
    }
}
