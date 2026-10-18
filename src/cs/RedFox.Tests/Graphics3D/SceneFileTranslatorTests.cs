using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Buffers.PackedVector;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.IO.SceneFile;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneFileTranslatorTests
{
    [Fact]
    public void SceneFileTranslator_RoundTrip_PreservesSceneContent()
    {
        Scene source = new("source")
        {
            UpAxis = SceneUpAxis.Z,
        };

        Skeleton		skeleton	= source.RootNode.AddNode(new Skeleton("skeleton"));
        SkeletonBone	root		= skeleton.AddNode(new SkeletonBone("root"));
        SkeletonBone	child		= root.AddNode(new SkeletonBone("child"));

        root.BindTransform.LocalPosition	= new Vector3(1f, 2f, 3f);
        root.BindTransform.LocalRotation	= Quaternion.CreateFromYawPitchRoll(0.5f, 0.25f, 0.125f);
        child.BindTransform.WorldPosition	= new Vector3(0f, 5f, 0f);
        child.BindTransform.Scale			= new Vector3(2f, 2f, 2f);
        child.RenderBoneOnTop				= false;

        Texture texture = source.RootNode.AddNode(new Texture("textures/diffuse.png"));
        Material material = source.RootNode.AddNode(new Material("material")
        {
            DiffuseMapName	= "diffuse",
            DiffuseColor	= new Vector4(0.1f, 0.2f, 0.3f, 1f),
            Roughness		= 0.75f,
            Metallic		= null,
            DoubleSided		= true,
            Type			= "pbr",
        });
        material.Connect("diffuse", texture);

        Guid id = Guid.NewGuid();
        Mesh mesh = source.RootNode.AddNode(new Mesh
        {
            Name				= "mesh",
            Positions			= new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3),
            UVLayers			= new DataBuffer<float>([0f, 0f, 1f, 1f, 1f, 0f, 0f, 1f, 0f, 1f, 1f, 0f], 2, 2),
            ColorLayers			= new DataBuffer<byte>([255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255], 1, 4),
            FaceIndices			= new DataBuffer<ushort>(new ushort[] { 0, 1, 2 }, 1, 1),
            Materials			= [material],
            MaterialIndexRanges	= [(0, 3)],
            Attributes			= new() { ["count"] = 7, ["offset"] = new Vector3(1f, 2f, 3f), ["id"] = id },
        });

        mesh.Skin	= new Skin([root, child], new DataBuffer<byte>([0, 1, 0, 1, 1, 0], 2, 1), new DataBuffer<float>([0.75f, 0.25f, 0.5f, 0.5f, 1f, 0f], 2, 1)) { Name = "skin" };
        mesh.Morph	= new Morph(["smile"], new DataBuffer<float>([0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f], 1, 3));
        mesh.Morph.Weights[0] = 0.5f;

        source.RootNode.AddNode(new Light("light") { Color = new Vector3(1f, 0.5f, 0.25f), Intensity = 3f });
        source.RootNode.AddNode(new Camera("camera") { FieldOfView = 45f, Projection = CameraProjection.Orthographic });
        source.RootNode.AddNode(new ParentConstraintNode("constraint", child, root) { Weight = 0.25f, TranslationOffset = Vector3.One });
        source.RootNode.AddNode(new IKHandleNode("ik", root, child) { TargetNode = mesh, Weight = 0.5f });

        SkeletonAnimation skeletonAnimation = source.RootNode.AddNode(new SkeletonAnimation("walk") { Framerate = 60f });
        SkeletonAnimationTrack track = new("root");
        track.AddTranslationFrame(0f, Vector3.Zero);
        track.AddTranslationFrame(1f, Vector3.One);
        track.AddRotationFrame(0f, Quaternion.Identity);
        skeletonAnimation.Tracks.Add(track);
        skeletonAnimation.CreateAction("footstep").KeyFrames.Add(new(12f, 42));

        MorphAnimation morphAnimation = source.RootNode.AddNode(new MorphAnimation("blink"));
        morphAnimation.GetOrCreateTrack("smile").AddWeightFrame(0.5f, 1f);

        Scene loaded = RoundTrip(source, new SceneFileTranslator(), new SceneFileTranslator());

        Assert.Equal(SceneUpAxis.Z, loaded.UpAxis);

        SkeletonBone loadedRoot		= loaded.FindDescendant<SkeletonBone>("root");
        SkeletonBone loadedChild	= loaded.FindDescendant<SkeletonBone>("child");

        Assert.IsType<Skeleton>(loadedRoot.Parent);
        Assert.Same(loadedRoot, loadedChild.Parent);
        Assert.Equal(root.BindTransform.LocalPosition, loadedRoot.BindTransform.LocalPosition);
        Assert.Equal(root.BindTransform.LocalRotation, loadedRoot.BindTransform.LocalRotation);
        Assert.Null(loadedRoot.BindTransform.WorldPosition);
        Assert.Equal(child.BindTransform.WorldPosition, loadedChild.BindTransform.WorldPosition);
        Assert.Equal(child.BindTransform.Scale, loadedChild.BindTransform.Scale);
        Assert.False(loadedChild.RenderBoneOnTop);

        Material loadedMaterial = loaded.FindDescendant<Material>("material");
        Assert.Equal("diffuse", loadedMaterial.DiffuseMapName);
        Assert.Equal(material.DiffuseColor, loadedMaterial.DiffuseColor);
        Assert.Equal(0.75f, loadedMaterial.Roughness);
        Assert.Null(loadedMaterial.Metallic);
        Assert.True(loadedMaterial.DoubleSided);
        Assert.Equal("pbr", loadedMaterial.Type);
        Assert.Equal("textures/diffuse.png", loadedMaterial.GetDiffuseMap().FilePath);

        Mesh loadedMesh = loaded.FindDescendant<Mesh>("mesh");
        Assert.Equal(mesh.Positions!.AsReadOnlySpan<float>().ToArray(), Assert.IsType<DataBuffer<float>>(loadedMesh.Positions).ToArray());
        Assert.Equal(2, loadedMesh.UVLayerCount);
        Assert.Equal(mesh.UVLayers!.AsReadOnlySpan<float>().ToArray(), loadedMesh.UVLayers!.AsReadOnlySpan<float>().ToArray());
        Assert.Equal([255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255], Assert.IsType<DataBuffer<byte>>(loadedMesh.ColorLayers).ToArray());
        Assert.Equal([0, 1, 2], Assert.IsType<DataBuffer<ushort>>(loadedMesh.FaceIndices).ToArray());
        Assert.Same(loadedMaterial, Assert.Single(loadedMesh.Materials!));
        Assert.Equal((0, 3), Assert.Single(loadedMesh.MaterialIndexRanges!));
        Assert.Equal(7, loadedMesh.Attributes!["count"]);
        Assert.Equal(new Vector3(1f, 2f, 3f), loadedMesh.Attributes["offset"]);
        Assert.Equal(id.ToString(), loadedMesh.Attributes["id"]);

        Skin loadedSkin = loadedMesh.Skin!;
        Assert.Equal("skin", loadedSkin.Name);
        Assert.Equal([loadedRoot, loadedChild], loadedSkin.Bones);
        Assert.Equal(0.25f, loadedSkin.BoneWeights.Get<float>(0, 1, 0));
        Assert.Equal(1, loadedSkin.GetBoneIndex(2, 0));

        Morph loadedMorph = loadedMesh.Morph!;
        Assert.Equal(["smile"], loadedMorph.TargetNames);
        Assert.Equal(0.5f, loadedMorph.Weights[0]);
        Assert.Equal(Vector3.UnitY, loadedMorph.DeltaPositions!.GetVector3(2, 0));

        Light loadedLight = loaded.FindDescendant<Light>("light");
        Assert.Equal(new Vector3(1f, 0.5f, 0.25f), loadedLight.Color);
        Assert.Equal(3f, loadedLight.Intensity);

        Camera loadedCamera = loaded.FindDescendant<Camera>("camera");
        Assert.Equal(45f, loadedCamera.FieldOfView);
        Assert.Equal(CameraProjection.Orthographic, loadedCamera.Projection);

        ParentConstraintNode loadedConstraint = loaded.FindDescendant<ParentConstraintNode>("constraint");
        Assert.Same(loadedChild, loadedConstraint.ConstrainedNode);
        Assert.Same(loadedRoot, loadedConstraint.SourceNode);
        Assert.Equal(0.25f, loadedConstraint.Weight);
        Assert.Equal(Vector3.One, loadedConstraint.TranslationOffset);

        IKHandleNode loadedIK = loaded.FindDescendant<IKHandleNode>("ik");
        Assert.Same(loadedRoot, loadedIK.StartNode);
        Assert.Same(loadedChild, loadedIK.EndNode);
        Assert.Same(loadedMesh, loadedIK.TargetNode);
        Assert.Null(loadedIK.PoleNode);

        SkeletonAnimation loadedAnimation = loaded.FindDescendant<SkeletonAnimation>("walk");
        SkeletonAnimationTrack loadedTrack = Assert.Single(loadedAnimation.Tracks);
        Assert.Equal(60f, loadedAnimation.Framerate);
        Assert.Equal("root", loadedTrack.Name);
        Assert.Equal(Vector3.One, loadedTrack.TranslationCurve!.GetVector3(1));
        Assert.Equal(1f, loadedTrack.TranslationCurve.GetKeyTime(1));
        Assert.Equal(Quaternion.Identity, loadedTrack.RotationCurve!.GetQuaternion(0));
        Assert.Null(loadedTrack.ScaleCurve);

        AnimationKeyFrame<float, object?> loadedKeyFrame = Assert.Single(Assert.Single(loadedAnimation.Actions!).KeyFrames);
        Assert.Equal(12f, loadedKeyFrame.Frame);
        Assert.Equal("42", loadedKeyFrame.Value);

        MorphAnimationTrack loadedMorphTrack = Assert.Single(loaded.FindDescendant<MorphAnimation>("blink").Tracks);
        Assert.Equal("smile", loadedMorphTrack.Name);
        Assert.Equal(1f, loadedMorphTrack.SampleWeight(0.5f));
    }

    [Fact]
    public void SceneFileTranslator_UnknownNodeType_LoadsAsGroupAndSkipsItsData()
    {
        Scene source = new("source");
        Light light = source.RootNode.AddNode(new Light("light") { Intensity = 4f });
        light.BindTransform.LocalPosition = new Vector3(1f, 2f, 3f);
        light.AddNode(new SkeletonBone("bone"));
        source.RootNode.AddNode(new Camera("camera") { FieldOfView = 30f });

        SceneFileTranslator writer = new();
        writer.RegisterNodeType("FutureLight", () => new Light());

        Scene loaded = RoundTrip(source, writer, new SceneFileTranslator());

        Group unknown = loaded.FindDescendant<Group>("light");
        Assert.Equal(new Vector3(1f, 2f, 3f), unknown.BindTransform.LocalPosition);
        Assert.Same(unknown, loaded.FindDescendant<SkeletonBone>("bone").Parent);
        Assert.Equal(30f, loaded.FindDescendant<Camera>("camera").FieldOfView);
    }

    [Fact]
    public void SceneFileTranslator_PackedBuffer_IsWrittenAsFloats()
    {
        PackedBuffer<Half2> packed = new(2);
        packed.Add(new Vector2(0.5f, 1.5f));
        packed.Add(new Vector2(-2f, 4f));

        Scene source = new("source");
        source.RootNode.AddNode(new Mesh { Name = "mesh", UVLayers = packed });

        Scene loaded = RoundTrip(source, new SceneFileTranslator(), new SceneFileTranslator());

        DataBuffer<float> uvs = Assert.IsType<DataBuffer<float>>(loaded.FindDescendant<Mesh>("mesh").UVLayers);
        Assert.Equal([0.5f, 1.5f, -2f, 4f], uvs.ToArray());
        Assert.Equal(2, uvs.ComponentCount);
    }

    private static Scene RoundTrip(Scene source, SceneFileTranslator writer, SceneFileTranslator reader)
    {
        using MemoryStream stream = new();

        SceneTranslatorManager writeManager = new();
        writeManager.Register(writer);
        writeManager.Write(stream, "sample.rfscene", source, new SceneTranslatorOptions());

        stream.Position = 0;

        SceneTranslatorManager readManager = new();
        readManager.Register(reader);
        return readManager.Read(stream, "sample.rfscene", new SceneTranslatorOptions());
    }
}
