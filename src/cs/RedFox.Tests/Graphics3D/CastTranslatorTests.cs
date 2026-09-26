using System.Numerics;
using CastNet;
using CastNet.Nodes;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
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

    [Fact]
    public void CastTranslator_RoundTrip_PreservesMeshData()
    {
        Scene source = new("mesh");
        MeshGroup model = source.RootNode.AddNode(new MeshGroup { Name = "Model" });
        SkeletonBone rootBone = model.AddNode(new SkeletonBone("root"));
        SkeletonBone childBone = rootBone.AddNode(new SkeletonBone("child"));
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });

        rootBone.BindTransform.Scale = new Vector3(2f);
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.Normals = new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f], 1, 3);
        mesh.UVLayers = new DataBuffer<float>([0f, 0f, 1f, 1f, 1f, 0f, 0f, 1f, 0f, 1f, 1f, 0f], 2, 2);
        mesh.ColorLayers = new DataBuffer<float>([1f, 0f, 0f, 1f, 0f, 1f, 0f, 1f, 0f, 0f, 1f, 1f], 1, 4);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        mesh.Skin = new Skin([rootBone, childBone], new DataBuffer<int>([0, 1, 1, 0, 0, 1], 2, 1), new DataBuffer<float>([0.75f, 0.25f, 1f, 0f, 0.5f, 0.5f], 2, 1));

        Scene loaded = RoundTrip(source, "mesh.cast");
        Mesh loadedMesh = Assert.Single(loaded.GetDescendants<Mesh>());

        Assert.Equal(new Vector3(0f, 1f, 0f), loadedMesh.Positions!.GetVector3(2, 0));
        Assert.Equal(new Vector3(0f, 0f, 1f), loadedMesh.Normals!.GetVector3(1, 0));
        Assert.Equal(2, loadedMesh.UVLayerCount);
        Assert.Equal(new Vector2(0f, 1f), loadedMesh.UVLayers!.GetVector2(1, 1));
        Assert.Equal(new Vector4(0f, 1f, 0f, 1f), loadedMesh.ColorLayers!.GetVector4(1, 0));
        Assert.Equal(2, loadedMesh.FaceIndices!.Get<int>(2, 0, 0));
        Assert.Equal(["root", "child"], loadedMesh.Skin!.Bones.Select(static bone => bone.Name));
        Assert.Equal(1, loadedMesh.Skin.BoneIndices.Get<int>(0, 1, 0));
        Assert.Equal(0.75f, loadedMesh.Skin.BoneWeights.Get<float>(0, 0, 0));
        Assert.Equal(new Vector3(2f), loaded.EnumerateHierarchy<SkeletonBone>().Single(static bone => bone.Name == "root").BindTransform.Scale);
    }

    [Fact]
    public void CastTranslator_RoundTrip_MapsMaterialSlots()
    {
        Scene source = new("material");
        MeshGroup model = source.RootNode.AddNode(new MeshGroup { Name = "Model" });
        Material material = model.AddNode(new Material("metal"));
        Texture albedo = material.AddNode(new Texture("albedo.png"));
        Texture metallic = material.AddNode(new Texture("metal.png"));
        Texture detail = material.AddNode(new Texture("detail.png"));

        material.Connect("albedo", albedo);
        material.DiffuseMapName = "albedo";
        material.Connect("metallicMap", metallic);
        material.MetallicMapName = "metallicMap";
        material.Connect("detail", detail);
        material.RoughnessColor = new Vector4(0.5f);

        MaterialNode materialNode = Write(source).Roots[0].Models[0].Materials[0];

        Assert.Equal(["albedo", "metal", "extra0", "roughness"], materialNode.EnumerateSlots().Select(static slot => slot.Key));

        Material loaded = Assert.Single(RoundTrip(source, "material.cast").GetDescendants<Material>());

        Assert.Equal("albedo", loaded.DiffuseMapName);
        Assert.Equal("metal", loaded.MetallicMapName);
        Assert.Equal("metal.png", loaded.MetallicMap!.FilePath);
        Assert.True(loaded.TryGetTexture("extra0", out _));
        Assert.Equal(new Vector4(0.5f), loaded.RoughnessColor);
    }

    [Fact]
    public void CastTranslator_RoundTrip_PreservesCurveModesAndKeys()
    {
        Scene source = new("animation");
        SkeletonAnimation animation = source.RootNode.AddNode(new SkeletonAnimation("walk"));
        SkeletonAnimationTrack track = new("root");

        track.RotationCurve = AnimationCurve.CreateQuaternion(TransformSpace.Local, TransformType.Absolute);
        track.RotationCurve.Add(0f, Quaternion.Identity);
        track.RotationCurve.Add(10f, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1f));
        track.TranslationCurve = AnimationCurve.CreateVector3(TransformSpace.Local, TransformType.Additive);
        track.TranslationCurve.Add(0f, Vector3.Zero);
        track.TranslationCurve.Add(2.6f, Vector3.One);
        track.GetOrCreateCustomCurve("visibility", 1).Add(0f, 1f);
        track.GetOrCreateCustomCurve("visibility", 1).Add(5f, 0f);
        animation.Tracks.Add(track);

        CurveNode rotationNode = Write(source).Roots[0].Animations[0].Curves.Single(static curve => curve.KeyPropertyName == "rq");

        Assert.Equal(CastPropertyType.Byte, rotationNode.KeyFrames!.Type);

        SkeletonAnimationTrack loaded = Assert.Single(Assert.Single(RoundTrip(source, "animation.cast").GetDescendants<SkeletonAnimation>()).Tracks);

        Assert.Equal(TransformType.Absolute, loaded.RotationCurve!.TransformType);
        Assert.Equal(TransformType.Additive, loaded.TranslationCurve!.TransformType);
        Assert.Equal(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1f), loaded.RotationCurve.GetQuaternion(1));
        Assert.Equal(3f, loaded.TranslationCurve.GetKeyTime(1));
        Assert.Equal(0f, loaded.CustomCurves!["visibility"].GetScalar(1));
    }

    [Fact]
    public void CastTranslator_Read_MergesAxesWithDifferentKeys()
    {
        RootNode root = new();
        AnimationNode animation = root.AddNode(new AnimationNode { Framerate = 30f });

        root.AddNode(new MetadataNode { UpAxis = "z" });
        animation.AddNode(new CurveNode { NodeName = "bone", KeyPropertyName = "tx", Mode = "relative", KeyFrames = CastArrayProperty.CreateIndices<int>([0, 10]), KeyValues = CastArrayProperty.Create<float>([0f, 10f]) });
        animation.AddNode(new CurveNode { NodeName = "bone", KeyPropertyName = "ty", Mode = "relative", KeyFrames = CastArrayProperty.CreateIndices<int>([0, 5, 10]), KeyValues = CastArrayProperty.Create<float>([1f, 2f, 3f]) });

        Scene loaded = Read(root);
        AnimationCurve translation = Assert.Single(Assert.Single(loaded.GetDescendants<SkeletonAnimation>()).Tracks).TranslationCurve!;

        Assert.Equal(SceneUpAxis.Z, loaded.UpAxis);
        Assert.Equal([0f, 5f, 10f], Enumerable.Range(0, translation.KeyFrameCount).Select(translation.GetKeyTime));
        Assert.Equal(new Vector3(5f, 2f, 0f), translation.Values!.GetVector3(1, 0));
    }

    [Fact]
    public void CastTranslator_Read_AppliesCurveModeOverridesToDescendants()
    {
        RootNode root = new();
        SkeletonNode skeleton = root.AddNode<ModelNode>().AddNode<SkeletonNode>();
        AnimationNode animation = root.AddNode(new AnimationNode { Framerate = 30f });

        skeleton.AddNode(new BoneNode { Name = "tag_origin" });
        skeleton.AddNode(new BoneNode { Name = "j_spine", ParentIndex = 0 });
        animation.AddNode(new CurveNode { NodeName = "j_spine", KeyPropertyName = "rq", Mode = "relative", KeyFrames = CastArrayProperty.CreateIndices<int>([0]), KeyValues = CastArrayProperty.Create(Quaternion.Identity) });
        animation.AddNode(new CurveNode { NodeName = "j_spine", KeyPropertyName = "tx", Mode = "relative", KeyFrames = CastArrayProperty.CreateIndices<int>([0]), KeyValues = CastArrayProperty.Create(1f) });
        animation.AddNode(new CurveModeOverrideNode { NodeName = "tag_origin", Mode = "absolute", OverrideRotationCurves = true });

        SkeletonAnimationTrack track = Assert.Single(Assert.Single(Read(root).GetDescendants<SkeletonAnimation>()).Tracks);

        Assert.Equal(TransformType.Absolute, track.RotationCurve!.TransformType);
        Assert.Equal(TransformType.Relative, track.TranslationCurve!.TransformType);
    }

    [Fact]
    public void CastTranslator_RoundTrip_PreservesHair()
    {
        Scene source = new("hair");
        MeshGroup model = source.RootNode.AddNode(new MeshGroup { Name = "Model" });
        Material material = model.AddNode(new Material("hair_material"));
        Hair hair = model.AddNode(new Hair { StrandSegments = [1, 2], Particles = new DataBuffer<float>([0f, 0f, 0f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 0f, 1f, 1f, 0f, 2f], 1, 3), Material = material });

        Hair loaded = Assert.Single(RoundTrip(source, "hair.cast").GetDescendants<Hair>());

        Assert.Equal(hair.StrandSegments, loaded.StrandSegments);
        Assert.Equal(new Vector3(1f, 0f, 2f), loaded.Particles!.GetVector3(4, 0));
        Assert.Equal("hair_material", loaded.Material!.Name);
    }

    [Fact]
    public void CastTranslator_Read_ResolvesInstancesFromSceneRoot()
    {
        RootNode root = new();
        string sceneRoot = Path.Combine(Path.GetTempPath(), "cast_scene");

        root.AddNode(new MetadataNode { SceneRoot = sceneRoot });
        root.AddNode(new InstanceNode { Name = "crate", Position = new Vector3(1f, 2f, 3f), Rotation = Quaternion.Identity, Scale = new Vector3(2f) }).ReferenceFile = new FileNode { Path = "props/crate.cast" };

        SceneReference reference = Assert.Single(Read(root).GetDescendants<SceneReference>());

        Assert.Equal("crate", reference.Name);
        Assert.Equal("props/crate.cast", reference.FilePath);
        Assert.Equal(Path.GetFullPath(Path.Combine(sceneRoot, "props/crate.cast")), reference.ResolvedFilePath);
        Assert.Equal(new Vector3(1f, 2f, 3f), reference.BindTransform.LocalPosition);
        Assert.Equal(new Vector3(2f), reference.BindTransform.Scale);
    }

    [Fact]
    public void CastTranslator_Read_CreatesConstraintsAndIKHandles()
    {
        RootNode root = new();
        SkeletonNode skeleton = root.AddNode<ModelNode>().AddNode<SkeletonNode>();
        BoneNode origin = skeleton.AddNode(new BoneNode { Name = "origin", LocalPosition = Vector3.Zero });
        BoneNode upper = skeleton.AddNode(new BoneNode { Name = "upper", ParentIndex = 0, LocalPosition = new Vector3(0f, 0f, 2f) });
        BoneNode lower = skeleton.AddNode(new BoneNode { Name = "lower", ParentIndex = 1, LocalPosition = new Vector3(0f, 0f, 2f) });
        BoneNode target = skeleton.AddNode(new BoneNode { Name = "target", ParentIndex = 0, LocalPosition = new Vector3(3f, 0f, 0f), Scale = new Vector3(2f) });

        skeleton.AddNode(new CastNet.Nodes.IKHandleNode { Name = "arm_ik", StartBone = origin, EndBone = lower, TargetBone = target, PoleVectorBone = upper, TargetOffset = Vector3.UnitY });
        skeleton.AddNode(new CastNet.Nodes.ConstraintNode { ConstraintType = "pt", ConstraintBone = lower, TargetBone = target, MaintainOffset = true, SkipY = true, Weight = 0.5f });
        skeleton.AddNode(new CastNet.Nodes.ConstraintNode { ConstraintType = "or", ConstraintBone = lower, TargetBone = target, CustomOffset = Vector4.UnitW });
        skeleton.AddNode(new CastNet.Nodes.ConstraintNode { ConstraintType = "sc", ConstraintBone = upper, TargetBone = target, MaintainOffset = true });

        Scene loaded = Read(root);
        RedFox.Graphics3D.IKHandleNode handle = Assert.Single(loaded.GetDescendants<RedFox.Graphics3D.IKHandleNode>());
        PointConstraintNode point = Assert.Single(loaded.GetDescendants<PointConstraintNode>());

        Assert.Equal(["origin", "upper", "lower"], handle.GetChain().Select(static node => node.Name));
        Assert.Equal("target", handle.TargetNode!.Name);
        Assert.Equal("upper", handle.PoleVectorNode!.Name);
        Assert.Equal(Vector3.UnitY, handle.TargetOffset);
        Assert.Equal(new Vector3(-3f, 0f, 4f), point.TranslationOffset);
        Assert.True(point.SkipY);
        Assert.Equal(0.5f, point.Weight);
        Assert.Equal(Quaternion.Identity, Assert.Single(loaded.GetDescendants<OrientConstraintNode>()).RotationOffset);
        Assert.Equal(new Vector3(0.5f), Assert.Single(loaded.GetDescendants<ScaleConstraintNode>()).ScaleOffset);
    }

    [Fact]
    public void CastTranslator_Write_ExportsIKAndSplitsParentConstraints()
    {
        Scene source = new("rig");
        MeshGroup model = source.RootNode.AddNode(new MeshGroup { Name = "Model" });
        SkeletonBone root = model.AddNode(new SkeletonBone("root"));
        SkeletonBone mid = root.AddNode(new SkeletonBone("mid"));
        SkeletonBone tip = mid.AddNode(new SkeletonBone("tip"));

        model.AddNode(new RedFox.Graphics3D.IKHandleNode("ik", root, tip) { TargetNode = mid, UseTargetRotation = true });
        model.AddNode(new ParentConstraintNode("parent", tip, root) { TranslationOffset = Vector3.UnitX, Weight = 0.25f });

        SkeletonNode skeleton = Write(source).Roots[0].Models[0].Skeleton!;
        CastNet.Nodes.IKHandleNode handle = Assert.Single(skeleton.IKHandles);

        Assert.Equal("root", handle.StartBone!.Name);
        Assert.Equal("tip", handle.EndBone!.Name);
        Assert.True(handle.UseTargetRotation);
        Assert.Equal(["pt", "or"], skeleton.Constraints.Select(static constraint => constraint.ConstraintType));
        Assert.Equal(new Vector4(1f, 0f, 0f, 0f), skeleton.Constraints[0].CustomOffset);
        Assert.All(skeleton.Constraints, static constraint => Assert.Equal(0.25f, constraint.Weight));
    }

    [Fact]
    public void CastTranslator_RoundTrip_PreservesSkinningModeAndBlendWeight()
    {
        Scene source = new("skinning");
        MeshGroup model = source.RootNode.AddNode(new MeshGroup { Name = "Model" });
        SkeletonBone bone = model.AddNode(new SkeletonBone("bone"));
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });
        SkeletonAnimation animation = source.RootNode.AddNode(new SkeletonAnimation("additive"));
        SkeletonAnimationTrack track = new("bone");

        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.Skin = new Skin([bone], new DataBuffer<int>([0], 1, 1), new DataBuffer<float>([1f], 1, 1)) { SkinningMode = SkinningMode.DualQuaternion };
        track.TranslationCurve = AnimationCurve.CreateVector3(TransformSpace.Local, TransformType.Additive);
        track.TranslationCurve.Add(0f, Vector3.One);
        track.TranslationCurve.BlendWeight = 0.5f;
        animation.Tracks.Add(track);

        Scene loaded = RoundTrip(source, "skinning.cast");

        Assert.Equal(SkinningMode.DualQuaternion, Assert.Single(loaded.GetDescendants<Mesh>()).Skin!.SkinningMode);
        Assert.Equal(0.5f, Assert.Single(Assert.Single(loaded.GetDescendants<SkeletonAnimation>()).Tracks).TranslationCurve!.BlendWeight);
    }

    private static Scene RoundTrip(Scene source, string fileName)
    {
        SceneTranslatorManager manager = new();
        manager.Register(new CastTranslator());

        using MemoryStream stream = new();
        manager.Write(stream, fileName, source, new SceneTranslatorOptions());
        stream.Position = 0;

        return manager.Read(stream, fileName, new SceneTranslatorOptions());
    }

    private static Cast Write(Scene source)
    {
        SceneTranslatorManager manager = new();
        manager.Register(new CastTranslator());

        using MemoryStream stream = new();
        manager.Write(stream, "inspect.cast", source, new SceneTranslatorOptions());
        stream.Position = 0;

        return CastReader.Load(stream);
    }

    private static Scene Read(RootNode root)
    {
        SceneTranslatorManager manager = new();
        manager.Register(new CastTranslator());

        using MemoryStream stream = new();
        CastWriter.Save(stream, root);
        stream.Position = 0;

        return manager.Read(stream, "read.cast", new SceneTranslatorOptions());
    }
}
