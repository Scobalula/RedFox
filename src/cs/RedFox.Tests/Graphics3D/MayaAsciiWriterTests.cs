using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Formats.MayaAscii;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class MayaAsciiWriterTests
{
    [Fact]
    public void MayaAsciiWriter_WritesAllConstraintTypesAndDerivedCameras()
    {
        Scene scene = new("maya");
        MeshGroup model = scene.RootNode.AddNode(new MeshGroup
        {
            Name = "Model",
        });
        SkeletonBone root = model.AddNode(new SkeletonBone("root"));
        SkeletonBone tip = root.AddNode(new SkeletonBone("tip"));

        model.AddNode(new ParentConstraintNode("parent_con", tip, root));
        model.AddNode(new OrientConstraintNode("orient_con", tip, root)
        {
            SkipY = true,
        });
        model.AddNode(new PointConstraintNode("point_con", tip, root)
        {
            SkipX = true,
        });
        model.AddNode(new ScaleConstraintNode("scale_con", tip, root));
        scene.RootNode.AddNode(new PerspectiveCamera
        {
            Name = "shot_cam",
        });

        string output = Write(scene);

        Assert.Contains("createNode parentConstraint -n \"parent_con\"", output);
        Assert.Contains("createNode orientConstraint -n \"orient_con\"", output);
        Assert.Contains("createNode pointConstraint -n \"point_con\"", output);
        Assert.Contains("createNode scaleConstraint -n \"scale_con\"", output);
        Assert.Contains("\"shot_cam\"", output);
        Assert.DoesNotContain("orient_con.cry", output);
        Assert.Contains("orient_con.crx", output);
        Assert.DoesNotContain("point_con.ctx", output);
        Assert.Contains("point_con.cty", output);
        Assert.Contains("scale_con.csz", output);
    }

    [Fact]
    public void MayaAsciiWriter_ConvertsAnimationFramesToConfiguredTimeUnit()
    {
        Scene scene = new("maya");
        SkeletonBone bone = scene.RootNode.AddNode(new SkeletonBone("root"));
        bone.BindTransform.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);
        SkeletonAnimation animation = scene.RootNode.AddNode(new SkeletonAnimation("walk"));
        animation.Framerate = 48f;
        SkeletonAnimationTrack track = new("root");
        track.TransformType = TransformType.Absolute;
        track.AddTranslationFrame(0f, Vector3.Zero);
        track.AddTranslationFrame(24f, Vector3.One);
        track.AddRotationFrame(0f, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f));
        track.AddRotationFrame(24f, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI));
        animation.Tracks.Add(track);
        MayaAsciiWriteOptions options = new()
        {
            TimeUnit = MayaTimeUnit.Film,
        };

        using MemoryStream stream = new();
        MayaAsciiWriter writer = new(stream, options);
        writer.Write(scene, "maya");
        string output = Encoding.ASCII.GetString(stream.ToArray());

        Assert.Contains(" 12 1", output);
        Assert.Contains(" 12 90", output);
    }

    private static string Write(Scene scene)
    {
        SceneTranslatorManager manager = new();
        manager.Register(new MayaAsciiTranslator());

        using MemoryStream stream = new();
        manager.Write(stream, "scene.ma", scene, new SceneTranslatorOptions());

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
