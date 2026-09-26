using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Formats.Cast;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class CastTranslatorTests
{
    [Fact]
    public void CastTranslator_RoundTrip_ModelAndAnimationInOneFileGetUniqueNames()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new CastTranslator());

        Scene source = new("CastSample");
        SkeletonBone bone = source.RootNode.AddNode(new SkeletonBone("root"));
        bone.BindTransform.LocalPosition = new Vector3(0f, 1f, 0f);

        SkeletonAnimation animation = source.RootNode.AddNode(new SkeletonAnimation("walk"));
        SkeletonAnimationTrack track = new("root");
        track.AddTranslationFrame(0f, Vector3.Zero);
        track.AddTranslationFrame(1f, Vector3.One);
        animation.Tracks.Add(track);

        using MemoryStream stream = new();
        manager.Write(stream, "sample.cast", source, new SceneTranslatorOptions());
        stream.Position = 0;

        Scene loaded = manager.Read(stream, "sample.cast", new SceneTranslatorOptions());

        Assert.Equal("sample", Assert.Single(loaded.GetDescendants<MeshGroup>()).Name);
        Assert.Equal("sample_1", Assert.Single(loaded.GetDescendants<SkeletonAnimation>()).Name);
        Assert.Single(loaded.EnumerateHierarchy<SkeletonBone>(), static loadedBone => loadedBone.Name == "root");
    }
}
