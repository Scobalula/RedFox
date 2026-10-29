using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Formats.ActorX;
using RedFox.Graphics3D.Formats.BiovisionHierarchy;
using RedFox.Graphics3D.Formats.Cast;
using RedFox.Graphics3D.Formats.GLTransmissionFormat;
using RedFox.Graphics3D.Formats.IdTech;
using RedFox.Graphics3D.Formats.IwEngine;
using RedFox.Graphics3D.Formats.MayaAscii;
using RedFox.Graphics3D.Formats.SEAnim;
using RedFox.Graphics3D.Formats.SEModel;
using RedFox.Graphics3D.Formats.StudioMDL;
using RedFox.Graphics3D.Formats.WavefrontObj;
using RedFox.Graphics3D.Formats.Wilt;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.IO.SceneFile;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Tests.Graphics3D;

public sealed class TranslatorRegressionTests
{
    [Theory]
    [InlineData("point")]
    [InlineData("orient")]
    [InlineData("parent")]
    [InlineData("scale")]
    [InlineData("ik")]
    public void SceneFile_RejectsMissingRequiredReferences(string kind)
    {
        Group source	= new("source");
        Group target	= new("target");
        SceneNode node	= kind switch
        {
            "point" => new PointConstraintNode("constraint", target, source),
            "orient" => new OrientConstraintNode("constraint", target, source),
            "parent" => new ParentConstraintNode("constraint", target, source),
            "scale" => new ScaleConstraintNode("constraint", target, source),
            "ik" => new IKHandleNode("handle", source, target),
            _ => throw new ArgumentException(kind),
        };
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        node.Write(writer, new Dictionary<SceneNode, int>());
        stream.Position = 0;
        using BinaryReader reader = new(stream, Encoding.UTF8, true);
        Assert.Throws<InvalidDataException>(() => node.Read(reader, []));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SceneFile_RejectsTruncatedTracksAtRecordBoundary(bool morph)
    {
        Scene scene = new();
        scene.AddNode(morph ? (Animation)new MorphAnimation("clip") : new SkeletonAnimation("clip"));
        byte[] data = Write(new SceneFileTranslator(), scene, ".rfscene");

        using MemoryStream input = new(data);
        using BinaryReader reader = new(input);
        reader.ReadUInt32();
        reader.ReadUInt16();
        reader.ReadUInt16();
        int nodeCount = reader.ReadInt32();
        for (int i = 0; i < nodeCount; i++)
        {
            reader.ReadString();
            reader.ReadInt32();
        }

        int sceneSize = reader.ReadInt32();
        input.Position += sceneSize;
        int recordSizeOffset	= checked((int)input.Position);
        int recordSize			= reader.ReadInt32();

        byte[] truncated = new byte[data.Length + sizeof(byte)];
        data.CopyTo(truncated, 0);
        BinaryPrimitives.WriteInt32LittleEndian(truncated.AsSpan(data.Length - sizeof(int)), 1);
        BinaryPrimitives.WriteInt32LittleEndian(truncated.AsSpan(recordSizeOffset), recordSize + sizeof(byte));
        Assert.Throws<EndOfStreamException>(() => Read(new SceneFileTranslator(), truncated, ".rfscene"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SceneFile_RejectsOversizedTrackCountsBeforeAllocation(bool morph)
    {
        Scene scene = new();
        scene.AddNode(morph ? (Animation)new MorphAnimation("clip") : new SkeletonAnimation("clip"));
        byte[] data = Write(new SceneFileTranslator(), scene, ".rfscene");
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(data.Length - sizeof(int)), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => Read(new SceneFileTranslator(), data, ".rfscene"));
    }

    [Fact]
    public void Gltf_RebasesAnimationAcrossFilteredBones()
    {
        Scene scene			= new();
        SkeletonBone root	= scene.AddNode(new SkeletonBone("root") { Flags = SceneNodeFlags.Selected });
        SkeletonBone middle	= root.AddNode(new SkeletonBone("middle"));
        SkeletonBone tip	= middle.AddNode(new SkeletonBone("tip") { Flags = SceneNodeFlags.Selected });
        tip.BindTransform.LocalPosition = Vector3.UnitZ;
        SkeletonAnimation animation		= scene.AddNode(new SkeletonAnimation("clip") { Flags = SceneNodeFlags.Selected, Framerate = 30 });
        SkeletonAnimationTrack track	= new("middle");
        track.AddTranslationFrame(0, new Vector3(0, 2, 0));
        track.AddTranslationFrame(1, new Vector3(0, 4, 0));
        animation.Tracks.Add(track);
        using MemoryStream stream = new();
        new GltfTranslator().Write(scene, stream, SceneTranslator.CreateWriteContext("filtered.glb", new SceneTranslatorOptions { Filter = SceneNodeFlags.Selected }), CancellationToken.None);
        stream.Position = 0;
        GltfDocument document			= GltfReader.ParseGlb(stream);
        int tipIndex					= document.Nodes.FindIndex(static node => node.Name == "tip");
        GltfAnimation output			= Assert.Single(document.Animations);
        GltfAnimationChannel channel	= Assert.Single(output.Channels, channel => channel.TargetNode == tipIndex && channel.TargetPath == "translation");
        Assert.Equal([0f, 2f, 1f, 0f, 4f, 1f], document.ReadAccessorAsFloats(output.Samplers[channel.Sampler].Output));
    }

    [Fact]
    public void Gltf_UsesWideIndicesAtReservedUshortValue()
    {
        Scene scene = new();
        scene.AddNode(new Mesh
        {
            Name		= "mesh",
            Positions	= new DataBuffer<float>(new float[65536 * 3], 1, 3),
            FaceIndices	= new DataBuffer<int>([0, 1, 65535], 1, 1),
        });
        using MemoryStream stream = new(Write(new GltfTranslator(), scene, ".glb"));
        GltfDocument document = GltfReader.ParseGlb(stream);
        Assert.Equal(5125, document.Accessors[Assert.Single(Assert.Single(document.Meshes).Primitives).Indices].ComponentType);
    }

    [Theory]
    [InlineData("{\"asset\":{\"version\":\"1.0\"}}")]
    [InlineData("{\"extensionsRequired\":[\"KHR_draco_mesh_compression\"]}")]
    [InlineData("{\"animations\":[{\"samplers\":[{\"input\":0,\"output\":1,\"interpolation\":\"STEP\"}]}]}")]
    public void Gltf_RejectsUnsupportedInterpretations(string json)
    {
        Assert.Throws<NotSupportedException>(() => GltfJsonParser.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Gltf_RejectsAmbiguousAnimationTargets()
    {
        Scene scene = new();
        scene.AddNode(new Skeleton("first")).AddNode(new SkeletonBone("joint"));
        scene.AddNode(new Skeleton("second")).AddNode(new SkeletonBone("joint"));
        SkeletonAnimation animation		= scene.AddNode(new SkeletonAnimation("clip"));
        SkeletonAnimationTrack track	= new("joint");
        track.AddRotationFrame(0, Quaternion.Identity);
        animation.Tracks.Add(track);
        Assert.Throws<InvalidDataException>(() => Write(new GltfTranslator(), scene, ".glb"));
    }

    [Fact]
    public void MaterialRanges_PreserveUnassignedFacesAndUnsortedRanges()
    {
        Scene scene	= CreateTriangleScene();
        Mesh mesh	= Assert.Single(scene.GetDescendants<Mesh>());
        mesh.Materials				= [scene.AddNode(new Material("first")), scene.AddNode(new Material("second"))];
        mesh.FaceIndices			= new DataBuffer<int>([0, 1, 2, 0, 2, 1, 2, 1, 0], 1, 1);
        mesh.MaterialIndexRanges	= [(6, 3), (0, 3)];
        using MemoryStream stream = new(Write(new GltfTranslator(), scene, ".glb"));
        GltfDocument document = GltfReader.ParseGlb(stream);
        Assert.Equal([1, -1, 0], Assert.Single(document.Meshes).Primitives.Select(static primitive => primitive.Material));
        string obj = Encoding.UTF8.GetString(Write(new ObjTranslator(), scene, ".obj"));
        Assert.Equal(3, obj.Split('\n').Count(static line => line.StartsWith("f ")));
        Assert.Contains("usemtl second\nf ", obj);
        Assert.Contains("usemtl off\nf ", obj);
        Assert.Contains("usemtl first\nf ", obj);
        foreach (string extension in new[] { ".xmodel_export", ".xmodel_bin" })
        {
            Scene loaded = Read(new XModelTranslator(), Write(new XModelTranslator(), scene, extension), extension);
            Assert.Equal(3, loaded.GetDescendants<Mesh>().Sum(static item => item.FaceCount));
            Assert.Equal(["default", "first", "second"], loaded.GetDescendants<Mesh>().Select(static item => item.Materials![0].Name).Order());
        }
    }

    [Fact]
    public void Maya_HonorsRadiansAndTextureBindings()
    {
        Scene scene	= CreateTriangleScene();
        Mesh mesh	= Assert.Single(scene.GetDescendants<Mesh>());
        mesh.BindTransform.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
        Material material = scene.AddNode(new Material("surface") { DiffuseMapName = "diffuse" });
        material.Connect("diffuse", material.AddNode(new Texture("textures/diffuse.png")));
        mesh.Materials = [material];
        MayaAsciiTranslator translator = new();
        translator.WriteOptions.AngularUnit = MayaAngularUnit.Radian;
        string text = Encoding.ASCII.GetString(Write(translator, scene, ".ma"));
        Assert.Contains("-a rad", text);
        Assert.Contains("setAttr \".r\" -type \"double3\" 1.570", text);
        Assert.Contains("\"textures/diffuse.png\"", text);
    }

    [Theory]
    [InlineData(".psk")]
    [InlineData(".wiltmodel")]
    [InlineData(".semodel")]
    [InlineData(".cast")]
    [InlineData(".md5mesh")]
    [InlineData(".smd")]
    [InlineData(".glb")]
    public void FilteredSkeleton_RetainsPlacementAcrossRemovedAncestors(string extension)
    {
        Scene scene	= CreateTriangleScene();
        Mesh mesh	= Assert.Single(scene.GetDescendants<Mesh>());
        mesh.Flags = SceneNodeFlags.Selected;
        SkeletonBone root	= scene.AddNode(new SkeletonBone("root") { Flags = SceneNodeFlags.Selected });
        SkeletonBone middle	= root.AddNode(new SkeletonBone("middle"));
        SkeletonBone tip	= middle.AddNode(new SkeletonBone("tip") { Flags = SceneNodeFlags.Selected });
        root.BindTransform.LocalPosition	= new Vector3(10, 0, 0);
        middle.BindTransform.LocalPosition	= new Vector3(0, 20, 0);
        tip.BindTransform.LocalPosition		= new Vector3(0, 0, 30);
        mesh.Skin							= new Skin([root, tip], new DataBuffer<int>([0, 1, 1], 1, 1), new DataBuffer<float>([1f, 1f, 1f], 1, 1));
        SceneTranslator translator = CreateTranslator(extension);
        using MemoryStream output = new();
        SceneTranslatorOptions options = new() { Filter = SceneNodeFlags.Selected, WriteRawVertices = true };
        translator.Write(scene, output, SceneTranslator.CreateWriteContext("filtered" + extension, options), CancellationToken.None);
        Scene result			= Read(translator, output.ToArray(), extension);
        SkeletonBone loadedTip	= Assert.Single(result.GetDescendants<SkeletonBone>(), static bone => bone.Name == "tip");
        Assert.InRange(Vector3.Distance(new Vector3(10, 20, 30), loadedTip.GetBindWorldPosition()), 0, 0.0001f);
    }

    [Theory]
    [InlineData(".psk")]
    [InlineData(".psa")]
    [InlineData(".bvh")]
    [InlineData(".glb")]
    [InlineData(".ma")]
    [InlineData(".md5mesh")]
    [InlineData(".md5anim")]
    [InlineData(".smd")]
    [InlineData(".obj")]
    [InlineData(".seanim")]
    [InlineData(".semodel")]
    [InlineData(".cast")]
    [InlineData(".wiltmodel")]
    [InlineData(".wiltanim")]
    [InlineData(".xmodel_export")]
    [InlineData(".xanim_export")]
    [InlineData(".rfscene")]
    public void DirectWrite_PreCancelled_DoesNotWrite(string extension)
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using MemoryStream output = new();
        SceneTranslator translator = CreateTranslator(extension);
        Assert.Throws<OperationCanceledException>(() => translator.Write(new Scene(), output, SceneTranslator.CreateWriteContext("cancelled" + extension, new SceneTranslatorOptions()), cancellation.Token));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void XAnim_ReadsAuthoredWorldPoseAndWritesBothContainers()
    {
        string text		= "ANIMATION\nVERSION 3\nNUMPARTS 1\nPART 0 \"root\"\nFRAMERATE 30\nNUMFRAMES 1\nFRAME 0\nPART 0\nOFFSET 1 2 3\nSCALE 1 1 1\nX 1 0 0\nY 0 1 0\nZ 0 0 1\nNOTETRACKS\nPART 0\nNUMTRACKS 0\n";
        Scene source	= Read(new XAnimTranslator(), Encoding.UTF8.GetBytes(text), ".xanim_export");
        foreach (string extension in new[] { ".xanim_export", ".xanim_bin" })
        {
            Scene loaded					= Read(new XAnimTranslator(), Write(new XAnimTranslator(), source, extension), extension);
            SkeletonAnimationTrack track	= Assert.Single(Assert.Single(loaded.GetDescendants<SkeletonAnimation>()).Tracks);
            Assert.Equal(new Vector3(1, 2, 3), track.TranslationCurve!.GetVector3(0));
        }
    }

    [Fact]
    public void SceneFile_PreservesDescendantsOfUnregisteredParents()
    {
        Scene scene			= new();
        SceneNode parent	= scene.AddNode(new AnimationPlayer("unregistered"));
        parent.AddNode(new Mesh { Name = "descendant" });
        Scene loaded = Read(new SceneFileTranslator(), Write(new SceneFileTranslator(), scene, ".rfscene"), ".rfscene");
        Assert.Equal("unregistered", Assert.Single(loaded.GetDescendants<Mesh>()).Parent!.Name);
    }

    [Fact]
    public void SceneFile_RejectsRecordOverrunsAndReadsShortReads()
    {
        byte[] data = Write(new SceneFileTranslator(), new Scene(), ".rfscene");
        using TranslationTestStream shortReads = new(data, 1, null);
        new SceneFileTranslator().Read(new Scene(), shortReads, SceneTranslator.CreateReadContext("test.rfscene", new SceneTranslatorOptions()), CancellationToken.None);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12), 1);
        Assert.Throws<EndOfStreamException>(() => Read(new SceneFileTranslator(), data, ".rfscene"));
    }

    [Fact]
    public void Seanim_RejectsModifierOverflowBeforeWriting()
    {
        Scene scene					= new();
        SkeletonAnimation animation	= scene.AddNode(new SkeletonAnimation("clip") { TransformType = TransformType.Relative });
        for (int i = 0; i < 256; i++)
        {
            SkeletonAnimationTrack track = new($"bone_{i}") { TransformType = TransformType.Absolute };
            track.AddTranslationFrame(0, Vector3.Zero);
            animation.Tracks.Add(track);
        }
        using MemoryStream output = new();
        Assert.Throws<InvalidDataException>(() => new SeanimTranslator().Write(scene, output, SceneTranslator.CreateWriteContext("clip.seanim", new SceneTranslatorOptions()), CancellationToken.None));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void Gltf_ReadsShortReadsAndLeavesStreamOpen()
    {
        byte[] data = Write(new GltfTranslator(), CreateTriangleScene(), ".glb");
        using TranslationTestStream stream = new(data, 1, null);
        SceneTranslatorManager manager = new();
        manager.Register(new GltfTranslator());
        Assert.Single(manager.Read(stream, "test.glb", new SceneTranslatorOptions()).GetDescendants<Mesh>());
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Obj_CancelsDuringVertexConversion()
    {
        Scene scene = new();
        scene.AddNode(new Mesh
        {
            Name		= "mesh",
            Positions	= new DataBuffer<float>(new float[30000], 1, 3),
        });
        using CancellationTokenSource cancellation = new();
        using TranslationTestStream stream = new(new byte[1000000], int.MaxValue, cancellation.Cancel);
        Assert.Throws<OperationCanceledException>(() => new ObjTranslator().Write(scene, stream, SceneTranslator.CreateWriteContext("test.obj", new SceneTranslatorOptions()), cancellation.Token));
    }

    [Fact]
    public void Gltf_MatrixUsesRowVectorTranslation()
    {
        float[] values		= [2, 0, 0, 0, 0, 3, 0, 0, 0, 0, 4, 0, 5, 6, 7, 1];
        Matrix4x4 matrix	= GltfReader.ReadMatrix4x4(values, 0);
        Assert.Equal(new Vector3(7, 9, 11), Vector3.Transform(Vector3.One, matrix));
        float[] output = new float[16];
        GltfWriter.WriteMatrix4x4(Matrix4x4.CreateTranslation(8, 9, 10), output, 0);
        Assert.Equal(8f, output[12]);
        Assert.Equal(9f, output[13]);
        Assert.Equal(10f, output[14]);
    }

    [Fact]
    public void Gltf_JsonStartsAtCurrentStreamPosition()
    {
        byte[] prefix = Encoding.UTF8.GetBytes("ignored");
        using MemoryStream stream = new();
        stream.Write(prefix);
        stream.Write("{\"asset\":{\"version\":\"2.0\"},\"nodes\":[{\"name\":\"node\"}]}"u8);
        stream.Position = prefix.Length;
        Assert.Equal("node", Assert.Single(GltfReader.ParseGltf(stream, null).Nodes).Name);
        Assert.Equal(stream.Length, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(".gltf", true)]
    [InlineData(".glb", false)]
    public void Gltf_OutputMatchesExtension(string extension, bool json)
    {
        byte[] data = Write(new GltfTranslator(), CreateTriangleScene(), extension);
        if (json)
        {
            using JsonDocument document = JsonDocument.Parse(data);
            Assert.StartsWith("data:application/octet-stream;base64,", document.RootElement.GetProperty("buffers")[0].GetProperty("uri").GetString());
        }
        else
        {
            Assert.Equal("glTF"u8.ToArray(), data[..4]);
        }
    }

    [Theory]
    [InlineData(8, 4)]
    [InlineData(12, 2)]
    [InlineData(12, 4000)]
    public void Gltf_RejectsInvalidContainerBoundaries(int offset, int change)
    {
        byte[] data		= Write(new GltfTranslator(), CreateTriangleScene(), ".glb");
        uint original	= BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), original + (uint)change);
        using MemoryStream stream = new(data);
        Assert.Throws<InvalidDataException>(() => GltfReader.ParseGlb(stream));
    }

    [Fact]
    public void Gltf_RejectsSparseAccessors()
    {
        Assert.Throws<NotSupportedException>(() => GltfJsonParser.Parse("{\"accessors\":[{\"sparse\":{}}]}"u8.ToArray()));
    }

    [Fact]
    public void Gltf_AccessorHonorsStrideAndViewBoundary()
    {
        GltfDocument document = new();
        document.Buffers.Add(new GltfBuffer
        {
            ByteLength	= 16,
            Data		= [1, 0, 2, 0, 9, 9, 9, 9, 3, 0, 4, 0, 9, 9, 9, 9],
        });
        document.BufferViews.Add(new GltfBufferView
        {
            Buffer		= 0,
            ByteLength	= 16,
            ByteStride	= 8,
        });
        document.Accessors.Add(new GltfAccessor
        {
            BufferView		= 0,
            ComponentType	= 5123,
            Type			= "VEC2",
            Count			= 2,
        });
        Assert.Equal([1f, 2f, 3f, 4f], document.ReadAccessorAsFloats(0));
        document.BufferViews[0].ByteLength = 10;
        Assert.Throws<InvalidDataException>(() => document.ReadAccessorAsFloats(0));
    }

    [Fact]
    public void Gltf_PreservesAncestorTransformsAndSharedJoints()
    {
        GltfDocument document = new();
        document.Nodes.Add(new GltfNode
        {
            Translation	= [10, 0, 0],
            Scale		= [2, 2, 2],
            Children	= [1],
        });
        document.Nodes.Add(new GltfNode
        {
            Name		= "joint",
            Translation	= [3, 0, 0],
        });
        document.Skins.Add(new GltfSkin
        {
            Name	= "first",
            Joints	= [1],
        });
        document.Skins.Add(new GltfSkin
        {
            Name	= "second",
            Joints	= [1],
        });
        Scene scene = new();
        new GltfReader(document, "test", new SceneTranslatorOptions()).Read(scene);
        SkeletonBone bone = Assert.Single(scene.GetDescendants<SkeletonBone>());
        Assert.Equal(new Vector3(16, 0, 0), bone.GetBindWorldPosition());
        Assert.Equal(new Vector3(2), bone.GetBindLocalScale());
    }

    [Fact]
    public void Gltf_ExportsMaterialRangesAndVec4Tangents()
    {
        Scene scene		= CreateTriangleScene();
        Mesh mesh		= Assert.Single(scene.GetDescendants<Mesh>());
        Material first	= scene.AddNode(new Material("first"));
        Material second	= scene.AddNode(new Material("second"));
        mesh.Materials				= [first, second];
        mesh.FaceIndices			= new DataBuffer<int>([0, 1, 2, 2, 1, 0], 1, 1);
        mesh.MaterialIndexRanges	= [(0, 3), (3, 3)];
        mesh.Tangents				= new DataBuffer<float>([1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f], 1, 3);
        using MemoryStream stream = new(Write(new GltfTranslator(), scene, ".glb"));
        GltfDocument document	= GltfReader.ParseGlb(stream);
        GltfMesh exported		= Assert.Single(document.Meshes);
        Assert.Equal(2, exported.Primitives.Count);
        Assert.Equal([0, 1], exported.Primitives.Select(static primitive => primitive.Material));
        Assert.Equal("VEC4", document.Accessors[exported.Primitives[0].Attributes["TANGENT"]].Type);
        Assert.Equal(1f, document.ReadAccessorAsFloats(exported.Primitives[0].Attributes["TANGENT"])[3]);
    }

    [Fact]
    public void Gltf_UsesIdentityForSkinsWithDuplicateNamesAndRetainsInverseBinds()
    {
        Scene scene		= CreateTriangleScene();
        Mesh firstMesh	= Assert.Single(scene.GetDescendants<Mesh>());
        Mesh secondMesh	= scene.AddNode(new Mesh
        {
            Name		= "second",
            Positions	= firstMesh.Positions,
            FaceIndices	= firstMesh.FaceIndices,
        });
        Group firstGroup			= scene.AddNode(new Group("first_group"));
        Group secondGroup			= scene.AddNode(new Group("second_group"));
        SkeletonBone first			= firstGroup.AddNode(new Skeleton("same")).AddNode(new SkeletonBone("joint"));
        SkeletonBone second			= secondGroup.AddNode(new Skeleton("same")).AddNode(new SkeletonBone("joint"));
        DataBuffer<int> indices		= new([0, 0, 0], 1, 1);
        DataBuffer<float> weights	= new([1f, 1f, 1f], 1, 1);
        firstMesh.Skin	= new Skin([first], indices, weights, [Matrix4x4.CreateTranslation(-10, 0, 0)]);
        secondMesh.Skin	= new Skin([second], indices, weights, [Matrix4x4.CreateTranslation(-20, 0, 0)]);
        using MemoryStream stream = new(Write(new GltfTranslator(), scene, ".glb"));
        GltfDocument document	= GltfReader.ParseGlb(stream);
        GltfNode[] meshes		= document.Nodes.Where(static node => node.Mesh >= 0).ToArray();
        GltfSkin firstSkin		= document.Skins[meshes[0].Skin];
        GltfSkin secondSkin		= document.Skins[meshes[1].Skin];
        Assert.NotEqual(firstSkin.Joints[0], secondSkin.Joints[0]);
        Assert.Equal(-10f, document.ReadAccessorAsFloats(firstSkin.InverseBindMatrices)[12]);
        Assert.Equal(-20f, document.ReadAccessorAsFloats(secondSkin.InverseBindMatrices)[12]);
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(0, 2, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 2, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 0)]
    public void Bvh_AnalyticalEulerPreservesAllOrdersAndGimbalLocks(int first, int middle, int last)
    {
        BvhChannelType[] axes		= [BvhChannelType.Xrotation, BvhChannelType.Yrotation, BvhChannelType.Zrotation];
        BvhChannelType[] channels	= [axes[first], axes[middle], axes[last]];
        Vector3 previous			= new(20, -30, 40);
        foreach (float angle in new[] { 25f, 89f, 90f, 91f, 179f, 181f })
        {
            Vector3 source = new(20, -30, 40);
            source[middle] = angle;
            Quaternion rotation	= BvhRotation.ComposeDegrees(source, channels);
            Vector3 result		= BvhRotation.ToEulerDegrees(rotation, channels, previous);
            Assert.True(MathF.Abs(Quaternion.Dot(rotation, BvhRotation.ComposeDegrees(result, channels))) > 0.99999f);
            previous = result;
        }
    }

    [Theory]
    [InlineData("bad }")]
    [InlineData("}")]
    [InlineData("1 2 }")]
    [InlineData("1")]
    public void Md5_RejectsMalformedFrameImmediately(string text)
    {
        Assert.Throws<InvalidDataException>(() => ParseMd5Frame(text));
    }

    [Fact]
    public void Md5_WorldTransformIncludesDeepAncestors()
    {
        Md5AnimJoint[] hierarchy = new Md5AnimJoint[200];
        (Vector3, Quaternion)[] bind = new (Vector3, Quaternion)[200];
        for (int i = 0; i < hierarchy.Length; i++)
        {
            hierarchy[i]	= new Md5AnimJoint($"bone_{i}", i - 1, 0, 0);
            bind[i]			= (Vector3.UnitX, Quaternion.Identity);
        }
        Assert.Equal(new Vector3(200, 0, 0), Md5AnimReader.GetFrameWorldTransform(199, hierarchy, bind, []).Position);
    }

    [Fact]
    public void Psa_TrackTimeIsEncodedAsFloat()
    {
        Scene scene						= new();
        SkeletonAnimation animation		= scene.AddNode(new SkeletonAnimation("clip"));
        SkeletonAnimationTrack track	= new("root");
        track.AddTranslationFrame(0, Vector3.Zero);
        track.AddTranslationFrame(3, Vector3.One);
        animation.Tracks.Add(track);
        byte[] data		= Write(new PsaTranslator(), scene, ".psa");
        int sequence	= Encoding.ASCII.GetString(data).IndexOf("ANIMINFO", StringComparison.Ordinal);
        Assert.True(sequence >= 0);
        Assert.Equal(4f, BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(sequence + 32 + 148)));
    }

    [Fact]
    public void Psk_RoundTripDoesNotCollideMeshAndMaterialNames()
    {
        Scene scene	= CreateTriangleScene();
        Mesh mesh	= Assert.Single(scene.GetDescendants<Mesh>());
        mesh.Materials = [scene.AddNode(new Material("material"))];
        byte[] data		= Write(new PskTranslator(), scene, ".psk");
        Scene result	= Read(new PskTranslator(), data, ".psk");
        Assert.Equal(3, Assert.Single(result.GetDescendants<Mesh>()).VertexCount);
        Assert.Equal("material", Assert.Single(result.GetDescendants<Material>()).Name);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(65535)]
    [InlineData(65536)]
    public void Seanim_ReadsSpecifiedModifierIndexWidth(int boneCount)
    {
        Scene scene					= Read(new SeanimTranslator(), CreateSeanim(boneCount), ".seanim");
        SkeletonAnimation animation	= Assert.Single(scene.GetDescendants<SkeletonAnimation>());
        Assert.Equal(boneCount, animation.Tracks.Count);
        Assert.Equal(TransformType.Absolute, animation.Tracks[^1].TransformType);
    }

    [Fact]
    public void Seanim_HonorsExtendedHeaders()
    {
        byte[] original = CreateSeanim(1);
        byte[] extended = new byte[original.Length + 8];
        original.AsSpan(0, 36).CopyTo(extended);
        BinaryPrimitives.WriteUInt16LittleEndian(extended.AsSpan(8), 36);
        original.AsSpan(36).CopyTo(extended.AsSpan(44));
        Assert.Single(Read(new SeanimTranslator(), extended, ".seanim").GetDescendants<SkeletonAnimation>());
    }

    [Fact]
    public void Seanim_PreservesRotationModeAndDoublePrecision()
    {
        Scene scene					= new();
        SkeletonAnimation animation	= scene.AddNode(new SkeletonAnimation("clip") { TransformType = TransformType.Additive });
        animation.Tracks.Add(new SkeletonAnimationTrack("root")
        {
            TransformType	= TransformType.Additive,
            RotationCurve	= new AnimationCurve(TransformSpace.Local, TransformType.Additive)
            {
                Keys	= new DataBuffer<float>([0f]),
                Values	= new DataBuffer<double>(new double[] { 0.123456789123, 0, 0, 1 }, 1, 4),
            },
        });
        Scene loaded			= Read(new SeanimTranslator(), Write(new SeanimTranslator(), scene, ".seanim"), ".seanim");
        AnimationCurve rotation	= Assert.Single(loaded.GetDescendants<SkeletonAnimation>()).Tracks[0].RotationCurve!;
        Assert.Equal(TransformType.Additive, rotation.TransformType);
        Assert.Equal(0.123456789123, rotation.Values!.Get<double>(0, 0, 0));
    }

    [Fact]
    public void Smd_MapsSerializedBoneIdsAndNumericMaterialNames()
    {
        string text	= "version 1\nnodes\n10 \"root\" -1\n20 \"child\" 10\nend\nskeleton\ntime 0\n10 0 0 0 0 0 0\n20 1 0 0 0 0 0\nend\ntriangles\n123 material\n20 0 0 0 0 0 1 0 0 1 20 1\n20 1 0 0 0 0 1 1 0 1 20 1\n20 0 1 0 0 0 1 0 1 1 20 1\nend\n";
        Scene scene	= Read(new SmdTranslator(), Encoding.UTF8.GetBytes(text), ".smd");
        Mesh mesh	= Assert.Single(scene.GetDescendants<Mesh>());
        Assert.Equal("123 material", Assert.Single(mesh.Materials!).Name);
        Assert.Equal("child", mesh.Skin!.Bones[mesh.Skin.GetBoneIndex(0, 0)].Name);
    }

    [Fact]
    public void Smd_RejectsIncompleteTriangle()
    {
        string text = "version 1\nnodes\n0 \"root\" -1\nend\ntriangles\nmaterial\n0 0 0 0 0 0 1 0 0\nend\n";
        Assert.Throws<InvalidDataException>(() => Read(new SmdTranslator(), Encoding.UTF8.GetBytes(text), ".smd"));
    }

    [Fact]
    public void Smd_UnskinnedExportHasIdentityRoot()
    {
        Scene scene = Read(new SmdTranslator(), Write(new SmdTranslator(), CreateTriangleScene(), ".smd"), ".smd");
        Assert.Equal(Vector3.Zero, Assert.Single(scene.GetDescendants<SkeletonBone>()).GetBindLocalPosition());
        Assert.Equal(3, Assert.Single(scene.GetDescendants<Mesh>()).VertexCount);
    }

    [Fact]
    public void Obj_TriangulatesConcavePolygonWithTabsAndComments()
    {
        string text	= "v\t0 0 0\nv 3 0 0\nv 3 3 0\nv 1.5 1 0\nv 0 3 0\nf\t1 2 3 4 5 # face\n";
        Mesh mesh	= Assert.Single(Read(new ObjTranslator(), Encoding.UTF8.GetBytes(text), ".obj").GetDescendants<Mesh>());
        float area	= 0;
        for (int i = 0; i < mesh.IndexCount; i += 3)
        {
            Vector3 a = mesh.Positions!.GetVector3(mesh.FaceIndices!.Get<int>(i, 0, 0), 0);
            Vector3 b = mesh.Positions.GetVector3(mesh.FaceIndices.Get<int>(i + 1, 0, 0), 0);
            Vector3 c = mesh.Positions.GetVector3(mesh.FaceIndices.Get<int>(i + 2, 0, 0), 0);
            area += Vector3.Cross(b - a, c - a).Z / 2;
        }
        Assert.Equal(6f, area, 4);
    }

    [Fact]
    public void Mtl_ParsesStringAndVectorTextureOptions()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("newmtl\tmat\nKd 0.1 0.2 0.3\nmap_Kd -clamp on -s 2 3 -o -1 0 1 textures/a.png # image\n"));
        Dictionary<string, Material> materials = [];
        ObjMtlReader.Read(stream, materials, Path.GetTempPath());
        Assert.Equal("textures/a.png", materials["mat"].GetDiffuseMap().FilePath);
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 1), materials["mat"].DiffuseColor);
    }

    [Fact]
    public void Maya_EmitsEachDagNodeOnceInParentOrder()
    {
        Scene scene		= new();
        MeshGroup group	= scene.AddNode(new MeshGroup("model"));
        group.AddNode(new Group("child"));
        string text = Encoding.ASCII.GetString(Write(new MayaAsciiTranslator(), scene, ".ma"));
        Assert.Equal(1, text.Split("createNode transform -n \"model\"").Length - 1);
        Assert.True(text.IndexOf("-n \"model\"", StringComparison.Ordinal) < text.IndexOf("-n \"child\"", StringComparison.Ordinal));
        Assert.Contains("-n \"child\" -p \"model\"", text);
    }

    [Fact]
    public void Cast_AssignsNestedAndUngroupedMeshesOnce()
    {
        Scene scene		= CreateTriangleScene();
        Mesh mesh		= Assert.Single(scene.GetDescendants<Mesh>());
        MeshGroup outer	= scene.AddNode(new MeshGroup("outer"));
        MeshGroup inner	= outer.AddNode(new MeshGroup("inner"));
        inner.AddNode(new Mesh
        {
            Name		= "nested",
            Positions	= mesh.Positions,
            FaceIndices	= mesh.FaceIndices,
        });
        Scene loaded = Read(new CastTranslator(), Write(new CastTranslator(), scene, ".cast"), ".cast");
        Assert.Equal(2, loaded.GetDescendants<Mesh>().Length);
    }

    [Fact]
    public void Wilt_RejectsSectionsThatExceedTheFile()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write("WLTA"u8);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write(0);
        writer.Write(0xffffffffu);
        writer.Write(100u);
        Assert.Throws<InvalidDataException>(() => Read(new WiltAnimTranslator(), stream.ToArray(), ".wiltanim"));
    }

    [Fact]
    public void SceneFile_RejectsOversizedBuffersBeforeAllocation()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write((byte)SceneBufferType.Single);
        writer.Write(int.MaxValue);
        writer.Write(int.MaxValue);
        writer.Write(3);
        stream.Position = 0;
        using BinaryReader reader = new(stream);
        Assert.Throws<InvalidDataException>(() => reader.ReadBuffer());
    }

    [Fact]
    public void SceneFile_RejectsCyclicParents()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write("RFSC"u8);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write(1);
        writer.Write("Group");
        writer.Write(0);
        Assert.Throws<InvalidDataException>(() => Read(new SceneFileTranslator(), stream.ToArray(), ".rfscene"));
    }

    [Fact]
    public void Wilt_RejectsMixedFrameRates()
    {
        Scene scene = new();
        scene.AddNode(new SkeletonAnimation("skeletal") { Framerate = 60 });
        scene.AddNode(new MorphAnimation("morph") { Framerate = 30 });
        Assert.Throws<InvalidDataException>(() => Write(new WiltAnimTranslator(), scene, ".wiltanim"));
    }

    private static SceneTranslator CreateTranslator(string extension) => extension switch
    {
        ".psk" => new PskTranslator(),
        ".psa" => new PsaTranslator(),
        ".bvh" => new BvhTranslator(),
        ".glb" => new GltfTranslator(),
        ".ma" => new MayaAsciiTranslator(),
        ".md5mesh" => new Md5MeshTranslator(),
        ".md5anim" => new Md5AnimTranslator(),
        ".smd" => new SmdTranslator(),
        ".obj" => new ObjTranslator(),
        ".seanim" => new SeanimTranslator(),
        ".semodel" => new SemodelTranslator(),
        ".cast" => new CastTranslator(),
        ".wiltmodel" => new WiltModelTranslator(),
        ".wiltanim" => new WiltAnimTranslator(),
        ".xmodel_export" => new XModelTranslator(),
        ".xanim_export" => new XAnimTranslator(),
        ".rfscene" => new SceneFileTranslator(),
        _ => throw new ArgumentException(extension),
    };

    private static void ParseMd5Frame(string text)
    {
        TextTokenReader reader = new(text);
        Md5AnimReader.ParseFrame(ref reader, 1);
    }

    private static byte[] CreateSeanim(int boneCount)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write("SEAnim"u8);
        writer.Write((ushort)1);
        writer.Write((ushort)28);
        writer.Write((byte)2);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((ushort)0);
        writer.Write(30f);
        writer.Write(1);
        writer.Write(boneCount);
        writer.Write((byte)1);
        writer.Write(new byte[3]);
        writer.Write(0);
        for (int i = 0; i < boneCount; i++)
            writer.Write((byte)0);
        if (boneCount <= 255)
            writer.Write((byte)(boneCount - 1));
        else if (boneCount <= 65535)
            writer.Write((ushort)(boneCount - 1));
        else
            writer.Write(boneCount - 1);
        writer.Write((byte)0);
        for (int i = 0; i < boneCount; i++)
            writer.Write((ushort)0);
        return stream.ToArray();
    }

    private static Scene CreateTriangleScene()
    {
        Scene scene = new("triangle");
        scene.AddNode(new Mesh
        {
            Name		= "mesh",
            Positions	= new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3),
            FaceIndices	= new DataBuffer<int>([0, 1, 2], 1, 1),
        });
        return scene;
    }

    private static byte[] Write(SceneTranslator translator, Scene scene, string extension)
    {
        using MemoryStream stream = new();
        translator.Write(scene, stream, SceneTranslator.CreateWriteContext("fixture" + extension, new SceneTranslatorOptions()), CancellationToken.None);
        Assert.True(stream.CanWrite);
        return stream.ToArray();
    }

    private static Scene Read(SceneTranslator translator, byte[] data, string extension)
    {
        using MemoryStream stream = new(data);
        Scene scene = new();
        translator.Read(scene, stream, SceneTranslator.CreateReadContext("fixture" + extension, new SceneTranslatorOptions()), CancellationToken.None);
        Assert.True(stream.CanRead);
        return scene;
    }
}
