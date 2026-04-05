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

    [Fact]
    public void TryGetBounds_TracksAnimatedSkinning()
    {
        Scene scene = new("SkinnedBoundsScene");
        Skeleton skeleton = scene.RootNode.AddNode(new Skeleton("Skeleton"));
        SkeletonBone bone = skeleton.AddNode(new SkeletonBone("Root"));
        Mesh mesh = scene.RootNode.AddNode(new Mesh { Name = "Mesh" });

        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.BoneIndices = new DataBuffer<int>([0], 1, 1);
        mesh.BoneWeights = new DataBuffer<float>([1f], 1, 1);
        mesh.SetSkinBinding([bone]);

        Assert.True(SceneBounds.TryGetBounds(scene, out SceneBoundsInfo initialBounds));
        Assert.Equal(0.0f, initialBounds.Center.X, 4);

        bone.LiveTransform.LocalPosition = new Vector3(5.0f, 0.0f, 0.0f);
        bone.LiveTransform.WorldPosition = null;

        Assert.True(SceneBounds.TryGetBounds(scene, out SceneBoundsInfo animatedBounds));
        Assert.Equal(5.0f, animatedBounds.Center.X, 4);
    }

    [Fact]
    public void TryGetBounds_TracksActiveBlendShapeWeights()
    {
        Scene scene = new("MorphBoundsScene");
        Mesh mesh = scene.RootNode.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.DeltaPositions = new DataBuffer<float>([4f, 0f, 0f], 1, 3);

        BlendShape blendShape = new("Target0", 0, mesh)
        {
            Weight = 0.5f,
        };
        mesh.AddNode(blendShape);

        Assert.True(SceneBounds.TryGetBounds(scene, out SceneBoundsInfo bounds));
        Assert.Equal(2.0f, bounds.Center.X, 4);
        Assert.Equal(0, blendShape.TargetIndex);
    }
}
