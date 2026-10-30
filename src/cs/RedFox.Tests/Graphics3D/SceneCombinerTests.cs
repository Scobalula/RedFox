using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneCombinerTests
{
    [Fact]
    public void Combine_InPlacePreservesIndependentSkeletonsAndSourceReferences()
    {
        Scene first				= CreateModel("first", 3);
        Scene second			= CreateModel("second", 8);
        Scene combined			= SceneCombiner.Combine([first, second], SceneCombineMode.InPlace, CancellationToken.None);
        SkeletonBone[] bones	= combined.EnumerateDescendants<SkeletonBone>().ToArray();
        Mesh[] meshes			= combined.EnumerateDescendants<Mesh>().ToArray();

        Assert.Equal(2, bones.Length);
        Assert.Same(bones[0], meshes[0].Skin!.Bones[0]);
        Assert.Same(bones[1], meshes[1].Skin!.Bones[0]);
        Assert.Equal(new Vector3(3, 0, 0), bones[0].GetBindWorldPosition());
        Assert.Equal(new Vector3(8, 0, 0), bones[1].GetBindWorldPosition());
        Assert.NotSame(first.EnumerateDescendants<SkeletonBone>().Single(), bones[0]);
        Assert.Same(first, first.EnumerateDescendants<SkeletonBone>().Single().Scene);
    }

    [Fact]
    public void Combine_ConnectBonesSharesBaseAndRetainsBindOffsetsAndUniqueBranches()
    {
        Scene first		= CreateModel("first", 3);
        Scene second	= CreateModel("second", 8);
        second.EnumerateDescendants<SkeletonBone>().Single().AddNode(new SkeletonBone("extra"));
        Scene combined		= SceneCombiner.Combine([first, second], SceneCombineMode.ConnectBones, CancellationToken.None);
        SkeletonBone root	= combined.EnumerateDescendants<SkeletonBone>().Single(bone => bone.Name == "root");
        Mesh[] meshes		= combined.EnumerateDescendants<Mesh>().ToArray();

        Assert.All(meshes, mesh => Assert.Same(root, mesh.Skin!.Bones[0]));
        Assert.Equal(new Vector3(3, 0, 0), root.GetBindWorldPosition());
        Assert.Same(root, combined.EnumerateDescendants<SkeletonBone>().Single(bone => bone.Name == "extra").Parent);
        Assert.Equal(-8, meshes[1].Skin!.InverseBindMatrices![0].M41);
        Assert.Null(second.EnumerateDescendants<Mesh>().Single().Skin!.InverseBindMatrices);
        Assert.Equal(2, second.EnumerateDescendants<SkeletonBone>().Count());
    }

    [Fact]
    public void Combine_AmbiguousBoneNamesFailOnlyInConnectMode()
    {
        Scene model = CreateModel("model", 0);
        model.AddNode(new Group("other")).AddNode(new SkeletonBone("root"));
        Assert.Throws<InvalidOperationException>(() => SceneCombiner.Combine([model, CreateModel("other", 0)], SceneCombineMode.ConnectBones, CancellationToken.None));
        Assert.Equal(3, SceneCombiner.Combine([model, CreateModel("other", 0)], SceneCombineMode.InPlace, CancellationToken.None).EnumerateDescendants<SkeletonBone>().Count());
    }

    [Fact]
    public void BindAnimations_ExternalClipDrivesAllIndependentSkeletons()
    {
        Scene combined					= SceneCombiner.Combine([CreateModel("a", 0), CreateModel("b", 0)], SceneCombineMode.InPlace, CancellationToken.None);
        SkeletonAnimation clip			= combined.AddNode(new SkeletonAnimation("walk"));
        SkeletonAnimationTrack track	= new("root");
        track.AddTranslationFrame(0, Vector3.Zero);
        track.AddTranslationFrame(30, Vector3.One);
        clip.Tracks.Add(track);
        SceneCombiner.BindAnimations(combined, SceneCombineMode.InPlace, CancellationToken.None);

        Assert.Equal(2, combined.AnimationPlayers.Count);
        foreach (AnimationPlayer player in combined.AnimationPlayers)
        {
            player.Update(30);
        }
        Assert.All(combined.EnumerateDescendants<SkeletonBone>(), bone => Assert.Equal(Vector3.One, bone.LiveTransform.LocalPosition));
    }

    [Fact]
    public void BindAnimations_EmbeddedClipStaysWithItsModel()
    {
        Scene first						= CreateModel("first", 0);
        SkeletonAnimation clip			= first.AddNode(new SkeletonAnimation("clip"));
        SkeletonAnimationTrack track	= new("root");
        track.AddTranslationFrame(0, Vector3.Zero);
        clip.Tracks.Add(track);
        Scene combined = SceneCombiner.Combine([first, CreateModel("second", 0)], SceneCombineMode.InPlace, CancellationToken.None);
        SceneCombiner.BindAnimations(combined, SceneCombineMode.InPlace, CancellationToken.None);
        Assert.Single(combined.AnimationPlayers);
    }

    [Fact]
    public void Combine_ObservesCancellation()
    {
        Assert.Throws<OperationCanceledException>(() => SceneCombiner.Combine([CreateModel("model", 0)], SceneCombineMode.InPlace, new CancellationToken(true)));
    }

    private static Scene CreateModel(string name, float position)
    {
        Scene scene							= new(name);
        SkeletonBone bone					= scene.AddNode(new SkeletonBone("root"));
        bone.BindTransform.LocalPosition	= new Vector3(position, 0, 0);
        Mesh mesh							= scene.AddNode(new Mesh { Name = "mesh" });
        mesh.Skin							= new Skin([bone], new DataBuffer<uint>(1, 1, 1), new DataBuffer<float>(1, 1, 1));
        return scene;
    }
}
