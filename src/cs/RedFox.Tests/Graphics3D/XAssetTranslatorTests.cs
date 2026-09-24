using RedFox.Graphics3D.Formats.XAsset;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Tests.Graphics3D;

public sealed class XAssetTranslatorTests
{
    [Theory]
    [InlineData("char_usa_marine_player_head_smg_LOD1.XMODEL_EXPORT")]
    [InlineData("c_hro_hendricks_undercover_fb_lod0.xmodel_bin")]
    public void XModelSamples_ReadAndRoundTrip(string fileName)
    {
        string path = GetReferencePath(fileName);
        if (!File.Exists(path))
            return;

        SceneTranslatorManager manager = new();
        manager.Register(new XModelTranslator());
        Scene scene = manager.Read(path, new SceneTranslatorOptions());
        Mesh[] sourceMeshes = scene.GetDescendants<Mesh>();
        SkeletonBone[] sourceBones = scene.GetDescendants<SkeletonBone>();
        Assert.NotEmpty(sourceMeshes);
        Assert.NotEmpty(sourceBones);
        Assert.All(sourceMeshes, static mesh => Assert.True(mesh.FaceCount > 0));

        foreach (string extension in new[] { ".xmodel_export", ".xmodel_bin" })
        {
            using MemoryStream output = new();
            manager.Write(output, $"roundtrip{extension}", scene, new SceneTranslatorOptions());
            output.Position = 0;
            Scene roundTripped = manager.Read(output, $"roundtrip{extension}", new SceneTranslatorOptions());
            Assert.Equal(sourceMeshes.Sum(static mesh => mesh.FaceCount), roundTripped.GetDescendants<Mesh>().Sum(static mesh => mesh.FaceCount));
            Assert.Equal(sourceBones.Length, roundTripped.GetDescendants<SkeletonBone>().Length - 1);
        }
    }

    [Fact]
    public void XModelWrite_IncludesUnskinnedBones()
    {
        string path = GetReferencePath("char_usa_marine_player_head_smg_LOD1.XMODEL_EXPORT");
        if (!File.Exists(path))
            return;

        SceneTranslatorManager manager = new();
        manager.Register(new XModelTranslator());
        Scene scene = manager.Read(path, new SceneTranslatorOptions());
        SkeletonBone unskinnedParent = scene.RootNode.AddNode(new SkeletonBone("unskinned_parent"));
        unskinnedParent.AddNode(new SkeletonBone("unskinned_child"));

        using MemoryStream output = new();
        manager.Write(output, "roundtrip.xmodel_bin", scene, new SceneTranslatorOptions());
        output.Position = 0;
        Scene roundTripped = manager.Read(output, "roundtrip.xmodel_bin", new SceneTranslatorOptions());
        string[] boneNames = roundTripped.GetDescendants<SkeletonBone>().Select(static bone => bone.Name).ToArray();
        Assert.Contains("unskinned_parent", boneNames);
        Assert.Contains("unskinned_child", boneNames);
    }

    [Fact]
    public void XModelWrite_InjectsTagOriginWhenSceneHasNoBones()
    {
        string path = GetReferencePath("char_usa_marine_player_head_smg_LOD1.XMODEL_EXPORT");
        if (!File.Exists(path))
            return;

        SceneTranslatorManager manager = new();
        manager.Register(new XModelTranslator());
        Scene scene = manager.Read(path, new SceneTranslatorOptions());
        foreach (Mesh mesh in scene.GetDescendants<Mesh>())
        {
            mesh.BoneIndices = null;
            mesh.BoneWeights = null;
            mesh.SkinnedBones = null;
        }
        foreach (SkeletonBone bone in (scene.RootNode.Children ?? []).OfType<SkeletonBone>().ToArray())
            scene.RootNode.RemoveNode(bone);

        using MemoryStream output = new();
        manager.Write(output, "roundtrip.xmodel_bin", scene, new SceneTranslatorOptions());
        output.Position = 0;
        Scene roundTripped = manager.Read(output, "roundtrip.xmodel_bin", new SceneTranslatorOptions());
        SkeletonBone[] bones = roundTripped.GetDescendants<SkeletonBone>();
        Assert.Single(bones, static bone => bone.Name == "tag_origin");
        Assert.All(roundTripped.GetDescendants<Mesh>(), static mesh =>
        {
            DataBuffer boneIndices = Assert.IsAssignableFrom<DataBuffer>(mesh.BoneIndices);
            DataBuffer boneWeights = Assert.IsAssignableFrom<DataBuffer>(mesh.BoneWeights);
            Assert.Equal(0, boneIndices.Get<int>(0, 0, 0));
            Assert.Equal(1f, boneWeights.Get<float>(0, 0, 0));
        });
    }

    [Theory]
    [InlineData("run_death_roll.XANIM_EXPORT")]
    [InlineData("ai_zombie_base_crawl_jump_down_36.xanim_bin")]
    public void XAnimSamples_ReadAndRoundTrip(string fileName)
    {
        string path = GetReferencePath(fileName);
        if (!File.Exists(path))
            return;

        SceneTranslatorManager manager = new();
        manager.Register(new XAnimTranslator());
        Scene scene = manager.Read(path, new SceneTranslatorOptions());
        SkeletonAnimation animation = scene.FirstOfType<SkeletonAnimation>();
        Assert.NotEmpty(animation.Tracks);
        Assert.All(animation.Tracks, static track => Assert.True(track.TranslationCurve?.KeyFrameCount > 0));

        foreach (string extension in new[] { ".xanim_export", ".xanim_bin" })
        {
            using MemoryStream output = new();
            manager.Write(output, $"roundtrip{extension}", scene, new SceneTranslatorOptions());
            output.Position = 0;
            Scene roundTripped = manager.Read(output, $"roundtrip{extension}", new SceneTranslatorOptions());
            SkeletonAnimation result = roundTripped.FirstOfType<SkeletonAnimation>();
            Assert.Equal(animation.Tracks.Count, result.Tracks.Count);
            Assert.Equal(animation.Framerate, result.Framerate);
            Assert.Equal(animation.GetAnimationFrameRange(), result.GetAnimationFrameRange());
        }
    }

    private static string GetReferencePath(string fileName)
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "ref", fileName);
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }
        return string.Empty;
    }
}
