using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.KaydaraFbx;

namespace RedFox.Tests.Graphics3D;

public sealed class FbxTranslatorTests
{
    [Fact]
    public void FbxTranslator_BinaryRoundTrip_PreservesMeshMaterialSkeletonShape()
    {
        Scene sourceScene = CreateSampleScene();
        SceneTranslatorManager manager = CreateManager();

        byte[] firstWrite = WriteScene(manager, sourceScene, "sample.fbx");
        Scene firstLoad = ReadScene(manager, firstWrite, "sample.fbx");
        byte[] secondWrite = WriteScene(manager, firstLoad, "sample.fbx");
        Scene secondLoad = ReadScene(manager, secondWrite, "sample.fbx");

        AssertSceneShapeEquivalent(firstLoad, secondLoad);
    }

    [Fact]
    public void FbxTranslator_AsciiRoundTrip_PreservesMeshMaterialSkeletonShape()
    {
        Scene sourceScene = CreateSampleScene();
        SceneTranslatorManager manager = CreateManager();

        byte[] firstWrite = WriteScene(manager, sourceScene, "sample.ascii.fbx");
        Scene firstLoad = ReadScene(manager, firstWrite, "sample.ascii.fbx");
        byte[] secondWrite = WriteScene(manager, firstLoad, "sample.ascii.fbx");
        Scene secondLoad = ReadScene(manager, secondWrite, "sample.ascii.fbx");

        AssertSceneShapeEquivalent(firstLoad, secondLoad);
    }

    [Fact]
    public void FbxTranslator_CanReadProvidedBinarySamples()
    {
        string samplesPath = GetSampleDirectory();
        if (string.IsNullOrWhiteSpace(samplesPath))
        {
            return;
        }

        string[] files = Directory.GetFiles(samplesPath, "*.fbx", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            return;
        }

        SceneTranslatorManager manager = CreateManager();
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            using FileStream stream = File.OpenRead(file);
            Scene scene = manager.Read(stream, file, new SceneTranslatorOptions(), token: null);

            Assert.NotNull(scene);
            Assert.NotNull(scene.RootNode);
            Assert.True(scene.GetDescendants<Mesh>().Length >= 0);
        }
    }

    [Fact]
    public void FbxSceneMapper_ImportScene_AppliesPostRotationInTransformStack()
    {
        FbxDocument document = new() { Format = FbxFormat.Binary, Version = 7400 };
        FbxNode objectsNode = new("Objects");
        FbxNode connectionsNode = new("Connections");
        document.Nodes.Add(objectsNode);
        document.Nodes.Add(connectionsNode);

        FbxNode modelNode = new("Model");
        modelNode.Properties.Add(new FbxProperty('L', 1001L));
        modelNode.Properties.Add(new FbxProperty('S', "stack_node\0\u0001Model"));
        modelNode.Properties.Add(new FbxProperty('S', "Null"));

        FbxNode properties = modelNode.AddChild("Properties70");
        AddVectorProperty(properties, "Lcl Translation", "Lcl Translation", Vector3.Zero);
        AddVectorProperty(properties, "Lcl Rotation", "Lcl Rotation", Vector3.Zero);
        AddVectorProperty(properties, "Lcl Scaling", "Lcl Scaling", Vector3.One);
        AddVectorProperty(properties, "PreRotation", "Vector3D", new Vector3(90f, 0f, 0f));
        AddVectorProperty(properties, "PostRotation", "Vector3D", new Vector3(90f, 0f, 0f));
        objectsNode.Children.Add(modelNode);

        FbxNode connectionNode = new("C");
        connectionNode.Properties.Add(new FbxProperty('S', "OO"));
        connectionNode.Properties.Add(new FbxProperty('L', 1001L));
        connectionNode.Properties.Add(new FbxProperty('L', 0L));
        connectionsNode.Children.Add(connectionNode);

        Scene scene = FbxSceneMapper.ImportScene(document, "stack-test");
        Model[] models = scene.GetDescendants<Model>();
        Assert.NotEmpty(models);

        Quaternion rotation = models[0].GetBindLocalRotation();
        float angle = 2f * MathF.Acos(Math.Clamp(MathF.Abs(rotation.W), 0f, 1f));
        Assert.True(angle < 0.01f, "Expected PreRotation and PostRotation to cancel out to identity.");
    }

    [Fact]
    public void FbxTranslator_Write_ClusterContainsTransformAndTransformLink()
    {
        Scene sourceScene = CreateSampleScene();
        Mesh mesh = sourceScene.GetDescendants<Mesh>()[0];
        mesh.BindTransform.LocalPosition = new Vector3(3f, -2f, 5f);
        mesh.BindTransform.LocalRotation = Quaternion.CreateFromYawPitchRoll(0.35f, -0.2f, 0.1f);
        mesh.BindTransform.Scale = new Vector3(1.2f, 0.8f, 1.1f);

        byte[] data;
        using (MemoryStream writeStream = new())
        {
            SceneTranslatorManager manager = CreateManager();
            manager.Write(writeStream, "cluster_test.fbx", sourceScene, new SceneTranslatorOptions(), token: null);
            data = writeStream.ToArray();
        }

        using MemoryStream readStream = new(data, writable: false);
        FbxDocument document = FbxDocumentIO.Read(readStream);
        FbxNode objectsNode = Assert.Single(document.NodesNamed("Objects"));
        FbxNode[] clusterNodes = objectsNode.Children.Where(static n => n.Name == "Deformer" && n.Properties.Count > 2 && n.Properties[2].AsString() == "Cluster").ToArray();
        Assert.NotEmpty(clusterNodes);

        for (int i = 0; i < clusterNodes.Length; i++)
        {
            double[] transform = FbxSceneMapper.GetNodeArray<double>(clusterNodes[i], "Transform");
            double[] transformLink = FbxSceneMapper.GetNodeArray<double>(clusterNodes[i], "TransformLink");
            Assert.Equal(16, transform.Length);
            Assert.Equal(16, transformLink.Length);
        }
    }

    [Fact]
    public void FbxRotation_EulerRoundTrip_PreservesQuaternion()
    {
        Quaternion expected = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.61f, -0.37f, 0.28f));
        Vector3 euler = FbxRotation.ToEulerDegreesXyz(expected);
        Quaternion actual = FbxRotation.FromEulerDegreesXyz(euler);
        float dot = MathF.Abs(Quaternion.Dot(expected, actual));
        Assert.True(dot > 0.999f, $"Expected FBX Euler round-trip to preserve rotation. Dot={dot}");
    }

    private static SceneTranslatorManager CreateManager()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new FbxTranslator());
        return manager;
    }

    private static byte[] WriteScene(SceneTranslatorManager manager, Scene scene, string sourcePath)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, scene, new SceneTranslatorOptions(), token: null);
        return stream.ToArray();
    }

    private static Scene ReadScene(SceneTranslatorManager manager, byte[] data, string sourcePath)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, sourcePath, new SceneTranslatorOptions(), token: null);
    }

    private static Scene CreateSampleScene()
    {
        Scene scene = new("FbxSample");

        Skeleton skeleton = scene.RootNode.AddNode(new Skeleton("Armature"));
        SkeletonBone rootBone = skeleton.AddNode(new SkeletonBone("root"));
        rootBone.BindTransform.LocalPosition = new Vector3(0f, 0f, 0f);
        rootBone.BindTransform.LocalRotation = Quaternion.Identity;
        rootBone.BindTransform.Scale = Vector3.One;

        SkeletonBone childBone = rootBone.AddNode(new SkeletonBone("child"));
        childBone.BindTransform.LocalPosition = new Vector3(0f, 1f, 0f);
        childBone.BindTransform.LocalRotation = Quaternion.Identity;
        childBone.BindTransform.Scale = Vector3.One;

        Model model = scene.RootNode.AddNode(new Model { Name = "ModelRoot" });
        Mesh mesh = model.AddNode(new Mesh { Name = "mesh_0" });

        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            1f, 1f, 0f,
            0f, 1f, 0f,
        ], 1, 3);

        mesh.Normals = new DataBuffer<float>(
        [
            0f, 0f, 1f,
            0f, 0f, 1f,
            0f, 0f, 1f,
            0f, 0f, 1f,
        ], 1, 3);

        mesh.UVLayers = new DataBuffer<float>(
        [
            0f, 0f, 0.05f, 0.05f,
            1f, 0f, 0.95f, 0.05f,
            1f, 1f, 0.95f, 0.95f,
            0f, 1f, 0.05f, 0.95f,
        ], 2, 2);

        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2, 0, 2, 3], 1, 1);
        mesh.BoneIndices = new DataBuffer<ushort>(
        [
            0, 1,
            0, 1,
            0, 1,
            0, 1,
        ], 2, 1);

        mesh.BoneWeights = new DataBuffer<float>(
        [
            0.75f, 0.25f,
            0.60f, 0.40f,
            0.35f, 0.65f,
            0.80f, 0.20f,
        ], 2, 1);

        mesh.SetSkinBinding([rootBone, childBone]);

        Material material = model.AddNode(new Material("material_0")
        {
            DiffuseColor = new Vector4(0.7f, 0.6f, 0.5f, 1f),
            SpecularColor = new Vector4(0.2f, 0.2f, 0.2f, 1f),
            EmissiveColor = new Vector4(0.05f, 0.01f, 0.0f, 1f),
        });

        mesh.Materials = [material];
        return scene;
    }

    private static void AssertSceneShapeEquivalent(Scene expected, Scene actual)
    {
        Mesh[] expectedMeshes = expected.GetDescendants<Mesh>();
        Mesh[] actualMeshes = actual.GetDescendants<Mesh>();

        Assert.Equal(expectedMeshes.Length, actualMeshes.Length);

        for (int meshIndex = 0; meshIndex < expectedMeshes.Length; meshIndex++)
        {
            Mesh expectedMesh = expectedMeshes[meshIndex];
            Mesh actualMesh = actualMeshes[meshIndex];

            Assert.Equal(expectedMesh.VertexCount, actualMesh.VertexCount);
            Assert.Equal(expectedMesh.FaceCount, actualMesh.FaceCount);
            Assert.Equal(expectedMesh.UVLayerCount, actualMesh.UVLayerCount);
            Assert.Equal(expectedMesh.HasSkinning, actualMesh.HasSkinning);

            int expectedSkinBoneCount = expectedMesh.SkinnedBones?.Count ?? 0;
            int actualSkinBoneCount = actualMesh.SkinnedBones?.Count ?? 0;
            Assert.Equal(expectedSkinBoneCount, actualSkinBoneCount);
        }

        Material[] expectedMaterials = expected.GetDescendants<Material>();
        Material[] actualMaterials = actual.GetDescendants<Material>();
        Assert.Equal(expectedMaterials.Length, actualMaterials.Length);

        SkeletonBone[] expectedBones = expected.GetDescendants<SkeletonBone>();
        SkeletonBone[] actualBones = actual.GetDescendants<SkeletonBone>();
        Assert.Equal(expectedBones.Length, actualBones.Length);
    }

    private static string GetSampleDirectory()
    {
        string explicitPath = "/home/philipmaher/FBX";
        if (Directory.Exists(explicitPath))
        {
            return explicitPath;
        }

        string? environmentPath = Environment.GetEnvironmentVariable("REDFOX_FBX_DIR");
        if (!string.IsNullOrWhiteSpace(environmentPath) && Directory.Exists(environmentPath))
        {
            return environmentPath;
        }

        return string.Empty;
    }

    private static void AddVectorProperty(FbxNode properties, string propertyName, string propertyType, Vector3 value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('S', "A"));
        property.Properties.Add(new FbxProperty('D', value.X));
        property.Properties.Add(new FbxProperty('D', value.Y));
        property.Properties.Add(new FbxProperty('D', value.Z));
    }
}
