using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Formats.BiovisionHierarchy;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class BvhMultiRootTests
{
    [Fact]
    public void BvhTranslator_WritesMultipleRootsUnderSyntheticSkeletonJoint()
    {
        Scene source = new("bvh");
        Skeleton skeleton = source.RootNode.AddNode(new Skeleton("Armature"));
        skeleton.AddNode(new SkeletonBone("rootA"));
        skeleton.AddNode(new SkeletonBone("rootB"));
        SceneTranslatorManager manager = new();
        manager.Register(new BvhTranslator());

        using MemoryStream stream = new();
        manager.Write(stream, "scene.bvh", source, new SceneTranslatorOptions(), token: null);
        string output = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("ROOT Armature", output);
        Assert.Contains("JOINT rootA", output);
        Assert.Contains("JOINT rootB", output);
        Assert.Contains("  CHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation", output);

        stream.Position = 0;
        Scene loaded = manager.Read(stream, "scene.bvh", new SceneTranslatorOptions(), token: null);
        Assert.Equal(["Armature", "rootA", "rootB"], loaded.GetDescendants<SkeletonBone>().Select(static bone => bone.Name));
    }
}
