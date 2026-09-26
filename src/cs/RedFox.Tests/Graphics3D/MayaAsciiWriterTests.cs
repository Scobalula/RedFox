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
        MeshGroup model = scene.RootNode.AddNode(new MeshGroup { Name = "Model" });
        SkeletonBone root = model.AddNode(new SkeletonBone("root"));
        SkeletonBone tip = root.AddNode(new SkeletonBone("tip"));

        model.AddNode(new ParentConstraintNode("parent_con", tip, root));
        model.AddNode(new OrientConstraintNode("orient_con", tip, root) { SkipY = true });
        model.AddNode(new PointConstraintNode("point_con", tip, root) { SkipX = true });
        model.AddNode(new ScaleConstraintNode("scale_con", tip, root));
        scene.RootNode.AddNode(new PerspectiveCamera { Name = "shot_cam" });

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

    private static string Write(Scene scene)
    {
        SceneTranslatorManager manager = new();
        manager.Register(new MayaAsciiTranslator());

        using MemoryStream stream = new();
        manager.Write(stream, "scene.ma", scene, new SceneTranslatorOptions());

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
