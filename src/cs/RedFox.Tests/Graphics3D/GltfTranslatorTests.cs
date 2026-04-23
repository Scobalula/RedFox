// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Gltf;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class GltfTranslatorTests
{
    [Fact]
    public void GltfTranslator_RoundTrip_PreservesPositionsNormalsUVs()
    {
        Scene sourceScene = CreateTriangleScene();
        SceneTranslatorManager manager = CreateManager();

        byte[] glbData = WriteSceneToGlb(manager, sourceScene);
        Scene loadedScene = ReadSceneFromGlb(manager, glbData);

        Mesh[] sourceMeshes = sourceScene.GetDescendants<Mesh>();
        Mesh[] loadedMeshes = loadedScene.GetDescendants<Mesh>();

        Assert.Equal(sourceMeshes.Length, loadedMeshes.Length);
        Assert.Equal(sourceMeshes[0].VertexCount, loadedMeshes[0].VertexCount);
        Assert.Equal(sourceMeshes[0].FaceCount, loadedMeshes[0].FaceCount);

        for (int i = 0; i < sourceMeshes[0].VertexCount; i++)
        {
            AssertVector3Equal(
                sourceMeshes[0].Positions!.GetVector3(i, 0),
                loadedMeshes[0].Positions!.GetVector3(i, 0),
                1e-5f);

            AssertVector3Equal(
                sourceMeshes[0].Normals!.GetVector3(i, 0),
                loadedMeshes[0].Normals!.GetVector3(i, 0),
                1e-5f);

            AssertVector2Equal(
                sourceMeshes[0].UVLayers!.GetVector2(i, 0),
                loadedMeshes[0].UVLayers!.GetVector2(i, 0),
                1e-5f);
        }
    }

    [Fact]
    public void GltfTranslator_RoundTrip_PreservesMultipleMeshes()
    {
        Scene sourceScene = CreateMultiMeshScene();
        SceneTranslatorManager manager = CreateManager();

        byte[] glbData = WriteSceneToGlb(manager, sourceScene);
        Scene loadedScene = ReadSceneFromGlb(manager, glbData);

        Mesh[] loadedMeshes = loadedScene.GetDescendants<Mesh>();
        Assert.Equal(2, loadedMeshes.Length);
    }

    [Fact]
    public void GltfTranslator_RoundTrip_PreservesMaterials()
    {
        Scene sourceScene = CreateSceneWithMaterials();
        SceneTranslatorManager manager = CreateManager();

        byte[] glbData = WriteSceneToGlb(manager, sourceScene);
        Scene loadedScene = ReadSceneFromGlb(manager, glbData);

        Material[] loadedMats = loadedScene.GetDescendants<Material>();
        Assert.Single(loadedMats);
        Assert.Equal("PBRMaterial", loadedMats[0].Name);
        Assert.NotNull(loadedMats[0].DiffuseColor);

        Vector4 dc = loadedMats[0].DiffuseColor!.Value;
        Assert.True(MathF.Abs(dc.X - 0.8f) < 1e-5f);
        Assert.True(MathF.Abs(dc.Y - 0.2f) < 1e-5f);
        Assert.True(MathF.Abs(dc.Z - 0.1f) < 1e-5f);
    }

    [Fact]
    public void GltfReader_BuildMaterials_UsesTextureNodeNamesAndResolvesRelativeTexturePaths()
    {
        GltfDocument doc = new();
        doc.Images.Add(new GltfImage
        {
            Uri = "textures/diffuse.png",
            Name = "DiffuseImage",
        });
        doc.Textures.Add(new GltfTexture
        {
            Source = 0,
        });
        doc.Materials.Add(new GltfMaterial
        {
            Name = "Textured",
            BaseColorTextureIndex = 0,
        });

        Scene scene = new("GltfMaterials");
        Model model = scene.RootNode.AddNode(new Model { Name = "ModelRoot" });
        GltfReader reader = new(doc, "TexturedScene", new SceneTranslatorOptions(), @"C:\Temp\Model");

        Material material = reader.BuildMaterials(model).Single();

        Assert.Equal("diffuse_diffuse", material.DiffuseMapName);
        Assert.True(material.TryGetDiffuseMap(out Texture? diffuseTexture));
        Assert.NotNull(diffuseTexture);
        Assert.Equal("textures/diffuse.png", diffuseTexture!.FilePath);
        Assert.Equal(Path.GetFullPath(@"C:\Temp\Model\textures\diffuse.png"), diffuseTexture.ResolvedFilePath);
    }

    [Fact]
    public void GltfWriter_UsesTextureFilePathInsteadOfTextureNodeName()
    {
        Scene scene = CreateSceneWithMaterials();
        Material material = scene.GetDescendants<Material>().Single();
        material.DiffuseMapName = "diffuse_tex";
        material.AddNode(new Texture("textures/diffuse.png", "diffuse")
        {
            Name = "diffuse_tex",
        });

        using MemoryStream stream = new();
        GltfWriter writer = new(new SceneTranslatorOptions());
        writer.Write(scene, stream, "TexturedScene");

        stream.Position = 0;
        GltfDocument doc = GltfReader.ParseGlb(stream);

        Assert.Single(doc.Images);
        Assert.Equal("textures/diffuse.png", doc.Images[0].Uri);
    }

    [Fact]
    public void GltfTranslator_RoundTrip_PreservesSkeleton()
    {
        Scene sourceScene = CreateSkinnedScene();
        SceneTranslatorManager manager = CreateManager();

        byte[] glbData = WriteSceneToGlb(manager, sourceScene);
        Scene loadedScene = ReadSceneFromGlb(manager, glbData);

        SkeletonBone[] skeletons = loadedScene.GetDescendants<SkeletonBone>().Where(b => b.Parent is not SkeletonBone).ToArray();
        Assert.Single(skeletons);

        SkeletonBone[] bones = skeletons[0].EnumerateHierarchy<SkeletonBone>().ToArray();
        Assert.Equal(2, bones.Length);

        Mesh[] meshes = loadedScene.GetDescendants<Mesh>();
        Assert.Single(meshes);
        Assert.NotNull(meshes[0].BoneIndices);
        Assert.NotNull(meshes[0].BoneWeights);
    }

    [Fact]
    public void GltfTranslator_RoundTrip_PreservesAnimation()
    {
        Scene sourceScene = CreateAnimatedScene();
        SceneTranslatorManager manager = CreateManager();

        byte[] glbData = WriteSceneToGlb(manager, sourceScene);
        Scene loadedScene = ReadSceneFromGlb(manager, glbData);

        SkeletonAnimation[] animations = loadedScene.GetDescendants<SkeletonAnimation>();
        Assert.Single(animations);
        Assert.Equal("WalkCycle", animations[0].Name);
        Assert.True(animations[0].Tracks.Count > 0);

        // Check that translation keyframes survived
        SkeletonAnimationTrack? rootTrack = animations[0].Tracks
            .FirstOrDefault(t => t.Name == "Root");
        Assert.NotNull(rootTrack);
        Assert.NotNull(rootTrack.TranslationCurve);
        Assert.Equal(3, rootTrack.TranslationCurve!.KeyFrameCount);
    }

    [Fact]
    public void GltfTranslator_RoundTrip_DeterministicRewrite()
    {
        Scene sourceScene = CreateTriangleScene();
        SceneTranslatorManager manager = CreateManager();

        byte[] firstWrite = WriteSceneToGlb(manager, sourceScene);
        Scene firstLoad = ReadSceneFromGlb(manager, firstWrite);
        byte[] secondWrite = WriteSceneToGlb(manager, firstLoad);
        Scene secondLoad = ReadSceneFromGlb(manager, secondWrite);

        AssertSceneStructureEquivalent(firstLoad, secondLoad);
    }

    [Fact]
    public void GltfTranslator_PositionsOnly_RoundTrips()
    {
        Scene scene = new("PositionsOnly");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });
        Mesh mesh = model.AddNode(new Mesh { Name = "pos_only" });
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        SceneTranslatorManager manager = CreateManager();
        byte[] glbData = WriteSceneToGlb(manager, scene);
        Scene loaded = ReadSceneFromGlb(manager, glbData);

        Mesh loadedMesh = loaded.GetDescendants<Mesh>().Single();
        Assert.Equal(3, loadedMesh.VertexCount);
        Assert.Equal(1, loadedMesh.FaceCount);
    }

    [Fact]
    public void GltfTranslator_VertexColors_RoundTrip()
    {
        Scene scene = new("ColorScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });
        Mesh mesh = model.AddNode(new Mesh { Name = "colored" });
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.ColorLayers = new DataBuffer<float>(
        [
            1f, 0f, 0f, 1f,
            0f, 1f, 0f, 1f,
            0f, 0f, 1f, 1f,
        ], 1, 4);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        SceneTranslatorManager manager = CreateManager();
        byte[] glbData = WriteSceneToGlb(manager, scene);
        Scene loaded = ReadSceneFromGlb(manager, glbData);

        Mesh loadedMesh = loaded.GetDescendants<Mesh>().Single();
        Assert.NotNull(loadedMesh.ColorLayers);
        Assert.Equal(3, loadedMesh.ColorLayers!.ElementCount);

        // Red vertex
        Assert.True(MathF.Abs(loadedMesh.ColorLayers.Get<float>(0, 0, 0) - 1f) < 1e-5f);
        Assert.True(MathF.Abs(loadedMesh.ColorLayers.Get<float>(0, 0, 1)) < 1e-5f);
    }

    [Fact]
    public void GltfTranslator_GlbMagic_IsDetectedCorrectly()
    {
        GltfTranslator translator = new();

        // Test with GLB magic bytes
        byte[] glbStart = [0x67, 0x6C, 0x54, 0x46]; // "glTF"
        Assert.True(translator.IsValid("test.glb", ".glb", new SceneTranslatorOptions(), glbStart));
        Assert.True(translator.IsValid("test.bin", ".bin", new SceneTranslatorOptions(), glbStart));

        // Test without magic
        Assert.True(translator.IsValid("test.gltf", ".gltf", new SceneTranslatorOptions()));
        Assert.False(translator.IsValid("test.obj", ".obj", new SceneTranslatorOptions()));
    }

    [Fact]
    public void GltfJsonParser_ParsesMinimalDocument()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("""
            {
                "asset": { "version": "2.0" },
                "scene": 0,
                "scenes": [{ "nodes": [0] }],
                "nodes": [{ "name": "TestNode", "mesh": 0 }],
                "meshes": [{
                    "primitives": [{
                        "attributes": { "POSITION": 0 },
                        "indices": 1
                    }]
                }],
                "accessors": [
                    { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                    { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
                ],
                "bufferViews": [
                    { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                    { "buffer": 0, "byteOffset": 36, "byteLength": 6 }
                ],
                "buffers": [{ "byteLength": 42 }]
            }
            """);

        GltfDocument doc = GltfJsonParser.Parse(json);

        Assert.Equal(0, doc.Scene);
        Assert.Single(doc.Scenes);
        Assert.Single(doc.Nodes);
        Assert.Equal("TestNode", doc.Nodes[0].Name);
        Assert.Equal(0, doc.Nodes[0].Mesh);
        Assert.Single(doc.Meshes);
        Assert.Equal(2, doc.Accessors.Count);
        Assert.Equal(GltfConstants.ComponentTypeFloat, doc.Accessors[0].ComponentType);
        Assert.Equal(GltfConstants.TypeVec3, doc.Accessors[0].Type);
        Assert.Equal(2, doc.BufferViews.Count);
        Assert.Single(doc.Buffers);
    }

    [Fact]
    public void GltfJsonParser_ParsesMaterialWithPBR()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("""
            {
                "materials": [{
                    "name": "Gold",
                    "pbrMetallicRoughness": {
                        "baseColorFactor": [1.0, 0.766, 0.336, 1.0],
                        "metallicFactor": 1.0,
                        "roughnessFactor": 0.1
                    },
                    "doubleSided": true
                }]
            }
            """);

        GltfDocument doc = GltfJsonParser.Parse(json);

        Assert.Single(doc.Materials);
        GltfMaterial mat = doc.Materials[0];
        Assert.Equal("Gold", mat.Name);
        Assert.True(MathF.Abs(mat.BaseColorFactor[0] - 1.0f) < 1e-5f);
        Assert.True(MathF.Abs(mat.MetallicFactor - 1.0f) < 1e-5f);
        Assert.True(MathF.Abs(mat.RoughnessFactor - 0.1f) < 1e-5f);
        Assert.True(mat.DoubleSided);
    }

    [Fact]
    public void GltfJsonParser_ParsesSkinAndAnimation()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("""
            {
                "skins": [{
                    "name": "Armature",
                    "inverseBindMatrices": 0,
                    "joints": [1, 2, 3],
                    "skeleton": 1
                }],
                "animations": [{
                    "name": "Walk",
                    "channels": [{
                        "sampler": 0,
                        "target": { "node": 1, "path": "translation" }
                    }],
                    "samplers": [{
                        "input": 0,
                        "output": 1,
                        "interpolation": "LINEAR"
                    }]
                }]
            }
            """);

        GltfDocument doc = GltfJsonParser.Parse(json);

        Assert.Single(doc.Skins);
        Assert.Equal("Armature", doc.Skins[0].Name);
        Assert.Equal(3, doc.Skins[0].Joints.Length);
        Assert.Equal(1, doc.Skins[0].SkeletonRoot);

        Assert.Single(doc.Animations);
        Assert.Equal("Walk", doc.Animations[0].Name);
        Assert.Single(doc.Animations[0].Channels);
        Assert.Equal("translation", doc.Animations[0].Channels[0].TargetPath);
    }

    [Fact]
    public void GltfDocument_ReadAccessorAsFloats_HandlesComponentTypes()
    {
        GltfDocument doc = new();
        doc.Buffers.Add(new GltfBuffer
        {
            ByteLength = 12,
            Data = new byte[]
            {
                0x00, 0x00, 0x80, 0x3F, // 1.0f
                0x00, 0x00, 0x00, 0x40, // 2.0f
                0x00, 0x00, 0x40, 0x40, // 3.0f
            }
        });
        doc.BufferViews.Add(new GltfBufferView
        {
            Buffer = 0,
            ByteOffset = 0,
            ByteLength = 12
        });
        doc.Accessors.Add(new GltfAccessor
        {
            BufferView = 0,
            ComponentType = GltfConstants.ComponentTypeFloat,
            Count = 1,
            Type = GltfConstants.TypeVec3
        });

        float[] result = doc.ReadAccessorAsFloats(0);
        Assert.Equal(3, result.Length);
        Assert.True(MathF.Abs(result[0] - 1.0f) < 1e-5f);
        Assert.True(MathF.Abs(result[1] - 2.0f) < 1e-5f);
        Assert.True(MathF.Abs(result[2] - 3.0f) < 1e-5f);
    }

    [Fact]
    public void GltfDocument_ReadAccessorAsInts_HandlesUShort()
    {
        GltfDocument doc = new();
        doc.Buffers.Add(new GltfBuffer
        {
            ByteLength = 6,
            Data = new byte[]
            {
                0x00, 0x00, // 0
                0x01, 0x00, // 1
                0x02, 0x00, // 2
            }
        });
        doc.BufferViews.Add(new GltfBufferView
        {
            Buffer = 0,
            ByteOffset = 0,
            ByteLength = 6
        });
        doc.Accessors.Add(new GltfAccessor
        {
            BufferView = 0,
            ComponentType = GltfConstants.ComponentTypeUnsignedShort,
            Count = 3,
            Type = GltfConstants.TypeScalar
        });

        int[] result = doc.ReadAccessorAsInts(0);
        Assert.Equal(3, result.Length);
        Assert.Equal(0, result[0]);
        Assert.Equal(1, result[1]);
        Assert.Equal(2, result[2]);
    }

    [Fact]
    public void GltfTranslator_CorpusGlb_CanLoadWithoutError()
    {
        string? testRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (testRoot is null)
            testRoot = @"Z:\Tests\RedFox";

        string inputDir = Path.Combine(testRoot, "Input", "Gltf");
        if (!Directory.Exists(inputDir))
            return; // Skip if test data not available

        string[] glbFiles = Directory.GetFiles(inputDir, "*.glb", SearchOption.AllDirectories);
        SceneTranslatorManager manager = CreateManager();
        int loaded = 0;
        List<string> failures = [];

        foreach (string filePath in glbFiles.Take(50)) // Test first 50 for speed
        {
            try
            {
                Scene scene = new(Path.GetFileName(filePath));
                GltfTranslator translator = new();
                translator.Read(scene, filePath, new SceneTranslatorOptions(), null);
                loaded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        Assert.True(loaded > 0, "No GLB files were loaded.");
        // Allow some failures for unsupported extensions (Draco, KTX2, etc.)
        Assert.True(failures.Count < loaded, $"Too many failures ({failures.Count}/{loaded + failures.Count}):\n{string.Join("\n", failures.Take(10))}");
    }

    [Fact]
    public void GltfTranslator_CorpusGltf_CanLoadWithoutError()
    {
        string? testRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (testRoot is null)
            testRoot = @"Z:\Tests\RedFox";

        string inputDir = Path.Combine(testRoot, "Input", "Gltf");
        if (!Directory.Exists(inputDir))
            return; // Skip if test data not available

        string[] gltfFiles = Directory.GetFiles(inputDir, "*.gltf", SearchOption.AllDirectories);
        int loaded = 0;
        List<string> failures = [];

        foreach (string filePath in gltfFiles.Take(50)) // Test first 50 for speed
        {
            try
            {
                Scene scene = new(Path.GetFileName(filePath));
                GltfTranslator translator = new();
                translator.Read(scene, filePath, new SceneTranslatorOptions(), null);
                loaded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        Assert.True(loaded > 0, "No GLTF files were loaded.");
        Assert.True(failures.Count < loaded, $"Too many failures ({failures.Count}/{loaded + failures.Count}):\n{string.Join("\n", failures.Take(10))}");
    }

    // ---- Helper methods ----

    private static Scene CreateTriangleScene()
    {
        Scene scene = new("TriangleScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });
        Mesh mesh = model.AddNode(new Mesh { Name = "triangle" });

        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);

        mesh.Normals = new DataBuffer<float>(
        [
            0f, 0f, 1f,
            0f, 0f, 1f,
            0f, 0f, 1f,
        ], 1, 3);

        mesh.UVLayers = new DataBuffer<float>(
        [
            0f, 0f,
            1f, 0f,
            0f, 1f,
        ], 1, 2);

        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        return scene;
    }

    private static Scene CreateMultiMeshScene()
    {
        Scene scene = new("MultiMeshScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });

        Mesh meshA = model.AddNode(new Mesh { Name = "BoxA" });
        meshA.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        meshA.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        Mesh meshB = model.AddNode(new Mesh { Name = "BoxB" });
        meshB.Positions = new DataBuffer<float>(
        [
            2f, 0f, 0f,
            3f, 0f, 0f,
            2f, 1f, 0f,
        ], 1, 3);
        meshB.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        return scene;
    }

    private static Scene CreateSceneWithMaterials()
    {
        Scene scene = new("MaterialScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });

        Material mat = model.AddNode(new Material("PBRMaterial")
        {
            DiffuseColor = new Vector4(0.8f, 0.2f, 0.1f, 1f),
            MetallicColor = new Vector4(0.9f, 0.9f, 0.9f, 1f),
            RoughnessColor = new Vector4(0.3f, 0.3f, 0.3f, 1f),
        });

        Mesh mesh = model.AddNode(new Mesh { Name = "textured_mesh" });
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        mesh.Materials = [mat];

        return scene;
    }

    private static Scene CreateSkinnedScene()
    {
        Scene scene = new("SkinnedScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });

        // Create skeleton
        SkeletonBone skeleton = scene.RootNode.AddNode(new SkeletonBone("TestSkeleton"));
        SkeletonBone root = skeleton.AddNode(new SkeletonBone("Root"));
        root.BindTransform.SetLocalPosition(Vector3.Zero);
        root.BindTransform.SetLocalRotation(Quaternion.Identity);

        SkeletonBone child = root.AddNode(new SkeletonBone("Child"));
        child.BindTransform.SetLocalPosition(new Vector3(0f, 1f, 0f));
        child.BindTransform.SetLocalRotation(Quaternion.Identity);

        // Create skinned mesh
        Mesh mesh = model.AddNode(new Mesh { Name = "skinned_mesh" });
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
            1f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2, 1, 3, 2], 1, 1);

        // 4 influences per vertex (JOINTS_0 = VEC4)
        mesh.BoneIndices = new DataBuffer<int>(
        [
            0, 0, 0, 0, // vertex 0: bone 0
            0, 0, 0, 0, // vertex 1: bone 0
            1, 0, 0, 0, // vertex 2: bone 1
            1, 0, 0, 0, // vertex 3: bone 1
        ], 4, 1);

        mesh.BoneWeights = new DataBuffer<float>(
        [
            1f, 0f, 0f, 0f,
            1f, 0f, 0f, 0f,
            1f, 0f, 0f, 0f,
            1f, 0f, 0f, 0f,
        ], 4, 1);

        SkeletonBone[] bones = skeleton.EnumerateHierarchy<SkeletonBone>().ToArray();
        mesh.SetSkinBinding(bones, [Matrix4x4.Identity, Matrix4x4.Identity]);
        mesh.SkinBindingName = skeleton.Name;

        return scene;
    }

    private static Scene CreateAnimatedScene()
    {
        Scene scene = new("AnimatedScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });

        // Create skeleton
        SkeletonBone skeleton = scene.RootNode.AddNode(new SkeletonBone("Armature"));
        SkeletonBone root = skeleton.AddNode(new SkeletonBone("Root"));
        root.BindTransform.SetLocalPosition(Vector3.Zero);
        root.BindTransform.SetLocalRotation(Quaternion.Identity);

        SkeletonBone leg = root.AddNode(new SkeletonBone("Leg"));
        leg.BindTransform.SetLocalPosition(new Vector3(0f, -1f, 0f));
        leg.BindTransform.SetLocalRotation(Quaternion.Identity);

        // Simple mesh
        Mesh mesh = model.AddNode(new Mesh { Name = "body" });
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        // Create animation
        SkeletonAnimation anim = new("WalkCycle", skeleton);
        anim.TransformType = TransformType.Absolute;

        SkeletonAnimationTrack rootTrack = new("Root")
        {
            TransformSpace = TransformSpace.Local,
            TransformType = TransformType.Absolute
        };
        rootTrack.AddTranslationFrame(0f, new Vector3(0f, 0f, 0f));
        rootTrack.AddTranslationFrame(0.5f, new Vector3(1f, 0f, 0f));
        rootTrack.AddTranslationFrame(1f, new Vector3(0f, 0f, 0f));

        SkeletonAnimationTrack legTrack = new("Leg")
        {
            TransformSpace = TransformSpace.Local,
            TransformType = TransformType.Absolute
        };
        legTrack.AddRotationFrame(0f, Quaternion.Identity);
        legTrack.AddRotationFrame(0.5f, Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 4));
        legTrack.AddRotationFrame(1f, Quaternion.Identity);

        anim.Tracks.Add(rootTrack);
        anim.Tracks.Add(legTrack);

        scene.RootNode.AddNode(anim);

        return scene;
    }

    private static SceneTranslatorManager CreateManager()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new GltfTranslator());
        return manager;
    }

    private static byte[] WriteSceneToGlb(SceneTranslatorManager manager, Scene scene)
    {
        using MemoryStream stream = new();
        manager.Write(stream, "test.glb", scene, new SceneTranslatorOptions(), token: null);
        return stream.ToArray();
    }

    private static Scene ReadSceneFromGlb(SceneTranslatorManager manager, byte[] data)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, "test.glb", new SceneTranslatorOptions(), token: null);
    }

    private static void AssertSceneStructureEquivalent(Scene expected, Scene actual)
    {
        Mesh[] expectedMeshes = expected.GetDescendants<Mesh>();
        Mesh[] actualMeshes = actual.GetDescendants<Mesh>();
        Assert.Equal(expectedMeshes.Length, actualMeshes.Length);

        for (int i = 0; i < expectedMeshes.Length; i++)
        {
            Assert.Equal(expectedMeshes[i].VertexCount, actualMeshes[i].VertexCount);
            Assert.Equal(expectedMeshes[i].FaceCount, actualMeshes[i].FaceCount);

            for (int v = 0; v < expectedMeshes[i].VertexCount; v++)
            {
                AssertVector3Equal(
                    expectedMeshes[i].Positions!.GetVector3(v, 0),
                    actualMeshes[i].Positions!.GetVector3(v, 0),
                    1e-5f);
            }
        }
    }

    private static void AssertVector3Equal(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"Expected {expected} but received {actual}.");
    }

    private static void AssertVector2Equal(Vector2 expected, Vector2 actual, float tolerance)
    {
        Assert.True(Vector2.Distance(expected, actual) <= tolerance, $"Expected {expected} but received {actual}.");
    }
}
