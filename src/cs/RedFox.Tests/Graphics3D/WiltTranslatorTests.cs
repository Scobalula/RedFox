using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Formats.Wilt;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class WiltTranslatorTests
{
    [Fact]
    public void WiltModelTranslator_RoundTrip_PreservesModelContent()
    {
        Scene source = CreateModelScene();
        Scene loaded = Read(Write(source, "sample.wiltmodel"), "sample.wiltmodel");

        Assert.Equal(SceneUpAxis.Z, loaded.UpAxis);
        Assert.Equal("Test Game", loaded.Attributes!["source_game"]);

        SkeletonBone root	= loaded.FindDescendant<SkeletonBone>("root");
        SkeletonBone child	= loaded.FindDescendant<SkeletonBone>("child");

        Assert.Same(root, child.Parent);
        Assert.Equal(new Vector3(0, 1, 0), child.GetBindLocalPosition());

        Material material = loaded.FindDescendant<Material>("material");

        Assert.True(material.DoubleSided);
        Assert.Equal("textures/diffuse.png", material.GetDiffuseMap().FilePath);
        Assert.Equal(new Vector4(1, 0.5f, 0.25f, 1), material.DiffuseColor);
        Assert.Equal(2.5f, material.Attributes!["custom_scale"]);

        Mesh mesh = loaded.FindDescendant<Mesh>("mesh");

        Assert.IsType<DataBuffer<Half>>(mesh.Normals);
        Assert.IsType<DataBuffer<byte>>(mesh.ColorLayers);
        Assert.Equal([0, 1, 2], Assert.IsType<DataBuffer<ushort>>(mesh.FaceIndices).ToArray());
        Assert.Equal(2, mesh.UVLayerCount);
        Assert.Same(material, Assert.Single(mesh.Materials!));
        Assert.Equal((0, 3), Assert.Single(mesh.MaterialIndexRanges!));
        Assert.Equal([root, child], mesh.Skin!.Bones);
        Assert.Equal(2, mesh.Skin.InfluenceCount);
        Assert.Equal(["smile"], mesh.Morph!.TargetNames);
        Assert.Equal(0.25f, mesh.Morph.Weights[0]);
        Assert.Equal(new Vector3(0, 0, 1), mesh.Morph.DeltaPositions!.GetVector3(2, 0));

        Hair hair = loaded.FindDescendant<Hair>("hair");

        Assert.Equal([2], hair.StrandSegments);
        Assert.Same(material, hair.Material);

        IKHandleNode ik = loaded.FindDescendant<IKHandleNode>("ik");

        Assert.Same(root, ik.StartNode);
        Assert.Same(child, ik.TargetNode);
        Assert.Null(ik.PoleNode);
        Assert.Equal(0.5f, ik.Weight);

        OrientConstraintNode constraint = loaded.FindDescendant<OrientConstraintNode>("orient");

        Assert.Same(child, constraint.ConstrainedNode);
        Assert.True(constraint.SkipY);
        Assert.False(constraint.SkipX);
    }

    [Fact]
    public void WiltAnimTranslator_RoundTrip_PreservesAnimationContent()
    {
        Scene source = CreateAnimationScene();
        Scene loaded = Read(Write(source, "sample.wiltanim"), "sample.wiltanim");

        SkeletonAnimation		skeletal	= Assert.Single(loaded.GetDescendants<SkeletonAnimation>());
        SkeletonAnimationTrack	track		= Assert.Single(skeletal.Tracks);

        Assert.Equal("walk", skeletal.Name);
        Assert.Equal(60, skeletal.Framerate);
        Assert.Equal(TransformType.Additive, skeletal.TransformType);
        Assert.Equal(TransformType.Additive, track.TransformType);
        Assert.Equal([0, 10], Assert.IsType<DataBuffer<ushort>>(track.TranslationCurve!.Keys).ToArray());
        Assert.Equal(new Vector3(1, 2, 3), track.TranslationCurve.SampleVector3(10));
        Assert.NotNull(track.RotationCurve);
        Assert.Null(track.ScaleCurve);
        Assert.Equal(1, track.CustomCurves!["visibility"].ComponentCount);

        MorphAnimation morph = Assert.Single(loaded.GetDescendants<MorphAnimation>());

        Assert.Equal(0.5f, Assert.Single(morph.Tracks).SampleWeight(0));

        AnimationAction action = Assert.Single(skeletal.Actions!);

        Assert.Equal("fire", action.Name);
        Assert.Equal("left_hand", action.KeyFrames[0].Value);
        Assert.Equal(3, action.KeyFrames[1].Value);
        Assert.Null(action.KeyFrames[2].Value);
    }

    [Theory]
    [InlineData("sample.wiltmodel")]
    [InlineData("sample.wiltanim")]
    public void WiltTranslators_RewriteLoadedScene_ProducesIdenticalBytes(string filePath)
    {
        Scene	source		= filePath.EndsWith(".wiltmodel") ? CreateModelScene() : CreateAnimationScene();
        byte[]	firstWrite	= Write(source, filePath);
        byte[]	secondWrite	= Write(Read(firstWrite, filePath), filePath);

        Assert.Equal(firstWrite, secondWrite);
    }

    [Fact]
    public void WiltModelTranslator_UnsupportedAttribute_Throws()
    {
        Scene source = CreateModelScene();

        source.Attributes!["id"] = Guid.NewGuid();

        Assert.Throws<InvalidDataException>(() => Write(source, "sample.wiltmodel"));
    }

    private static Scene CreateModelScene()
    {
        Scene scene = new("source")
        {
            UpAxis		= SceneUpAxis.Z,
            Attributes	= new() { ["source_game"] = "Test Game" },
        };

        Skeleton		skeleton	= scene.AddNode(new Skeleton("skeleton"));
        SkeletonBone	root		= skeleton.AddNode(new SkeletonBone("root"));
        SkeletonBone	child		= root.AddNode(new SkeletonBone("child"));

        child.BindTransform.LocalPosition = new Vector3(0, 1, 0);

        Material material = scene.AddNode(new Material("material"));

        material.DiffuseMapName	= "diffuse";
        material.DiffuseColor	= new Vector4(1, 0.5f, 0.25f, 1);
        material.DoubleSided	= true;
        material.Attributes		= new() { ["custom_scale"] = 2.5f };
        material.Connect("diffuse", material.AddNode(new Texture("textures/diffuse.png")));

        Mesh mesh = scene.AddNode<Mesh>("mesh");

        mesh.Positions				= new DataBuffer<float>(new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }, 1, 3);
        mesh.Normals				= new DataBuffer<Half>([.. Enumerable.Repeat((Half)0, 6), (Half)1, (Half)1, (Half)1], 1, 3);
        mesh.UVLayers				= new DataBuffer<float>(new float[12], 2, 2);
        mesh.ColorLayers			= new DataBuffer<byte>(new byte[12], 1, 4);
        mesh.FaceIndices			= new DataBuffer<ushort>(new ushort[] { 0, 1, 2 });
        mesh.Materials				= [material];
        mesh.MaterialIndexRanges	= [(0, 3)];
        mesh.Skin					= new Skin([root, child], new DataBuffer<byte>(new byte[] { 0, 1, 0, 1, 1, 0 }, 2, 1), new DataBuffer<float>([0.5f, 0.5f, 1, 0, 1, 0], 2, 1));
        mesh.Morph					= new Morph(["smile"], new DataBuffer<float>(new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 1 }, 1, 3));
        mesh.Morph.Weights[0]		= 0.25f;

        Hair hair = scene.AddNode<Hair>("hair");

        hair.StrandSegments	= [2];
        hair.Particles		= new DataBuffer<float>(new float[9], 1, 3);
        hair.Material		= material;

        scene.AddNode(new IKHandleNode("ik", root, child) { TargetNode = child, Weight = 0.5f });
        scene.AddNode(new OrientConstraintNode("orient", child, root) { RotationOffset = Quaternion.CreateFromYawPitchRoll(0.5f, 0, 0), SkipY = true });

        return scene;
    }

    private static Scene CreateAnimationScene()
    {
        Scene				scene		= new("source");
        SkeletonAnimation	skeletal	= scene.AddNode(new SkeletonAnimation("walk") { Framerate = 60, TransformType = TransformType.Additive });
        MorphAnimation		morph		= scene.AddNode(new MorphAnimation("walk_Morph"));

        SkeletonAnimationTrack track = new("root")
        {
            TransformType		= TransformType.Additive,
            TranslationCurve	= new AnimationCurve(TransformSpace.Local, TransformType.Additive)
            {
                Keys	= new DataBuffer<ushort>(new ushort[] { 0, 10 }),
                Values	= new DataBuffer<float>(new float[] { 0, 0, 0, 1, 2, 3 }, 1, 3),
            },
        };

        track.AddRotationFrame(0, Quaternion.Identity);
        track.AddRotationFrame(10, Quaternion.CreateFromYawPitchRoll(1, 0, 0));
        track.GetOrCreateCustomCurve("visibility", 1).Add(5, 1f);
        skeletal.Tracks.Add(track);

        morph.GetOrCreateTrack("smile").AddWeightFrame(0, 0.5f);

        skeletal.CreateAction("fire").KeyFrames.AddRange([new(5, "left_hand"), new(8, 3), new(12, null)]);

        return scene;
    }

    private static byte[] Write(Scene scene, string filePath)
    {
        using MemoryStream stream = new();

        SceneTranslatorManager manager = new();
        manager.Register(new WiltModelTranslator());
        manager.Register(new WiltAnimTranslator());
        manager.Write(stream, filePath, scene, new SceneTranslatorOptions());

        return stream.ToArray();
    }

    private static Scene Read(byte[] data, string filePath)
    {
        using MemoryStream stream = new(data, false);

        SceneTranslatorManager manager = new();
        manager.Register(new WiltModelTranslator());
        manager.Register(new WiltAnimTranslator());

        return manager.Read(stream, filePath, new SceneTranslatorOptions());
    }
}
