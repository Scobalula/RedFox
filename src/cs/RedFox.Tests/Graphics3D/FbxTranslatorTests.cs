using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.KaydaraFbx;
using RedFox.Graphics3D.MayaAscii;
using RedFox.Graphics3D.Semodel;

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
    public void FbxTranslator_BinaryRoundTrip_PreservesBoneWorldPositions()
    {
        Scene sourceScene = CreateSampleScene();

        // Give bones non-trivial transforms to exercise the full pipeline.
        // The Armature skeleton is left at identity (the common FBX case where the
        // export adds PreRotation -90° for Z-up to Y-up conversion).
        SkeletonBone rootBone = sourceScene.GetDescendants<SkeletonBone>().First(static b => b.Name == "root");
        rootBone.BindTransform.LocalPosition = new Vector3(1f, 0.5f, -0.25f);
        rootBone.BindTransform.LocalRotation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(-0.2f, 0.4f, 0.15f));

        SkeletonBone childBone = sourceScene.GetDescendants<SkeletonBone>().First(static b => b.Name == "child");
        childBone.BindTransform.LocalPosition = new Vector3(-0.5f, 1.2f, 0.3f);
        childBone.BindTransform.LocalRotation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.25f, -0.3f, 0.2f));

        // Capture pre-export world positions.
        Vector3 rootBoneWorldBefore = rootBone.GetBindWorldPosition();
        Vector3 childBoneWorldBefore = childBone.GetBindWorldPosition();
        Quaternion rootBoneRotBefore = rootBone.GetBindWorldRotation();
        Quaternion childBoneRotBefore = childBone.GetBindWorldRotation();

        // Rebuild IBMs from current transforms (no explicit IBMs).
        Mesh mesh = sourceScene.GetDescendants<Mesh>()[0];
        mesh.RebuildInverseBindMatrices();
        Vector3 meshWorldBefore = mesh.GetBindWorldPosition();

        SceneTranslatorManager manager = CreateManager();
        byte[] data = WriteScene(manager, sourceScene, "transform_rt.fbx");
        Scene reloaded = ReadScene(manager, data, "transform_rt.fbx");

        SkeletonBone reloadedRoot = reloaded.GetDescendants<SkeletonBone>().First(static b => b.Name == "root");
        SkeletonBone reloadedChild = reloaded.GetDescendants<SkeletonBone>().First(static b => b.Name == "child");
        Mesh reloadedMesh = reloaded.GetDescendants<Mesh>()[0];

        Assert.True(Vector3.Distance(rootBoneWorldBefore, reloadedRoot.GetBindWorldPosition()) < 0.01f,
            $"Root bone world position mismatch: expected {rootBoneWorldBefore}, got {reloadedRoot.GetBindWorldPosition()}");
        Assert.True(Vector3.Distance(childBoneWorldBefore, reloadedChild.GetBindWorldPosition()) < 0.01f,
            $"Child bone world position mismatch: expected {childBoneWorldBefore}, got {reloadedChild.GetBindWorldPosition()}");
        Assert.True(MathF.Abs(Quaternion.Dot(rootBoneRotBefore, reloadedRoot.GetBindWorldRotation())) > 0.999f,
            $"Root bone world rotation mismatch: expected {rootBoneRotBefore}, got {reloadedRoot.GetBindWorldRotation()}");
        Assert.True(MathF.Abs(Quaternion.Dot(childBoneRotBefore, reloadedChild.GetBindWorldRotation())) > 0.999f,
            $"Child bone world rotation mismatch: expected {childBoneRotBefore}, got {reloadedChild.GetBindWorldRotation()}");
        Assert.True(Vector3.Distance(meshWorldBefore, reloadedMesh.GetBindWorldPosition()) < 0.01f,
            $"Mesh world position mismatch: expected {meshWorldBefore}, got {reloadedMesh.GetBindWorldPosition()}");

        // Double round-trip: write the reloaded scene again and verify stability.
        byte[] data2 = WriteScene(manager, reloaded, "transform_rt2.fbx");
        Scene reloaded2 = ReadScene(manager, data2, "transform_rt2.fbx");

        SkeletonBone reloadedRoot2 = reloaded2.GetDescendants<SkeletonBone>().First(static b => b.Name == "root");
        SkeletonBone reloadedChild2 = reloaded2.GetDescendants<SkeletonBone>().First(static b => b.Name == "child");

        Assert.True(Vector3.Distance(rootBoneWorldBefore, reloadedRoot2.GetBindWorldPosition()) < 0.01f,
            $"Double RT root bone mismatch: expected {rootBoneWorldBefore}, got {reloadedRoot2.GetBindWorldPosition()}");
        Assert.True(Vector3.Distance(childBoneWorldBefore, reloadedChild2.GetBindWorldPosition()) < 0.01f,
            $"Double RT child bone mismatch: expected {childBoneWorldBefore}, got {reloadedChild2.GetBindWorldPosition()}");
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
        SceneNode importedNode = Assert.Single(scene.EnumerateChildren());

        Quaternion rotation = importedNode.GetBindLocalRotation();
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
    public void FbxSceneMapper_StripImportedRootBasisTransforms_ReconstructsBonesFromExplicitIbmUsingMeshWorld()
    {
        Scene scene = new("explicit-ibm-reconstruct");

        Group importedRoot = scene.RootNode.AddNode(new Group("ImportedRoot"));
        importedRoot.BindTransform.LocalPosition = Vector3.Zero;
        importedRoot.BindTransform.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, float.DegreesToRadians(-90f));
        importedRoot.BindTransform.Scale = Vector3.One;

        SkeletonBone skeleton = importedRoot.AddNode(new SkeletonBone("Rig"));
        SkeletonBone bone = skeleton.AddNode(new SkeletonBone("t7:Bone"));
        bone.BindTransform.LocalPosition = Vector3.Zero;
        bone.BindTransform.LocalRotation = Quaternion.Identity;
        bone.BindTransform.Scale = Vector3.One;

        Model model = importedRoot.AddNode(new Model { Name = "ModelRoot" });
        model.BindTransform.LocalPosition = new Vector3(2f, 0f, 0f);
        model.BindTransform.LocalRotation = Quaternion.Identity;
        model.BindTransform.Scale = Vector3.One;

        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.BoneIndices = new DataBuffer<ushort>(new ushort[] { 0 }, 1, 1);
        mesh.BoneWeights = new DataBuffer<float>([1f], 1, 1);

        Matrix4x4 desiredBoneWorld = Matrix4x4.CreateTranslation(5f, 0f, 0f);
        Matrix4x4 meshBindWorldAfterStrip = Matrix4x4.CreateTranslation(2f, 0f, 0f);
        Matrix4x4 expectedIbm = Matrix4x4.Invert(desiredBoneWorld, out Matrix4x4 invBoneWorld)
            ? meshBindWorldAfterStrip * invBoneWorld
            : Matrix4x4.Identity;

        mesh.SetSkinBinding([bone], [expectedIbm]);
        Assert.True(mesh.HasExplicitInverseBindMatrices);

        FbxSceneMapper.StripImportedRootBasisTransforms(scene.RootNode);

        Vector3 correctedBoneWorld = bone.GetBindWorldPosition();
        Assert.True(Vector3.Distance(correctedBoneWorld, new Vector3(5f, 0f, 0f)) < 0.0001f,
            $"Expected corrected bone world at <5,0,0>, got {correctedBoneWorld}");

        // IBMs are rebuilt from the current scene graph (meshBindWorld × inv(boneBindWorld))
        // so they are in the same coordinate space as the bone transforms.
        Assert.False(mesh.HasExplicitInverseBindMatrices);
        Assert.NotNull(mesh.InverseBindMatrices);
        Assert.Single(mesh.InverseBindMatrices!);

        // Rebuilt IBM should equal meshBindWorld × inv(boneBindWorld).
        Matrix4x4 meshWorld = mesh.GetBindWorldMatrix();
        Matrix4x4 rebuiltExpected = Matrix4x4.Invert(Matrix4x4.CreateTranslation(5f, 0f, 0f), out Matrix4x4 invBone)
            ? meshWorld * invBone
            : Matrix4x4.Identity;
        AssertMatrixApproximatelyEqual(rebuiltExpected, mesh.InverseBindMatrices![0]);
    }

    [Fact]
    public void FbxSceneMapper_StripImportedRootBasisTransforms_PreservesImportedLiveBonePoseWhenBindHintsApplied()
    {
        Scene scene = new("bind-vs-live");

        Group importedRoot = scene.RootNode.AddNode(new Group("ImportedRoot"));
        importedRoot.BindTransform.LocalPosition = Vector3.Zero;
        importedRoot.BindTransform.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, float.DegreesToRadians(-90f));
        importedRoot.BindTransform.Scale = Vector3.One;

        SkeletonBone skeleton = importedRoot.AddNode(new SkeletonBone("Rig"));
        SkeletonBone bone = skeleton.AddNode(new SkeletonBone("t7:Bone"));
        bone.BindTransform.LocalPosition = new Vector3(7f, 0f, 0f); // Imported/model local (live pose)
        bone.BindTransform.LocalRotation = Quaternion.Identity;
        bone.BindTransform.Scale = Vector3.One;

        Model model = importedRoot.AddNode(new Model { Name = "ModelRoot" });
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.BoneIndices = new DataBuffer<ushort>(new ushort[] { 0 }, 1, 1);
        mesh.BoneWeights = new DataBuffer<float>([1f], 1, 1);

        // Bind pose hint says the bone bind world is at x=5, while imported local is at x=7.
        Matrix4x4 bindWorld = Matrix4x4.CreateTranslation(5f, 0f, 0f);
        Matrix4x4 explicitIbm = Matrix4x4.CreateTranslation(-5f, 0f, 0f);
        mesh.SetSkinBinding([bone], [explicitIbm]);

        Dictionary<SkeletonBone, Matrix4x4> bindHints = new()
        {
            [bone] = bindWorld,
        };

        FbxSceneMapper.StripImportedRootBasisTransforms(scene.RootNode, bindHints);

        Vector3 bindWorldPos = bone.GetBindWorldPosition();
        Assert.True(Vector3.Distance(bindWorldPos, new Vector3(5f, 0f, 0f)) < 0.0001f,
            $"Expected bind world at <5,0,0>, got {bindWorldPos}");

        // Live overrides preserved — active world reflects the imported live pose.
        Vector3 activeWorldPos = bone.GetActiveWorldPosition();
        Assert.True(Vector3.Distance(activeWorldPos, new Vector3(7f, 0f, 0f)) < 0.0001f,
            $"Expected active world at <7,0,0>, got {activeWorldPos}");

        // Skinning with the active pose produces the correct offset.
        Matrix4x4 skinTransform = mesh.InverseBindMatrices![0] * bone.GetActiveWorldMatrix();
        Vector3 skinned = Vector3.Transform(Vector3.Zero, skinTransform);
        Assert.True(Vector3.Distance(skinned, new Vector3(2f, 0f, 0f)) < 0.0001f,
            $"Expected skinned offset at <2,0,0>, got {skinned}");
    }

    [Fact]
    public void FbxSceneMapper_StripImportedRootBasisTransforms_PreservesSmallLiveOverrides()
    {
        // Regression: small live displacements (< 1 unit) must not be lost.
        Scene scene = new("small-live-override");

        Group importedRoot = scene.RootNode.AddNode(new Group("ImportedRoot"));
        importedRoot.BindTransform.LocalPosition = Vector3.Zero;
        importedRoot.BindTransform.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, float.DegreesToRadians(-90f));
        importedRoot.BindTransform.Scale = Vector3.One;

        SkeletonBone skeleton = importedRoot.AddNode(new SkeletonBone("Rig"));
        SkeletonBone bone = skeleton.AddNode(new SkeletonBone("SmallOffsetBone"));
        bone.BindTransform.LocalPosition = new Vector3(5.3f, 0f, 0f);
        bone.BindTransform.LocalRotation = Quaternion.Identity;
        bone.BindTransform.Scale = Vector3.One;

        Model model = importedRoot.AddNode(new Model { Name = "ModelRoot" });
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.BoneIndices = new DataBuffer<ushort>(new ushort[] { 0 }, 1, 1);
        mesh.BoneWeights = new DataBuffer<float>([1f], 1, 1);

        Matrix4x4 bindWorld = Matrix4x4.CreateTranslation(5f, 0f, 0f);
        Matrix4x4 explicitIbm = Matrix4x4.CreateTranslation(-5f, 0f, 0f);
        mesh.SetSkinBinding([bone], [explicitIbm]);

        Dictionary<SkeletonBone, Matrix4x4> bindHints = new()
        {
            [bone] = bindWorld,
        };

        FbxSceneMapper.StripImportedRootBasisTransforms(scene.RootNode, bindHints);

        Vector3 bindWorldPos = bone.GetBindWorldPosition();
        Assert.True(Vector3.Distance(bindWorldPos, new Vector3(5f, 0f, 0f)) < 0.0001f,
            $"Expected bind world at <5,0,0>, got {bindWorldPos}");

        // Live overrides preserved — active world reflects the imported live pose.
        Vector3 activeWorldPos = bone.GetActiveWorldPosition();
        Assert.True(Vector3.Distance(activeWorldPos, new Vector3(5.3f, 0f, 0f)) < 0.001f,
            $"Expected active world at <5.3,0,0>, got {activeWorldPos}");

        // Skinning with the active pose produces the correct small offset.
        Matrix4x4 skinTransform = mesh.InverseBindMatrices![0] * bone.GetActiveWorldMatrix();
        Vector3 skinned = Vector3.Transform(Vector3.Zero, skinTransform);
        Assert.True(Vector3.Distance(skinned, new Vector3(0.3f, 0f, 0f)) < 0.001f,
            $"Expected skinned offset at <0.3,0,0>, got {skinned}");
    }

    [Fact]
    public void FbxTranslator_Write_UsesExactBindLocalTransforms()
    {
        Scene sourceScene = CreateSampleScene();
        SkeletonBone skeleton = sourceScene.GetDescendants<SkeletonBone>()[0];
        skeleton.BindTransform.LocalPosition = new Vector3(4f, -3f, 2f);
        skeleton.BindTransform.LocalRotation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.5f, -0.25f, 0.1f));
        skeleton.BindTransform.Scale = new Vector3(1.1f, 0.9f, 1.25f);

        SkeletonBone rootBone = sourceScene.GetDescendants<SkeletonBone>().First(static bone => bone.Name == "root");
        rootBone.BindTransform.LocalPosition = new Vector3(1.5f, 2f, -0.75f);
        rootBone.BindTransform.LocalRotation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(-0.2f, 0.35f, 0.4f));
        rootBone.BindTransform.Scale = new Vector3(1.0f, 1.15f, 0.95f);

        SkeletonBone childBone = sourceScene.GetDescendants<SkeletonBone>().First(static bone => bone.Name == "child");
        childBone.BindTransform.LocalPosition = new Vector3(-0.35f, 1.8f, 0.45f);
        childBone.BindTransform.LocalRotation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.3f, -0.4f, 0.25f));
        childBone.BindTransform.Scale = new Vector3(0.85f, 1.2f, 1.05f);

        using MemoryStream writeStream = new();
        CreateManager().Write(writeStream, "local_pose_test.fbx", sourceScene, new SceneTranslatorOptions(), token: null);

        writeStream.Position = 0;
        FbxDocument document = FbxDocumentIO.Read(writeStream);
        FbxNode objectsNode = Assert.Single(document.NodesNamed("Objects"));
        FbxNode childBoneNode = Assert.Single(objectsNode.Children, static node => node.Name == "Model" && FbxSceneMapper.GetNodeObjectName(node) == "child");
        FbxNode properties = Assert.Single(childBoneNode.ChildrenNamed("Properties70"));

        Vector3 exportedTranslation = FbxSceneMapper.GetPropertyVector3(properties, "Lcl Translation", Vector3.Zero);
        Vector3 exportedScale = FbxSceneMapper.GetPropertyVector3(properties, "Lcl Scaling", Vector3.One);
        Vector3 exportedEuler = FbxSceneMapper.GetPropertyVector3(properties, "Lcl Rotation", Vector3.Zero);
        Vector3 exportedPreRotation = FbxSceneMapper.GetPropertyVector3(properties, "PreRotation", Vector3.Zero);
        Quaternion exportedRotation = FbxRotation.FromEulerDegreesXyz(exportedEuler) * FbxRotation.FromEulerDegreesXyz(exportedPreRotation);

        AssertVectorApproximatelyEqual(childBone.GetBindLocalPosition(), exportedTranslation);
        AssertVectorApproximatelyEqual(childBone.GetBindLocalScale(), exportedScale);
        Assert.True(MathF.Abs(Quaternion.Dot(childBone.GetBindLocalRotation(), exportedRotation)) > 0.999f);
    }

    [Fact]
    public void FbxTranslator_Write_UsesLiveModelTransformsButPreservesBindClusterMatrices()
    {
        Scene scene = new("live-vs-bind-export");
        SkeletonBone skeleton = scene.RootNode.AddNode(new SkeletonBone("Rig"));
        SkeletonBone bone = skeleton.AddNode(new SkeletonBone("t7:Bone"));
        bone.BindTransform.LocalPosition = new Vector3(5f, 0f, 0f);
        bone.BindTransform.LocalRotation = Quaternion.Identity;
        bone.BindTransform.Scale = Vector3.One;
        bone.LiveTransform.LocalPosition = new Vector3(7f, 0f, 0f);
        bone.LiveTransform.LocalRotation = Quaternion.Identity;
        bone.LiveTransform.Scale = Vector3.One;

        Model model = scene.RootNode.AddNode(new Model { Name = "ModelRoot" });
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.BoneIndices = new DataBuffer<ushort>(new ushort[] { 0 }, 1, 1);
        mesh.BoneWeights = new DataBuffer<float>([1f], 1, 1);
        mesh.SetSkinBinding([bone], [Matrix4x4.CreateTranslation(-5f, 0f, 0f)]);

        using MemoryStream writeStream = new();
        CreateManager().Write(writeStream, "live_bind_export.fbx", scene, new SceneTranslatorOptions(), token: null);

        writeStream.Position = 0;
        FbxDocument document = FbxDocumentIO.Read(writeStream);
        FbxNode objectsNode = Assert.Single(document.NodesNamed("Objects"));
        FbxNode boneModel = Assert.Single(objectsNode.Children, static node => node.Name == "Model" && FbxSceneMapper.GetNodeObjectName(node).EndsWith("Bone", StringComparison.Ordinal));
        FbxNode boneProps = Assert.Single(boneModel.ChildrenNamed("Properties70"));
        Vector3 exportedBoneTranslation = FbxSceneMapper.GetPropertyVector3(boneProps, "Lcl Translation", Vector3.Zero);
        Assert.True(Vector3.Distance(exportedBoneTranslation, new Vector3(7f, 0f, 0f)) < 0.0001f,
            $"Expected exported live translation at <7,0,0>, got {exportedBoneTranslation}");

        FbxNode clusterNode = Assert.Single(objectsNode.Children, static node => node.Name == "Deformer" && node.Properties.Count > 2 && node.Properties[2].AsString() == "Cluster");
        Matrix4x4 transformLink = FbxSkinningMapper.ReadNodeMatrix(clusterNode, "TransformLink");
        Vector3 transformLinkTranslation = new(transformLink.M41, transformLink.M42, transformLink.M43);
        Assert.True(Vector3.Distance(transformLinkTranslation, new Vector3(5f, 0f, 0f)) < 0.0001f,
            $"Expected bind TransformLink translation at <5,0,0>, got {transformLinkTranslation}");
    }

    [Fact]
    public void FbxTranslator_Write_UsesLiveModelTransformsForNonT7BonesAndPreservesBindClusterMatrices()
    {
        Scene scene = new("live-vs-bind-export-non-t7");
        SkeletonBone skeleton = scene.RootNode.AddNode(new SkeletonBone("Rig"));
        SkeletonBone bone = skeleton.AddNode(new SkeletonBone("Bone"));
        bone.BindTransform.LocalPosition = new Vector3(5f, 0f, 0f);
        bone.BindTransform.LocalRotation = Quaternion.Identity;
        bone.BindTransform.Scale = Vector3.One;
        bone.LiveTransform.LocalPosition = new Vector3(7f, 0f, 0f);
        bone.LiveTransform.LocalRotation = Quaternion.Identity;
        bone.LiveTransform.Scale = Vector3.One;

        Model model = scene.RootNode.AddNode(new Model { Name = "ModelRoot" });
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f], 1, 3);
        mesh.BoneIndices = new DataBuffer<ushort>(new ushort[] { 0 }, 1, 1);
        mesh.BoneWeights = new DataBuffer<float>([1f], 1, 1);
        mesh.SetSkinBinding([bone], [Matrix4x4.CreateTranslation(-5f, 0f, 0f)]);

        using MemoryStream writeStream = new();
        CreateManager().Write(writeStream, "live_bind_export_non_t7.fbx", scene, new SceneTranslatorOptions(), token: null);

        writeStream.Position = 0;
        FbxDocument document = FbxDocumentIO.Read(writeStream);
        FbxNode objectsNode = Assert.Single(document.NodesNamed("Objects"));
        FbxNode boneModel = Assert.Single(objectsNode.Children, static node => node.Name == "Model" && FbxSceneMapper.GetNodeObjectName(node) == "Bone");
        FbxNode boneProps = Assert.Single(boneModel.ChildrenNamed("Properties70"));
        Vector3 exportedBoneTranslation = FbxSceneMapper.GetPropertyVector3(boneProps, "Lcl Translation", Vector3.Zero);
        Assert.True(Vector3.Distance(exportedBoneTranslation, new Vector3(7f, 0f, 0f)) < 0.0001f,
            $"Expected exported live translation at <7,0,0>, got {exportedBoneTranslation}");

        FbxNode clusterNode = Assert.Single(objectsNode.Children, static node => node.Name == "Deformer" && node.Properties.Count > 2 && node.Properties[2].AsString() == "Cluster");
        Matrix4x4 transformLink = FbxSkinningMapper.ReadNodeMatrix(clusterNode, "TransformLink");
        Vector3 transformLinkTranslation = new(transformLink.M41, transformLink.M42, transformLink.M43);
        Assert.True(Vector3.Distance(transformLinkTranslation, new Vector3(5f, 0f, 0f)) < 0.0001f,
            $"Expected bind TransformLink translation at <5,0,0>, got {transformLinkTranslation}");
    }

    [Fact]
    public void FbxTranslator_Write_BindPoseIncludesMeshAndBoneAncestors()
    {
        Scene sourceScene = CreateSampleScene();
        byte[] data = WriteScene(CreateManager(), sourceScene, "bind_pose_ancestors.fbx");

        using MemoryStream readStream = new(data, writable: false);
        FbxDocument document = FbxDocumentIO.Read(readStream);
        FbxNode objectsNode = Assert.Single(document.NodesNamed("Objects"));
        Dictionary<long, FbxNode> objectsById = objectsNode.Children.Where(static node => node.Properties.Count > 0).ToDictionary(static node => node.Properties[0].AsInt64());

        FbxNode poseNode = Assert.Single(objectsNode.Children, static node => node.Name == "Pose");
        FbxNode[] poseEntries = poseNode.Children.Where(static child => child.Name == "PoseNode").ToArray();
        string[] poseObjectNames = poseEntries.Select(entry => entry.FirstChild("Node")!.Properties[0].AsInt64())
            .Select(id => FbxSceneMapper.GetNodeObjectName(objectsById[id]))
            .ToArray();

        Assert.Contains("mesh_0", poseObjectNames);
        Assert.Contains("ModelRoot", poseObjectNames);
        Assert.Contains("Armature", poseObjectNames);
        Assert.Contains("root", poseObjectNames);
        Assert.Contains("child", poseObjectNames);
        Assert.Equal(poseEntries.Length, poseObjectNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void FbxTranslator_Write_AddsCompatibilityNodesAndBinaryFooter()
    {
        Scene sourceScene = CreateSampleScene();
        byte[] data = WriteScene(CreateManager(), sourceScene, "compatibility_test.fbx");

        using MemoryStream readStream = new(data, writable: false);
        FbxDocument document = FbxDocumentIO.Read(readStream);

        Assert.Single(document.NodesNamed("Documents"));
        Assert.Single(document.NodesNamed("References"));
        Assert.Single(document.NodesNamed("Definitions"));
        Assert.Single(document.NodesNamed("Takes"));
        Assert.Single(document.NodesNamed("FileId"));
        Assert.Single(document.NodesNamed("CreationTime"));
        Assert.Equal(7700, document.Version);

        FbxNode documents = Assert.Single(document.NodesNamed("Documents"));
        FbxNode exportedDocument = Assert.Single(documents.ChildrenNamed("Document"));
        Assert.Equal(string.Empty, exportedDocument.Properties[1].AsString());
        Assert.Equal("Scene", exportedDocument.Properties[2].AsString());
        FbxNode documentProperties = Assert.Single(exportedDocument.ChildrenNamed("Properties70"));
        FbxNode activeAnimStackName = Assert.Single(documentProperties.Children, static child => child.Name == "P" && child.Properties.Count > 0 && child.Properties[0].AsString() == "ActiveAnimStackName");
        Assert.Equal("Take 001", activeAnimStackName.Properties[4].AsString());

        FbxNode header = Assert.Single(document.NodesNamed("FBXHeaderExtension"));
        Assert.Single(header.ChildrenNamed("SceneInfo"));
        Assert.NotNull(header.FirstChild("OtherFlags"));

        FbxNode definitions = Assert.Single(document.NodesNamed("Definitions"));
        int definitionCount = (int)Assert.Single(definitions.ChildrenNamed("Count")).Properties[0].AsInt64();
        FbxNode[] objectTypes = definitions.Children.Where(static child => child.Name == "ObjectType").ToArray();
        Assert.Contains(objectTypes, static child => child.Properties.Count > 0 && child.Properties[0].AsString() == "GlobalSettings");
        Assert.Contains(definitions.Children, static child => child.Name == "ObjectType" && child.Properties.Count > 0 && child.Properties[0].AsString() == "Model" && child.FirstChild("PropertyTemplate") is not null);
        Assert.Contains(objectTypes, static child => child.Properties.Count > 0 && child.Properties[0].AsString() == "AnimationStack");
        Assert.Contains(objectTypes, static child => child.Properties.Count > 0 && child.Properties[0].AsString() == "AnimationLayer");

        FbxNode objects = Assert.Single(document.NodesNamed("Objects"));
        Assert.Equal(objects.Children.Count + 1, definitionCount);
        Assert.Single(objects.Children, static child => child.Name == "AnimationStack");
        Assert.Single(objects.Children, static child => child.Name == "AnimationLayer");

        FbxNode takes = Assert.Single(document.NodesNamed("Takes"));
        Assert.Equal("Take 001", Assert.Single(takes.ChildrenNamed("Current")).Properties[0].AsString());
        Assert.Single(takes.ChildrenNamed("Take"));

        Assert.True(data.Length > FbxDocumentSerializer.FooterMagic.Length);
        ReadOnlySpan<byte> footerMagic = data.AsSpan(data.Length - FbxDocumentSerializer.FooterMagic.Length);
        Assert.True(footerMagic.SequenceEqual(FbxDocumentSerializer.FooterMagic));

        FbxNode fileIdNode = Assert.Single(document.NodesNamed("FileId"));
        byte[] fileId = Assert.IsType<byte[]>(fileIdNode.Properties[0].Value);
        byte[] footerId = ReadFooterId(data);
        Assert.Equal(FbxDocumentSerializer.CreateFooterId(fileId), footerId);
    }

    [Fact]
    public void FbxTranslator_Write_UsesStandardModelAndGeometryPayloads()
    {
        Scene sourceScene = CreateSampleScene();
        byte[] data = WriteScene(CreateManager(), sourceScene, "payload_test.fbx");

        using MemoryStream readStream = new(data, writable: false);
        FbxDocument document = FbxDocumentIO.Read(readStream);
        FbxNode objectsNode = Assert.Single(document.NodesNamed("Objects"));

        FbxNode meshModel = Assert.Single(objectsNode.Children, static node => node.Name == "Model" && FbxSceneMapper.GetNodeObjectName(node) == "mesh_0");
        Assert.NotNull(meshModel.FirstChild("Version"));
        Assert.NotNull(meshModel.FirstChild("Shading"));
        Assert.NotNull(meshModel.FirstChild("Culling"));
        FbxNode meshProperties = Assert.Single(meshModel.ChildrenNamed("Properties70"));
        Assert.Contains(meshProperties.Children, static child => child.Name == "P" && child.Properties.Count > 4 && child.Properties[0].AsString() == "currentUVSet" && child.Properties[4].AsString() == "map1");

        FbxNode geometry = Assert.Single(objectsNode.Children, static node => node.Name == "Geometry" && FbxSceneMapper.GetNodeObjectName(node) == "mesh_0");
        Assert.NotNull(geometry.FirstChild("GeometryVersion"));
        Assert.NotNull(geometry.FirstChild("Edges"));
        Assert.NotNull(geometry.FirstChild("Layer"));

        FbxNode layerElementNormal = Assert.Single(geometry.ChildrenNamed("LayerElementNormal"));
        Assert.Equal("ByVertice", FbxSceneMapper.GetNodeString(layerElementNormal, "MappingInformationType"));

        FbxNode layerElementUv = geometry.ChildrenNamed("LayerElementUV").First();
        Assert.Equal("ByPolygonVertex", FbxSceneMapper.GetNodeString(layerElementUv, "MappingInformationType"));
        Assert.Equal("IndexToDirect", FbxSceneMapper.GetNodeString(layerElementUv, "ReferenceInformationType"));
    }

    [Fact]
    public void FbxSceneMapper_ImportScene_ClassifiesNullBoneHierarchyAsSkeleton()
    {
        FbxDocument document = CreateImportClassificationDocument();

        Scene scene = FbxSceneMapper.ImportScene(document, "classification-test");
        SkeletonBone rigRoot = Assert.Single(scene.EnumerateChildren().OfType<SkeletonBone>(), static bone => bone.Name == "RigRoot");

        Assert.Equal(2, rigRoot.EnumerateHierarchy<SkeletonBone>().Count());
        Assert.DoesNotContain(scene.EnumerateChildren(), static child => child.Name == "FbxSkeleton");
        Assert.DoesNotContain(scene.EnumerateChildren(), static child => child.Name == "FbxModelRoot");
        Assert.DoesNotContain(scene.GetDescendants<Group>(), static group => group.Name == "RigRoot");
    }

    [Fact]
    public void FbxSceneMapper_ImportScene_ClassifiesNullMeshHierarchyAsModel()
    {
        FbxDocument document = CreateImportClassificationDocument();

        Scene scene = FbxSceneMapper.ImportScene(document, "classification-test");
        Model modelRoot = Assert.Single(scene.EnumerateChildren().OfType<Model>(), static model => model.Name == "ModelRoot");

        Assert.Contains(modelRoot.EnumerateChildren(), static child => child is Model model && model.Name == "MeshGroup");
        Assert.DoesNotContain(scene.GetDescendants<Group>(), static group => group.Name == "MeshGroup");
    }

    [Fact]
    public void FbxSceneMapper_ImportScene_KeepsMixedNullHierarchyAsGroup()
    {
        FbxDocument document = CreateImportClassificationDocument();

        Scene scene = FbxSceneMapper.ImportScene(document, "classification-test");
        Group mixedGroup = Assert.Single(scene.EnumerateChildren().OfType<Group>(), static group => group.Name == "MixedGroup");

        Assert.Contains(mixedGroup.EnumerateChildren(), static child => child is SkeletonBone bone && bone.Name == "mixed_bone");
        Assert.Contains(mixedGroup.EnumerateChildren(), static child => child is Mesh mesh && mesh.Name == "mixed_mesh");
    }

    [Fact]
    public void FbxSceneMapper_ImportScene_ImportsParentConstraint()
    {
        FbxDocument document = CreateImportConstraintDocument();

        Scene scene = FbxSceneMapper.ImportScene(document, "constraint-test");
        ParentConstraintNode constraint = Assert.Single(scene.GetDescendants<ParentConstraintNode>());

        Assert.Equal("child_bone_parentConstraint1", constraint.Name);
        Assert.Equal("child_bone", constraint.ConstrainedNode.Name);
        Assert.Equal("root_bone", constraint.SourceNode.Name);
        Assert.Same(constraint.ConstrainedNode, constraint.Parent);
        Assert.True(Vector3.Distance(new Vector3(1.0f, 2.0f, 3.0f), constraint.TranslationOffset) < 0.0001f);
        Quaternion expectedRotationOffset = Quaternion.Normalize(FbxRotation.FromEulerDegreesXyz(new Vector3(10.0f, 20.0f, 30.0f)));
        Assert.True(MathF.Abs(Quaternion.Dot(expectedRotationOffset, constraint.RotationOffset)) > 0.999f);
        Assert.True(MathF.Abs(constraint.Weight - 0.5f) < 0.0001f);
    }

    [Fact]
    public void FbxSceneMapper_ImportScene_ImportsOrientConstraint()
    {
        FbxDocument document = CreateImportOrientConstraintDocument();

        Scene scene = FbxSceneMapper.ImportScene(document, "orient-constraint-test");
        OrientConstraintNode constraint = Assert.Single(scene.GetDescendants<OrientConstraintNode>());

        Assert.Equal("child_bone_orientConstraint1", constraint.Name);
        Assert.Equal("child_bone", constraint.ConstrainedNode.Name);
        Assert.Equal("root_bone", constraint.SourceNode.Name);
        Assert.Same(constraint.ConstrainedNode, constraint.Parent);
        Quaternion expectedRotationOffset = Quaternion.Normalize(FbxRotation.FromEulerDegreesXyz(new Vector3(15.0f, 25.0f, 35.0f)));
        Assert.True(MathF.Abs(Quaternion.Dot(expectedRotationOffset, constraint.RotationOffset)) > 0.999f);
        Assert.True(MathF.Abs(constraint.Weight - 0.75f) < 0.0001f);
    }

    [Fact]
    public void FbxSceneMapper_ImportScene_ParentsConstraintUnderExplicitOwner()
    {
        FbxDocument document = CreateImportConstraintDocumentWithOwnerConnection(true);

        Scene scene = FbxSceneMapper.ImportScene(document, "constraint-owner-test");
        ParentConstraintNode constraint = Assert.Single(scene.GetDescendants<ParentConstraintNode>());
        SkeletonBone rigRoot = Assert.Single(scene.EnumerateChildren().OfType<SkeletonBone>(), static bone => bone.Name == "RigRoot");

        Assert.Same(rigRoot, constraint.Parent);
    }

    [Fact]
    public void FbxTranslator_Read_RealTalk_ImportsVisibleConstraintNodes()
    {
        string realTalkPath = GetWorkspaceAssetPath("RealTalk.fbx");
        Assert.True(File.Exists(realTalkPath), $"Expected test asset at: {realTalkPath}");

        byte[] data = File.ReadAllBytes(realTalkPath);
        using MemoryStream documentStream = new(data, writable: false);
        FbxDocument document = FbxDocumentIO.Read(documentStream);
        FbxNode objectsNode = Assert.Single(document.NodesNamed("Objects"));
        FbxNode[] rawConstraintNodes = [.. objectsNode.Children.Where(static child => child.Name == "Constraint")];
        Assert.NotEmpty(rawConstraintNodes);

        FbxNode? connectionsNode = document.FirstNode("Connections");
        Assert.NotNull(connectionsNode);
        FbxConnection[] rawPropertyConnections = [.. FbxSceneMapper.BuildConnections(connectionsNode).Where(static connection => string.Equals(connection.ConnectionType, "OP", StringComparison.Ordinal))];
        Assert.NotEmpty(rawPropertyConnections);

        SceneTranslatorManager manager = CreateManager();
        using MemoryStream stream = new(data, writable: false);
        Scene scene = manager.Read(stream, realTalkPath, new SceneTranslatorOptions(), token: null);

        ParentConstraintNode[] constraints = scene.GetDescendants<ParentConstraintNode>();
        Assert.NotEmpty(constraints);
        Assert.Contains(constraints, static constraint => constraint.Name.Contains("parentConstraint", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(constraints, static constraint => constraint.ConstrainedNode.Name.Contains("t7:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(constraints, static constraint => constraint.SourceNode.Name.Contains("j_", StringComparison.OrdinalIgnoreCase));
        Assert.All(constraints, static constraint => Assert.Same(constraint.ConstrainedNode, constraint.Parent));
    }

    [Fact]
    public void FbxTranslator_Read_RealTalkBinary_BonePositionsMatchAscii()
    {
        string binaryPath = GetWorkspaceAssetPath("RealTalk.fbx");
        string asciiPath = GetWorkspaceAssetPath("RealTalkascii.fbx");
        if (!File.Exists(binaryPath) || !File.Exists(asciiPath))
        {
            return;
        }

        SceneTranslatorManager manager = CreateManager();

        Scene binaryScene;
        using (FileStream fs = File.OpenRead(binaryPath))
        {
            binaryScene = manager.Read(fs, binaryPath, new SceneTranslatorOptions(), token: null);
        }

        Scene asciiScene;
        using (FileStream fs = File.OpenRead(asciiPath))
        {
            asciiScene = manager.Read(fs, asciiPath, new SceneTranslatorOptions(), token: null);
        }

        SkeletonBone[] binaryBones = binaryScene.GetDescendants<SkeletonBone>();
        SkeletonBone[] asciiBones = asciiScene.GetDescendants<SkeletonBone>();

        // Strip any class prefix (e.g. "Model::") for name matching
        static string StripPrefix(string name)
        {
            int idx = name.LastIndexOf("::", StringComparison.Ordinal);
            return idx >= 0 ? name[(idx + 2)..] : name;
        }

        // Both files should produce bones with the same world positions
        Dictionary<string, SkeletonBone> asciiMap = asciiBones.ToDictionary(static b => StripPrefix(b.Name));
        int matched = 0;
        foreach (SkeletonBone bBone in binaryBones)
        {
            string stripped = StripPrefix(bBone.Name);
            if (asciiMap.TryGetValue(stripped, out SkeletonBone? aBone))
            {
                Vector3 bWorld = bBone.GetBindWorldPosition();
                Vector3 aWorld = aBone.GetBindWorldPosition();
                float dist = Vector3.Distance(bWorld, aWorld);
                Assert.True(dist < 0.1f, $"Bone {bBone.Name}: binary={bWorld} ascii={aWorld} dist={dist}");
                matched++;
            }
        }

        Assert.True(matched > 50, $"Expected at least 50 matching bones, got {matched}");
    }

    [Fact]
    public void FbxDocumentIO_ReadAscii_DoesNotStallOnUnexpectedPropertyToken()
    {
        const string ascii = "Root: [\n{\n}\n";
        byte[] data = Encoding.UTF8.GetBytes(ascii);

        using MemoryStream stream = new(data, writable: false);
        FbxDocument document = FbxDocumentIO.Read(stream);

        FbxNode root = Assert.Single(document.Nodes);
        Assert.Equal(FbxFormat.Ascii, document.Format);
        Assert.Equal("Root", root.Name);
    }

    [Fact]
    public void FbxTranslator_Read_RealTalkAscii_ImportsSceneNodes()
    {
        string realTalkPath = GetWorkspaceAssetPath("RealTalkascii.fbx");
        if (!File.Exists(realTalkPath))
        {
            return;
        }

        byte[] data = File.ReadAllBytes(realTalkPath);
        using MemoryStream documentStream = new(data, writable: false);
        FbxDocument document = FbxDocumentIO.Read(documentStream);

        Assert.Equal(FbxFormat.Ascii, document.Format);
        FbxNode? objectsNode = document.FirstNode("Objects") ?? document.FirstNodeRecursive("Objects");
        FbxNode? connectionsNode = document.FirstNode("Connections") ?? document.FirstNodeRecursive("Connections");
        Assert.NotNull(objectsNode);
        Assert.NotNull(connectionsNode);
        Assert.NotEmpty(objectsNode.Children);
        Assert.NotEmpty(connectionsNode.Children);
        Assert.Contains(objectsNode.Children, static child => child.Name == "Model");

        SceneTranslatorManager manager = CreateManager();
        using MemoryStream stream = new(data, writable: false);
        Scene scene = manager.Read(stream, realTalkPath, new SceneTranslatorOptions(), token: null);

        Assert.NotEmpty(scene.EnumerateChildren());
        Assert.NotEmpty(scene.GetDescendants<Mesh>());
        Assert.NotEmpty(scene.GetDescendants<SkeletonBone>());
        Assert.All(scene.GetDescendants<ParentConstraintNode>(), static constraint => Assert.Same(constraint.ConstrainedNode, constraint.Parent));
    }

    [Fact]
    public void FbxSceneMapper_BuildConnections_PreservesPropertyConnectionName()
    {
        FbxNode connections = new("Connections");
        FbxNode connection = connections.AddChild("C");
        connection.Properties.Add(new FbxProperty('S', "OP"));
        connection.Properties.Add(new FbxProperty('L', 10L));
        connection.Properties.Add(new FbxProperty('L', 20L));
        connection.Properties.Add(new FbxProperty('S', "Constrained object (Child)"));

        List<FbxConnection> parsedConnections = FbxSceneMapper.BuildConnections(connections);
        FbxConnection parsedConnection = Assert.Single(parsedConnections);

        Assert.Equal("OP", parsedConnection.ConnectionType);
        Assert.Equal(10L, parsedConnection.ChildId);
        Assert.Equal(20L, parsedConnection.ParentId);
        Assert.Equal("Constrained object (Child)", parsedConnection.PropertyName);
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

    private static byte[] ReadFooterId(byte[] data)
    {
        int magicStart = data.Length - FbxDocumentSerializer.FooterMagic.Length;
        int versionStart = magicStart - 124;
        int footerEnd = versionStart - 1;
        while (footerEnd >= 0 && data[footerEnd] == 0)
        {
            footerEnd--;
        }

        int footerStart = footerEnd - 15;
        byte[] footerId = new byte[16];
        Array.Copy(data, footerStart, footerId, 0, footerId.Length);
        return footerId;
    }

    private static byte[] WriteScene(SceneTranslatorManager manager, Scene scene, string sourcePath)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, scene, new SceneTranslatorOptions(), token: null);
        return stream.ToArray();
    }

    private static byte[] WriteScene(SceneTranslatorManager manager, Scene scene, string sourcePath, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, scene, options, token: null);
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

        SkeletonBone armature = scene.RootNode.AddNode(new SkeletonBone("Armature"));
        SkeletonBone rootBone = armature.AddNode(new SkeletonBone("root"));
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
        new ushort[]
        {
            0, 1,
            0, 1,
            0, 1,
            0, 1,
        }, 2, 1);

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

    [Fact]
    public void FbxTranslator_Write_Filter_ReparentsSelectedNodesToNearestExportedAncestorAndPreservesWorldTransform()
    {
        Scene scene = new("filtered-fbx");
        Model exportRoot = scene.RootNode.AddNode(new Model
        {
            Name = "ExportRoot",
            Flags = SceneNodeFlags.Selected,
        });
        exportRoot.BindTransform.LocalPosition = new Vector3(5f, 0f, 0f);

        Group omittedGroup = exportRoot.AddNode(new Group("FilteredOutMid"));
        omittedGroup.BindTransform.LocalPosition = new Vector3(2f, 0f, 0f);

        Mesh mesh = omittedGroup.AddNode(new Mesh
        {
            Name = "SelectedMesh",
            Flags = SceneNodeFlags.Selected,
        });
        mesh.BindTransform.LocalPosition = new Vector3(1f, 0f, 0f);
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        SceneTranslatorManager manager = CreateManager();
        SceneTranslatorOptions options = new() { Filter = SceneNodeFlags.Selected };

        byte[] data = WriteScene(manager, scene, "filtered_hierarchy.fbx", options);
        Scene reloaded = ReadScene(manager, data, "filtered_hierarchy.fbx");

        Mesh reloadedMesh = Assert.Single(reloaded.GetDescendants<Mesh>());
        Assert.Equal("ExportRoot", reloadedMesh.Parent?.Name);
        Assert.True(Vector3.Distance(reloadedMesh.GetBindWorldPosition(), new Vector3(8f, 0f, 0f)) < 0.01f,
            $"Expected SelectedMesh world position to remain <8,0,0>, got {reloadedMesh.GetBindWorldPosition()}");
        Assert.Empty(reloaded.GetDescendants<Group>());
    }

    [Fact]
    public void FbxTranslator_Write_Filter_ThrowsWhenSelectedMeshReferencesFilteredMaterial()
    {
        Scene scene = CreateSampleScene();
        SceneTranslatorManager manager = CreateManager();

        Model model = Assert.Single(scene.GetDescendants<Model>());
        Mesh mesh = Assert.Single(scene.GetDescendants<Mesh>());
        SkeletonBone skeleton = Assert.Single(scene.GetDescendants<SkeletonBone>().Where(b => b.Parent is not SkeletonBone));

        model.Flags = SceneNodeFlags.Selected;
        mesh.Flags = SceneNodeFlags.Selected;
        skeleton.Flags = SceneNodeFlags.Selected;

        foreach (SkeletonBone bone in scene.GetDescendants<SkeletonBone>())
        {
            bone.Flags = SceneNodeFlags.Selected;
        }

        SceneTranslatorOptions options = new() { Filter = SceneNodeFlags.Selected };

        using MemoryStream stream = new();
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => manager.Write(stream, "filtered_missing_material.fbx", scene, options, token: null));

        Assert.Contains("references material", exception.Message);
        Assert.Contains("material_0", exception.Message);
    }

    [Fact]
    public void FbxTranslator_Write_Filter_ThrowsWhenSelectedMeshReferencesUnexportedBones()
    {
        Scene scene = CreateSampleScene();
        SceneTranslatorManager manager = CreateManager();

        Model model = Assert.Single(scene.GetDescendants<Model>());
        Mesh mesh = Assert.Single(scene.GetDescendants<Mesh>());
        Material material = Assert.Single(scene.GetDescendants<Material>());

        model.Flags = SceneNodeFlags.Selected;
        mesh.Flags = SceneNodeFlags.Selected;
        material.Flags = SceneNodeFlags.Selected;

        SceneTranslatorOptions options = new() { Filter = SceneNodeFlags.Selected };

        using MemoryStream stream = new();
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => manager.Write(stream, "filtered_missing_bones.fbx", scene, options, token: null));

        Assert.Contains("references skinned bones", exception.Message);
        Assert.Contains("root", exception.Message);
    }


    [Fact]
    public void FbxTranslator_RealTalkBinary_RoundTripPreservesT7LiveBonesAndSkinSample()
    {
        string sourcePath = GetWorkspaceAssetPath("RealTalk.fbx");
        if (!File.Exists(sourcePath))
        {
            return;
        }

        SceneTranslatorManager manager = CreateManager();

        Scene sourceScene;
        using (FileStream input = File.OpenRead(sourcePath))
        {
            sourceScene = manager.Read(input, sourcePath, new SceneTranslatorOptions(), token: null);
        }

        SkeletonBone sourceBone = Assert.Single(sourceScene.GetDescendants<SkeletonBone>(), static bone => bone.Name.EndsWith("t7:j_wrist_le", StringComparison.Ordinal));
        Mesh sourceMesh = Assert.Single(sourceScene.GetDescendants<Mesh>(), static mesh => mesh.Name.EndsWith("SEModelMesh4", StringComparison.Ordinal));
        Mesh sourceMeshUrban = Assert.Single(sourceScene.GetDescendants<Mesh>(), static mesh => mesh.Name.EndsWith("SEModelMesh", StringComparison.Ordinal));

        Vector3 sourceActiveBonePos = sourceBone.GetActiveWorldPosition();
        Vector3 sourceSkinnedVertex = sourceMesh.GetVertexPosition(0, raw: false);

        using MemoryStream output = new();
        manager.Write(output, "roundtrip_t7.fbx", sourceScene, new SceneTranslatorOptions(), token: null);
        output.Position = 0;

        Scene roundTripped;
        roundTripped = manager.Read(output, "roundtrip_t7.fbx", new SceneTranslatorOptions(), token: null);

        SkeletonBone roundTripBone = Assert.Single(roundTripped.GetDescendants<SkeletonBone>(), static bone => bone.Name.EndsWith("t7:j_wrist_le", StringComparison.Ordinal));
        Mesh roundTripMesh = Assert.Single(roundTripped.GetDescendants<Mesh>(), static mesh => mesh.Name.EndsWith("SEModelMesh4", StringComparison.Ordinal));
        Mesh roundTripMeshUrban = Assert.Single(roundTripped.GetDescendants<Mesh>(), static mesh => mesh.Name.EndsWith("SEModelMesh", StringComparison.Ordinal));

        Vector3 roundTripActiveBonePos = roundTripBone.GetActiveWorldPosition();
        Vector3 roundTripSkinnedVertex = roundTripMesh.GetVertexPosition(0, raw: false);
        Vector3 sourceRawVertex = sourceMesh.GetVertexPosition(0, raw: true);
        Vector3 roundTripRawVertex = roundTripMesh.GetVertexPosition(0, raw: true);

        Matrix4x4 sourceIbm0 = sourceMesh.InverseBindMatrices is { Count: > 0 } sourceIbms ? sourceIbms[0] : Matrix4x4.Identity;
        Matrix4x4 roundTripIbm0 = roundTripMesh.InverseBindMatrices is { Count: > 0 } roundTripIbms ? roundTripIbms[0] : Matrix4x4.Identity;

        SkeletonBone? sourceMeshBone0 = sourceMesh.SkinnedBones is { Count: > 0 } sourceBones ? sourceBones[0] : null;
        SkeletonBone? roundTripMeshBone0 = roundTripMesh.SkinnedBones is { Count: > 0 } roundTripBones ? roundTripBones[0] : null;
        Vector3 sourceMeshBone0Active = sourceMeshBone0?.GetActiveWorldPosition() ?? Vector3.Zero;
        Vector3 roundTripMeshBone0Active = roundTripMeshBone0?.GetActiveWorldPosition() ?? Vector3.Zero;

        float dempseyMaxDelta = 0f;
        int dempseySampleCount = Math.Min(sourceMesh.VertexCount, roundTripMesh.VertexCount);
        for (int i = 0; i < dempseySampleCount; i++)
        {
            Vector3 src = sourceMesh.GetVertexPosition(i, raw: false);
            Vector3 dst = roundTripMesh.GetVertexPosition(i, raw: false);
            dempseyMaxDelta = Math.Max(dempseyMaxDelta, Vector3.Distance(src, dst));
        }

        float urbanMaxDelta = 0f;
        int urbanMaxIndex = -1;
        Vector3 urbanMaxSource = Vector3.Zero;
        Vector3 urbanMaxRoundTrip = Vector3.Zero;
        int urbanSampleCount = Math.Min(sourceMeshUrban.VertexCount, roundTripMeshUrban.VertexCount);
        for (int i = 0; i < urbanSampleCount; i++)
        {
            Vector3 src = sourceMeshUrban.GetVertexPosition(i, raw: false);
            Vector3 dst = roundTripMeshUrban.GetVertexPosition(i, raw: false);
            float delta = Vector3.Distance(src, dst);
            if (delta > urbanMaxDelta)
            {
                urbanMaxDelta = delta;
                urbanMaxIndex = i;
                urbanMaxSource = src;
                urbanMaxRoundTrip = dst;
            }
        }

        Matrix4x4 urbanSourceIbm0 = sourceMeshUrban.InverseBindMatrices is { Count: > 0 } urbanSourceIbms ? urbanSourceIbms[0] : Matrix4x4.Identity;
        Matrix4x4 urbanRoundTripIbm0 = roundTripMeshUrban.InverseBindMatrices is { Count: > 0 } urbanRoundTripIbms ? urbanRoundTripIbms[0] : Matrix4x4.Identity;

        string urbanInfluenceDetails = string.Empty;
        if (urbanMaxIndex >= 0 && sourceMeshUrban.BoneIndices is not null && sourceMeshUrban.BoneWeights is not null && roundTripMeshUrban.BoneIndices is not null && roundTripMeshUrban.BoneWeights is not null)
        {
            int srcInfluenceCount = Math.Min(sourceMeshUrban.BoneIndices.ValueCount, sourceMeshUrban.BoneWeights.ValueCount);
            int rtInfluenceCount = Math.Min(roundTripMeshUrban.BoneIndices.ValueCount, roundTripMeshUrban.BoneWeights.ValueCount);
            List<string> parts = [];

            for (int i = 0; i < srcInfluenceCount; i++)
            {
                int idx = sourceMeshUrban.BoneIndices.Get<int>(urbanMaxIndex, i, 0);
                float w = sourceMeshUrban.BoneWeights.Get<float>(urbanMaxIndex, i, 0);
                string boneName = (uint)idx < (uint)(sourceMeshUrban.SkinnedBones?.Count ?? 0) ? sourceMeshUrban.SkinnedBones![idx].Name : "?";
                Vector3 bonePos = (uint)idx < (uint)(sourceMeshUrban.SkinnedBones?.Count ?? 0) ? sourceMeshUrban.SkinnedBones![idx].GetActiveWorldPosition() : Vector3.Zero;
                parts.Add($"src[{i}] idx={idx} w={w:F4} bone={boneName} pos={bonePos}");
            }

            for (int i = 0; i < rtInfluenceCount; i++)
            {
                int idx = roundTripMeshUrban.BoneIndices.Get<int>(urbanMaxIndex, i, 0);
                float w = roundTripMeshUrban.BoneWeights.Get<float>(urbanMaxIndex, i, 0);
                string boneName = (uint)idx < (uint)(roundTripMeshUrban.SkinnedBones?.Count ?? 0) ? roundTripMeshUrban.SkinnedBones![idx].Name : "?";
                Vector3 bonePos = (uint)idx < (uint)(roundTripMeshUrban.SkinnedBones?.Count ?? 0) ? roundTripMeshUrban.SkinnedBones![idx].GetActiveWorldPosition() : Vector3.Zero;
                parts.Add($"rt[{i}] idx={idx} w={w:F4} bone={boneName} pos={bonePos}");
            }

            urbanInfluenceDetails = string.Join(" | ", parts);
        }

        Assert.True(Vector3.Distance(sourceActiveBonePos, roundTripActiveBonePos) < 0.05f,
            $"t7 wrist active position changed across roundtrip: source={sourceActiveBonePos}, roundtrip={roundTripActiveBonePos}");
        Assert.True(Vector3.Distance(sourceSkinnedVertex, roundTripSkinnedVertex) < 0.05f,
            $"t7 mesh skinned vertex changed across roundtrip: source={sourceSkinnedVertex}, roundtrip={roundTripSkinnedVertex}; rawSource={sourceRawVertex}, rawRoundTrip={roundTripRawVertex}; sourceIBM0={sourceIbm0}; roundTripIBM0={roundTripIbm0}; sourceBone0={sourceMeshBone0?.Name} active={sourceMeshBone0Active}; roundTripBone0={roundTripMeshBone0?.Name} active={roundTripMeshBone0Active}");
        Assert.True(dempseyMaxDelta < 0.15f, $"c_zom_der_dempsey_viewhands max skinned delta too large: {dempseyMaxDelta}");
        Assert.True(urbanMaxDelta < 0.15f,
            $"viewhands_sas_urban_a max skinned delta too large: {urbanMaxDelta} at vtx={urbanMaxIndex}; src={urbanMaxSource}; rt={urbanMaxRoundTrip}; srcIBM0={urbanSourceIbm0}; rtIBM0={urbanRoundTripIbm0}; influences={urbanInfluenceDetails}");
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

    private static string GetWorkspaceAssetPath(string assetName, [CallerFilePath] string sourceFilePath = "")
    {
        string? sourceDirectory = Path.GetDirectoryName(sourceFilePath);
        Assert.NotNull(sourceDirectory);
        return Path.GetFullPath(Path.Combine(sourceDirectory!, "..", "..", assetName));
    }

    private static FbxDocument CreateImportClassificationDocument()
    {
        FbxDocument document = new() { Format = FbxFormat.Binary, Version = 7400 };
        FbxNode objectsNode = new("Objects");
        FbxNode connectionsNode = new("Connections");
        document.Nodes.Add(objectsNode);
        document.Nodes.Add(connectionsNode);

        AddModelObject(objectsNode, 1001L, "RigRoot", "Null");
        AddModelObject(objectsNode, 1002L, "root_bone", "LimbNode");
        AddModelObject(objectsNode, 1003L, "child_bone", "LimbNode");
        AddModelObject(objectsNode, 1101L, "ModelRoot", "Null");
        AddModelObject(objectsNode, 1102L, "MeshGroup", "Null");
        AddModelObject(objectsNode, 1103L, "mesh_a", "Mesh");
        AddModelObject(objectsNode, 1201L, "MixedGroup", "Null");
        AddModelObject(objectsNode, 1202L, "mixed_bone", "LimbNode");
        AddModelObject(objectsNode, 1203L, "mixed_mesh", "Mesh");

        AddConnection(connectionsNode, "OO", 1001L, 0L);
        AddConnection(connectionsNode, "OO", 1002L, 1001L);
        AddConnection(connectionsNode, "OO", 1003L, 1002L);
        AddConnection(connectionsNode, "OO", 1101L, 0L);
        AddConnection(connectionsNode, "OO", 1102L, 1101L);
        AddConnection(connectionsNode, "OO", 1103L, 1102L);
        AddConnection(connectionsNode, "OO", 1201L, 0L);
        AddConnection(connectionsNode, "OO", 1202L, 1201L);
        AddConnection(connectionsNode, "OO", 1203L, 1201L);
        return document;
    }

    private static FbxDocument CreateImportConstraintDocument()
    {
        return CreateImportConstraintDocumentWithOwnerConnection(false);
    }

    private static FbxDocument CreateImportConstraintDocumentWithOwnerConnection(bool includeOwnerConnection)
    {
        FbxDocument document = new() { Format = FbxFormat.Binary, Version = 7400 };
        FbxNode objectsNode = new("Objects");
        FbxNode connectionsNode = new("Connections");
        document.Nodes.Add(objectsNode);
        document.Nodes.Add(connectionsNode);

        AddModelObject(objectsNode, 1001L, "RigRoot", "Null");
        AddModelObject(objectsNode, 1002L, "root_bone", "LimbNode");
        AddModelObject(objectsNode, 1003L, "child_bone", "LimbNode");

        FbxNode constraintNode = objectsNode.AddChild("Constraint");
        constraintNode.Properties.Add(new FbxProperty('L', 2001L));
        constraintNode.Properties.Add(new FbxProperty('S', "child_bone_parentConstraint1\0\u0001Constraint"));
        constraintNode.Properties.Add(new FbxProperty('S', "Parent-Child"));
        constraintNode.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "Parent-Child") } });
        FbxNode properties = constraintNode.AddChild("Properties70");
        AddDoubleProperty(properties, "child_bone.Weight", "Number", 50.0);
        AddVectorProperty(properties, "child_bone.Offset T", "Translation", new Vector3(1.0f, 2.0f, 3.0f));
        AddVectorProperty(properties, "child_bone.Offset R", "Translation", new Vector3(10.0f, 20.0f, 30.0f));

        AddConnection(connectionsNode, "OO", 1001L, 0L);
        AddConnection(connectionsNode, "OO", 1002L, 1001L);
        AddConnection(connectionsNode, "OO", 1003L, 1002L);
        if (includeOwnerConnection)
        {
            AddConnection(connectionsNode, "OO", 2001L, 1001L);
        }

        AddConnection(connectionsNode, "OP", 1003L, 2001L, "Constrained object (Child)");
        AddConnection(connectionsNode, "OP", 1002L, 2001L, "Source (Parent)");
        return document;
    }

    private static FbxDocument CreateImportOrientConstraintDocument()
    {
        FbxDocument document = new() { Format = FbxFormat.Binary, Version = 7400 };
        FbxNode objectsNode = new("Objects");
        FbxNode connectionsNode = new("Connections");
        document.Nodes.Add(objectsNode);
        document.Nodes.Add(connectionsNode);

        AddModelObject(objectsNode, 1001L, "RigRoot", "Null");
        AddModelObject(objectsNode, 1002L, "root_bone", "LimbNode");
        AddModelObject(objectsNode, 1003L, "child_bone", "LimbNode");

        FbxNode constraintNode = objectsNode.AddChild("Constraint");
        constraintNode.Properties.Add(new FbxProperty('L', 3001L));
        constraintNode.Properties.Add(new FbxProperty('S', "child_bone_orientConstraint1\0\u0001Constraint"));
        constraintNode.Properties.Add(new FbxProperty('S', "Orientation"));
        constraintNode.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "Orientation") } });
        FbxNode properties = constraintNode.AddChild("Properties70");
        AddDoubleProperty(properties, "child_bone.Weight", "Number", 75.0);
        AddVectorProperty(properties, "child_bone.Offset R", "Translation", new Vector3(15.0f, 25.0f, 35.0f));

        AddConnection(connectionsNode, "OO", 1001L, 0L);
        AddConnection(connectionsNode, "OO", 1002L, 1001L);
        AddConnection(connectionsNode, "OO", 1003L, 1002L);
        AddConnection(connectionsNode, "OP", 1003L, 3001L, "Constrained object (Child)");
        AddConnection(connectionsNode, "OP", 1002L, 3001L, "Source (Parent)");
        return document;
    }

    private static void AddModelObject(FbxNode objectsNode, long id, string name, string modelType)
    {
        FbxNode modelNode = objectsNode.AddChild("Model");
        modelNode.Properties.Add(new FbxProperty('L', id));
        modelNode.Properties.Add(new FbxProperty('S', $"{name}\0\u0001Model"));
        modelNode.Properties.Add(new FbxProperty('S', modelType));

        FbxNode properties = modelNode.AddChild("Properties70");
        AddVectorProperty(properties, "Lcl Translation", "Lcl Translation", Vector3.Zero);
        AddVectorProperty(properties, "Lcl Rotation", "Lcl Rotation", Vector3.Zero);
        AddVectorProperty(properties, "Lcl Scaling", "Lcl Scaling", Vector3.One);
    }

    private static void AddConnection(FbxNode connectionsNode, string connectionType, long childId, long parentId)
    {
        AddConnection(connectionsNode, connectionType, childId, parentId, string.Empty);
    }

    private static void AddConnection(FbxNode connectionsNode, string connectionType, long childId, long parentId, string propertyName)
    {
        FbxNode connection = connectionsNode.AddChild("C");
        connection.Properties.Add(new FbxProperty('S', connectionType));
        connection.Properties.Add(new FbxProperty('L', childId));
        connection.Properties.Add(new FbxProperty('L', parentId));

        if (!string.IsNullOrEmpty(propertyName))
        {
            connection.Properties.Add(new FbxProperty('S', propertyName));
        }
    }

    private static void AddDoubleProperty(FbxNode properties, string propertyName, string propertyType, double value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('S', "A"));
        property.Properties.Add(new FbxProperty('D', value));
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

    private static void AssertMatrixApproximatelyEqual(Matrix4x4 expected, Matrix4x4 actual)
    {
        Assert.True(MathF.Abs(expected.M11 - actual.M11) < 0.0001f);
        Assert.True(MathF.Abs(expected.M12 - actual.M12) < 0.0001f);
        Assert.True(MathF.Abs(expected.M13 - actual.M13) < 0.0001f);
        Assert.True(MathF.Abs(expected.M14 - actual.M14) < 0.0001f);
        Assert.True(MathF.Abs(expected.M21 - actual.M21) < 0.0001f);
        Assert.True(MathF.Abs(expected.M22 - actual.M22) < 0.0001f);
        Assert.True(MathF.Abs(expected.M23 - actual.M23) < 0.0001f);
        Assert.True(MathF.Abs(expected.M24 - actual.M24) < 0.0001f);
        Assert.True(MathF.Abs(expected.M31 - actual.M31) < 0.0001f);
        Assert.True(MathF.Abs(expected.M32 - actual.M32) < 0.0001f);
        Assert.True(MathF.Abs(expected.M33 - actual.M33) < 0.0001f);
        Assert.True(MathF.Abs(expected.M34 - actual.M34) < 0.0001f);
        Assert.True(MathF.Abs(expected.M41 - actual.M41) < 0.0001f);
        Assert.True(MathF.Abs(expected.M42 - actual.M42) < 0.0001f);
        Assert.True(MathF.Abs(expected.M43 - actual.M43) < 0.0001f);
        Assert.True(MathF.Abs(expected.M44 - actual.M44) < 0.0001f);
    }

    private static void AssertVectorApproximatelyEqual(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 0.0001f);
    }

    [Fact]
    public void FbxImport_RealTalkAscii_BoneTransformsMatchReferenceMA()
    {
        string fbxPath = GetWorkspaceAssetPath("RealTalkascii.fbx");
        if (!File.Exists(fbxPath))
        {
            fbxPath = GetWorkspaceAssetPath("RealTalk.fbx");
        }

        if (!File.Exists(fbxPath))
        {
            return;
        }

        SceneTranslatorManager manager = CreateManager();
        Scene scene;
        using (FileStream fs = File.OpenRead(fbxPath))
        {
            scene = manager.Read(fs, fbxPath, new SceneTranslatorOptions(), token: null);
        }

        // Verify root containers have identity transforms (coordinate conversion stripped)
        foreach (SceneNode child in scene.RootNode.EnumerateChildren())
        {
            if (child is SkeletonBone || child is Mesh)
            {
                continue;
            }

            Assert.Equal(Quaternion.Identity, child.GetBindLocalRotation());
            Assert.Equal(Vector3.Zero, child.GetBindLocalPosition());
        }

        // Verify key bone transforms match reference MA values (strip "Model::" prefix for binary FBX compat)
        SkeletonBone[] allBones = scene.GetDescendants<SkeletonBone>();

        static string StripPrefix(string name)
        {
            int idx = name.LastIndexOf("::", StringComparison.Ordinal);
            return idx >= 0 ? name[(idx + 2)..] : name;
        }

        Dictionary<string, SkeletonBone> boneMap = allBones.ToDictionary(b => StripPrefix(b.Name));

        // tag_origin: identity
        Assert.True(boneMap.ContainsKey("tag_origin"));
        AssertVectorNear(Vector3.Zero, boneMap["tag_origin"].GetBindLocalPosition(), 0.01f);

        // tag_view: t=(-0.269, 0, 163.373)
        Assert.True(boneMap.ContainsKey("tag_view"));
        AssertVectorNear(new(-0.269f, 0f, 163.373f), boneMap["tag_view"].GetBindLocalPosition(), 0.01f);

        // j_mainroot: t=(0, 0, -36.358), rotation near (90, -90, 0)
        Assert.True(boneMap.ContainsKey("j_mainroot"));
        AssertVectorNear(new(0f, 0f, -36.358f), boneMap["j_mainroot"].GetBindLocalPosition(), 0.01f);

        // j_knee_le: t=(43.593, ~0, 0)
        Assert.True(boneMap.ContainsKey("j_knee_le"));
        AssertVectorNear(new(43.593f, 0f, 0f), boneMap["j_knee_le"].GetBindLocalPosition(), 0.01f);
    }

    [Fact]
    public void FbxImport_RealTalkAscii_MaOutputMatchesReference()
    {
        string fbxPath = GetWorkspaceAssetPath("RealTalkascii.fbx");
        if (!File.Exists(fbxPath))
        {
            fbxPath = GetWorkspaceAssetPath("RealTalk.fbx");
        }

        if (!File.Exists(fbxPath))
        {
            return;
        }

        SceneTranslatorManager manager = CreateManager();
        manager.Register(new MayaAsciiTranslator());

        Scene scene;
        using (FileStream fs = File.OpenRead(fbxPath))
        {
            scene = manager.Read(fs, fbxPath, new SceneTranslatorOptions(), token: null);
        }

        using MemoryStream ms = new();
        manager.Write(ms, "test.ma", scene, new SceneTranslatorOptions(), token: null);
        ms.Position = 0;
        using StreamReader reader = new(ms, Encoding.UTF8);
        string maOutput = reader.ReadToEnd();

        // j_mainroot joint orient must be (90.003, -90, 0), not a gimbal-lock artifact
        int jMainrootIdx = maOutput.IndexOf("j_mainroot\"", StringComparison.Ordinal);
        Assert.True(jMainrootIdx >= 0, "j_mainroot joint not found in MA output");

        string block = maOutput[jMainrootIdx..Math.Min(jMainrootIdx + 500, maOutput.Length)];
        Assert.Contains("90.003", block);
        Assert.Contains("-90", block);
        Assert.Contains("-36.357", block);

        // Containers must have no rotation attributes — find "Joints" transform node
        int jointsIdx = maOutput.IndexOf("Joints\"", StringComparison.Ordinal);
        Assert.True(jointsIdx >= 0, "Joints container not found in MA output");

        string jointsBlock = maOutput[jointsIdx..Math.Min(jointsIdx + 300, maOutput.Length)];
        Assert.DoesNotContain(".r -type", jointsBlock.Split("createNode")[0]);

        // Header must declare Z-up since the scene graph uses Z-up convention
        Assert.Contains("upAxis \"z\"", maOutput);
    }

    private static void AssertVectorNear(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.True(MathF.Abs(expected.X - actual.X) < tolerance, $"X: expected {expected.X}, got {actual.X}");
        Assert.True(MathF.Abs(expected.Y - actual.Y) < tolerance, $"Y: expected {expected.Y}, got {actual.Y}");
        Assert.True(MathF.Abs(expected.Z - actual.Z) < tolerance, $"Z: expected {expected.Z}, got {actual.Z}");
    }

    [Fact]
    public void FbxImport_RealTalkAscii_GenerateOutputFiles()
    {
        string fbxPath = GetWorkspaceAssetPath("RealTalkascii.fbx");
        if (!File.Exists(fbxPath))
        {
            return;
        }

        string outputDir = Path.Combine(Path.GetDirectoryName(fbxPath)!, "artifacts", "fbx-build");
        Directory.CreateDirectory(outputDir);

        SceneTranslatorManager manager = CreateManager();
        manager.Register(new MayaAsciiTranslator());
        manager.Register(new SemodelTranslator());

        Scene scene;
        using (FileStream fs = File.OpenRead(fbxPath))
        {
            scene = manager.Read(fs, fbxPath, new SceneTranslatorOptions(), token: null);
        }

        // Diagnostic: dump vertex bounds, bone positions, IBMs, skin transforms
        string diagPath = Path.Combine(outputDir, "skin_diag.txt");

        // Read raw FBX document for BindPose diagnostics
        FbxDocument rawDoc;
        using (FileStream rawFs = File.OpenRead(fbxPath))
        {
            rawDoc = FbxDocumentIO.Read(rawFs);
        }

        using (StreamWriter sw = new(diagPath))
        {
            Mesh[] meshes = scene.GetDescendants<Mesh>();
            SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();

            sw.WriteLine($"=== Bones: {bones.Length}, Meshes: {meshes.Length} ===\n");

            // Dump BindPose entries from the raw FBX document
            sw.WriteLine("=== BindPose Entries ===");
            FbxNode? rawObjects = rawDoc.FirstNode("Objects") ?? rawDoc.FirstNodeRecursive("Objects");
            if (rawObjects is not null)
            {
                // Build a name map from model IDs
                Dictionary<long, string> idToName = [];
                foreach (FbxNode obj in rawObjects.Children)
                {
                    if (obj.Properties.Count >= 2)
                    {
                        long id = obj.Properties[0].AsInt64();
                        string name = obj.Properties[1].AsString().Split('\0')[0];
                        idToName[id] = name;
                    }
                }

                foreach (FbxNode obj in rawObjects.Children)
                {
                    if (!string.Equals(obj.Name, "Pose", StringComparison.Ordinal))
                        continue;
                    FbxNode? typeNode = obj.FirstChild("Type");
                    if (typeNode is null || typeNode.Properties.Count == 0)
                        continue;
                    string poseType = typeNode.Properties[0].AsString();
                    string poseName = obj.Properties.Count >= 2 ? obj.Properties[1].AsString().Split('\0')[0] : "?";
                    sw.WriteLine($"  Pose: {poseName} type={poseType}");

                    foreach (FbxNode poseNode in obj.ChildrenNamed("PoseNode"))
                    {
                        FbxNode? nodeChild = poseNode.FirstChild("Node");
                        FbxNode? matrixChild = poseNode.FirstChild("Matrix");
                        if (nodeChild is null || matrixChild is null) continue;
                        long nodeId = nodeChild.Properties[0].AsInt64();
                        Matrix4x4 mat = FbxSkinningMapper.ReadNodeMatrix(poseNode, "Matrix");
                        string nodeName = idToName.TryGetValue(nodeId, out string? n) ? n : $"id={nodeId}";
                        sw.WriteLine($"    {nodeName} (id={nodeId}): T=<{mat.M41:F3}, {mat.M42:F3}, {mat.M43:F3}>");
                    }
                }
            }
            sw.WriteLine();

            // Debug: check parent container transforms
            sw.WriteLine("=== Container Transform Debug ===");
            foreach (Mesh m in meshes)
            {
                if (m.Parent is not null && m.Parent is not Mesh)
                {
                    SceneNode parent = m.Parent;
                    Vector3 parentPos = parent.GetBindLocalPosition();
                    Quaternion parentRot = parent.GetBindLocalRotation();
                    if (parentPos != Vector3.Zero || parentRot != Quaternion.Identity)
                    {
                        sw.WriteLine($"  {m.Name} parent={parent.Name}: localPos={parentPos} localRot={parentRot} parentWorld={parent.GetBindWorldMatrix().Translation}");
                    }
                }
            }
            sw.WriteLine();

            // Debug: for each skinned mesh, show what the baking heuristic computed
            sw.WriteLine("=== Baking Heuristic Debug ===");
            foreach (Mesh m in meshes)
            {
                if (!m.HasSkinning || m.Positions is not { ElementCount: > 0 } pos || m.SkinnedBones is not { Count: > 0 } sb)
                    continue;

                Vector3 vCenter = Vector3.Zero;
                for (int v = 0; v < pos.ElementCount; v++) vCenter += pos.GetVector3(v, 0);
                vCenter /= pos.ElementCount;

                Vector3 bCenter = Vector3.Zero;
                for (int b = 0; b < sb.Count; b++) bCenter += sb[b].GetBindWorldPosition();
                bCenter /= sb.Count;

                float rawDist = Vector3.Distance(vCenter, bCenter);
                sw.WriteLine($"  {m.Name}: vCenter={vCenter} bCenter={bCenter} rawDist={rawDist:F2}");
            }
            sw.WriteLine();

            // Dump a few bone world positions
            sw.WriteLine("=== Key Bone World Positions ===");
            foreach (string boneName in new[] { "Model::tag_origin", "Model::tag_view", "Model::tag_torso", "Model::j_mainroot", "Model::j_hip_le", "Model::j_knee_le", "Model::t7:j_mainroot", "Model::t7:tag_origin", "Model::t7:j_wrist_le", "Model::t7:j_wristtwist5_le" })
            {
                SkeletonBone? bone = bones.FirstOrDefault(b => b.Name == boneName);
                if (bone != null)
                {
                    sw.WriteLine($"  {boneName}: worldPos={bone.GetBindWorldPosition()} localPos={bone.GetBindLocalPosition()}");
                    sw.WriteLine($"    worldMatrix={bone.GetBindWorldMatrix()}");
                }
            }

            sw.WriteLine();

            // Dump per-mesh diagnostics
            foreach (Mesh mesh in meshes)
            {
                sw.WriteLine($"=== Mesh: {mesh.Name} (parent={mesh.Parent?.Name}) ===");
                sw.WriteLine($"  localPos={mesh.GetBindLocalPosition()} localRot={mesh.GetBindLocalRotation()}");
                sw.WriteLine($"  worldMatrix={mesh.GetBindWorldMatrix()}");
                sw.WriteLine($"  HasSkinning={mesh.HasSkinning} SkinnedBones={mesh.SkinnedBones?.Count ?? 0}");
                sw.WriteLine($"  HasExplicitIBMs={mesh.HasExplicitInverseBindMatrices}");

                // Vertex bounding box
                if (mesh.Positions != null)
                {
                    Vector3 min = new(float.MaxValue);
                    Vector3 max = new(float.MinValue);
                    int vertCount = mesh.Positions.ElementCount;
                    for (int v = 0; v < vertCount; v++)
                    {
                        Vector3 p = mesh.Positions.GetVector3(v, 0);
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                    sw.WriteLine($"  RAW vertex bounds: min={min} max={max}");
                    sw.WriteLine($"  RAW vertex center: {(min + max) * 0.5f}");
                }

                // Show IBM and skinTransform for first bone
                if (mesh.HasSkinning && mesh.SkinnedBones?.Count > 0)
                {
                    SkeletonBone firstBone = mesh.SkinnedBones[0];

                    IReadOnlyList<Matrix4x4>? ibms = mesh.InverseBindMatrices;
                    if (ibms != null && ibms.Count > 0)
                    {
                        sw.WriteLine($"  IBM[0] (bone={firstBone.Name}): {ibms[0]}");
                        Matrix4x4 boneWorld = firstBone.GetActiveWorldMatrix();
                        Matrix4x4 skinTransform = ibms[0] * boneWorld;
                        sw.WriteLine($"  skinTransform[0]: {skinTransform}");

                        // Show what vertex 0 becomes after skinning
                        if (mesh.Positions != null && mesh.Positions.ElementCount > 0)
                        {
                            Vector3 rawV0 = mesh.Positions.GetVector3(0, 0);
                            Vector3 skinnedV0 = Vector3.Transform(rawV0, skinTransform);
                            sw.WriteLine($"  vertex[0] raw={rawV0} skinned={skinnedV0}");
                        }
                    }
                }

                sw.WriteLine();
            }
        }

        // Also compare with reference SEModel
        string refSemodelPath = GetWorkspaceAssetPath("RealTalk2.semodel");
        if (File.Exists(refSemodelPath))
        {
            Scene refScene;
            using (FileStream fs = File.OpenRead(refSemodelPath))
            {
                refScene = manager.Read(fs, refSemodelPath, new SceneTranslatorOptions(), token: null);
            }

            string refDiagPath = Path.Combine(outputDir, "ref_semodel_diag.txt");
            using (StreamWriter sw = new(refDiagPath))
            {
                Mesh[] refMeshes = refScene.GetDescendants<Mesh>();
                SkeletonBone[] refBones = refScene.GetDescendants<SkeletonBone>();

                sw.WriteLine($"=== REF SEModel: Bones={refBones.Length}, Meshes={refMeshes.Length} ===\n");

                sw.WriteLine("=== Key Bone World Positions ===");
                foreach (SkeletonBone bone in refBones.Take(20))
                {
                    sw.WriteLine($"  {bone.Name}: worldPos={bone.GetBindWorldPosition()} localPos={bone.GetBindLocalPosition()}");
                }

                sw.WriteLine();

                foreach (Mesh mesh in refMeshes)
                {
                    sw.WriteLine($"=== Mesh: {mesh.Name} (parent={mesh.Parent?.Name}) ===");
                    sw.WriteLine($"  HasSkinning={mesh.HasSkinning} SkinnedBones={mesh.SkinnedBones?.Count ?? 0}");
                    if (mesh.Positions != null)
                    {
                        Vector3 min = new(float.MaxValue);
                        Vector3 max = new(float.MinValue);
                        for (int v = 0; v < mesh.Positions.ElementCount; v++)
                        {
                            Vector3 p = mesh.Positions.GetVector3(v, 0);
                            min = Vector3.Min(min, p);
                            max = Vector3.Max(max, p);
                        }
                        sw.WriteLine($"  Vertex bounds: min={min} max={max}");
                        sw.WriteLine($"  Vertex center: {(min + max) * 0.5f}");
                    }
                    sw.WriteLine();
                }
            }
        }

        string maPath = Path.Combine(outputDir, "RealTalkascii.ma");
        using (FileStream maFs = File.Create(maPath))
        {
            manager.Write(maFs, maPath, scene, new SceneTranslatorOptions(), token: null);
        }

        string semodelPath = Path.Combine(outputDir, "RealTalkascii.semodel");
        using (FileStream smFs = File.Create(semodelPath))
        {
            manager.Write(smFs, semodelPath, scene, new SceneTranslatorOptions(), token: null);
        }

        Assert.True(File.Exists(maPath), $"MA file not created at {maPath}");
        Assert.True(File.Exists(semodelPath), $"SEModel file not created at {semodelPath}");
        Assert.True(new FileInfo(maPath).Length > 0, "MA file is empty");
        Assert.True(new FileInfo(semodelPath).Length > 0, "SEModel file is empty");

        // Round-trip: read our SEModel back and compare with input scene
        Scene reloadedScene;
        using (FileStream fs = File.OpenRead(semodelPath))
        {
            reloadedScene = manager.Read(fs, semodelPath, new SceneTranslatorOptions(), token: null);
        }

        string rtDiagPath = Path.Combine(outputDir, "roundtrip_diag.txt");
        using (StreamWriter sw = new(rtDiagPath))
        {
            SkeletonBone[] inputBones = scene.GetDescendants<SkeletonBone>();
            SkeletonBone[] outputBones = reloadedScene.GetDescendants<SkeletonBone>();
            Mesh[] inputMeshes = scene.GetDescendants<Mesh>();
            Mesh[] outputMeshes = reloadedScene.GetDescendants<Mesh>();

            sw.WriteLine($"Input : Bones={inputBones.Length}, Meshes={inputMeshes.Length}");
            sw.WriteLine($"Output: Bones={outputBones.Length}, Meshes={outputMeshes.Length}");
            sw.WriteLine();

            // Compare first 10 bone world positions
            sw.WriteLine("=== Bone World Position Comparison (first 10) ===");
            for (int i = 0; i < Math.Min(10, Math.Min(inputBones.Length, outputBones.Length)); i++)
            {
                Vector3 inPos = inputBones[i].GetBindWorldPosition();
                Vector3 outPos = outputBones[i].GetBindWorldPosition();
                float dist = Vector3.Distance(inPos, outPos);
                sw.WriteLine($"  [{i}] {inputBones[i].Name} → {outputBones[i].Name}: in={inPos} out={outPos} dist={dist:F6}");
            }

            sw.WriteLine();

            // Compare mesh vertex bounds
            sw.WriteLine("=== Mesh Vertex Comparison ===");
            for (int m = 0; m < Math.Min(inputMeshes.Length, outputMeshes.Length); m++)
            {
                Mesh inMesh = inputMeshes[m];
                Mesh outMesh = outputMeshes[m];

                if (inMesh.Positions == null || outMesh.Positions == null) continue;

                // Get first vertex of each
                Vector3 inV0 = inMesh.Positions.GetVector3(0, 0);
                Vector3 outV0 = outMesh.Positions.GetVector3(0, 0);
                float dist = Vector3.Distance(inV0, outV0);

                sw.WriteLine($"  [{m}] {inMesh.Name}→{outMesh.Name}: verts in={inMesh.Positions.ElementCount} out={outMesh.Positions.ElementCount}");
                sw.WriteLine($"    v0_in={inV0} v0_out={outV0} dist={dist:F6}");
                sw.WriteLine($"    inSkinned={inMesh.HasSkinning}({inMesh.SkinnedBones?.Count ?? 0}) outSkinned={outMesh.HasSkinning}({outMesh.SkinnedBones?.Count ?? 0})");
            }
        }
    }

    [Fact]
    public void Diagnostic_FbxVsSemodel_SkinnedVertexComparison()
    {
        string fbxPath = GetWorkspaceAssetPath("RealTalk.fbx");
        string semodelPath = GetWorkspaceAssetPath("RealTalk.semodel");
        if (!File.Exists(fbxPath) || !File.Exists(semodelPath))
        {
            return;
        }

        SceneTranslatorManager manager = new();
        manager.Register(new FbxTranslator());
        manager.Register(new SemodelTranslator());

        Scene fbxScene;
        using (FileStream fs = File.OpenRead(fbxPath))
        {
            fbxScene = manager.Read(fs, fbxPath, new SceneTranslatorOptions(), token: null);
        }

        Scene semodelScene;
        using (FileStream fs = File.OpenRead(semodelPath))
        {
            semodelScene = manager.Read(fs, semodelPath, new SceneTranslatorOptions(), token: null);
        }

        Mesh[] fbxMeshes = fbxScene.GetDescendants<Mesh>();
        Mesh[] seMeshes = semodelScene.GetDescendants<Mesh>();

        StringBuilder report = new();
        report.AppendLine($"FBX meshes: {fbxMeshes.Length}, SEModel meshes: {seMeshes.Length}");

        // List all mesh names
        report.AppendLine("\n=== FBX Meshes ===");
        foreach (Mesh m in fbxMeshes)
        {
            report.AppendLine($"  {m.Name}: verts={m.VertexCount} skinned={m.HasSkinning} bones={m.SkinnedBones?.Count ?? 0}");
        }

        report.AppendLine("\n=== SEModel Meshes ===");
        foreach (Mesh m in seMeshes)
        {
            report.AppendLine($"  {m.Name}: verts={m.VertexCount} skinned={m.HasSkinning} bones={m.SkinnedBones?.Count ?? 0}");
        }

        // For each FBX mesh, find matching SEModel mesh by name suffix and compare
        float overallMaxDelta = 0f;
        string worstMeshName = "";
        int worstVertex = -1;

        foreach (Mesh fbxMesh in fbxMeshes)
        {
            // Match by index suffix (SEModelMesh, SEModelMesh2, etc.)
            string fbxSuffix = fbxMesh.Name;
            if (fbxSuffix.Contains(':'))
            {
                fbxSuffix = fbxSuffix[(fbxSuffix.LastIndexOf(':') + 1)..];
            }

            Mesh? seMesh = seMeshes.FirstOrDefault(m => string.Equals(m.Name, fbxSuffix, StringComparison.OrdinalIgnoreCase));
            if (seMesh is null)
            {
                report.AppendLine($"\n  No SEModel match for FBX mesh '{fbxMesh.Name}' (suffix='{fbxSuffix}')");
                continue;
            }

            int sampleCount = Math.Min(fbxMesh.VertexCount, seMesh.VertexCount);
            float maxDelta = 0f;
            int maxDeltaVertex = -1;
            Vector3 maxDeltaSrc = Vector3.Zero;
            Vector3 maxDeltaDst = Vector3.Zero;

            // Compare skinned positions (FBX skinned vs SEModel raw which ARE the baked positions)
            for (int v = 0; v < sampleCount; v++)
            {
                Vector3 fbxSkinned = fbxMesh.GetVertexPosition(v, raw: false);
                Vector3 seRaw = seMesh.GetVertexPosition(v, raw: true);
                float delta = Vector3.Distance(fbxSkinned, seRaw);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxDeltaVertex = v;
                    maxDeltaSrc = fbxSkinned;
                    maxDeltaDst = seRaw;
                }
            }

            // Also compare raw-vs-raw
            float maxRawDelta = 0f;
            for (int v = 0; v < sampleCount; v++)
            {
                Vector3 fbxRaw = fbxMesh.GetVertexPosition(v, raw: true);
                Vector3 seRaw = seMesh.GetVertexPosition(v, raw: true);
                float delta = Vector3.Distance(fbxRaw, seRaw);
                maxRawDelta = Math.Max(maxRawDelta, delta);
            }

            report.AppendLine($"\n=== {fbxMesh.Name} vs {seMesh.Name} ({sampleCount} verts) ===");
            report.AppendLine($"  Max FBX-skinned vs SE-raw delta: {maxDelta:F6} at vtx={maxDeltaVertex}");
            report.AppendLine($"    FBX skinned: {maxDeltaSrc}");
            report.AppendLine($"    SE raw:      {maxDeltaDst}");
            report.AppendLine($"  Max raw-vs-raw delta: {maxRawDelta:F6}");

            if (maxDelta > overallMaxDelta)
            {
                overallMaxDelta = maxDelta;
                worstMeshName = fbxMesh.Name;
                worstVertex = maxDeltaVertex;
            }
        }

        // Also dump some bone info
        SkeletonBone[] fbxBones = fbxScene.GetDescendants<SkeletonBone>();
        SkeletonBone[] seBones = semodelScene.GetDescendants<SkeletonBone>();
        report.AppendLine($"\n=== Bones: FBX={fbxBones.Length} SE={seBones.Length} ===");

        // Compare matching bones
        foreach (SkeletonBone fbxBone in fbxBones.Take(30))
        {
            string boneSuffix = fbxBone.Name;
            if (boneSuffix.Contains(':'))
            {
                boneSuffix = boneSuffix[(boneSuffix.LastIndexOf(':') + 1)..];
            }

            SkeletonBone? seBone = seBones.FirstOrDefault(b => string.Equals(b.Name, boneSuffix, StringComparison.OrdinalIgnoreCase));
            if (seBone is null)
            {
                continue;
            }

            Vector3 fbxBindWorld = fbxBone.GetBindWorldPosition();
            Vector3 fbxActiveWorld = fbxBone.GetActiveWorldPosition();
            Vector3 seBindWorld = seBone.GetBindWorldPosition();

            float bindDelta = Vector3.Distance(fbxBindWorld, seBindWorld);
            float activeDelta = Vector3.Distance(fbxActiveWorld, seBindWorld);
            report.AppendLine($"  {boneSuffix}: bindDelta={bindDelta:F4} activeDelta={activeDelta:F4} fbxBind={fbxBindWorld} fbxActive={fbxActiveWorld} seBind={seBindWorld}");
        }

        Assert.True(overallMaxDelta < 0.001f,
            $"FBX skinned vertices differ from SEModel ground truth. Worst: {worstMeshName} vtx={worstVertex} delta={overallMaxDelta:F4}\n{report}");

        // Now round-trip the FBX and compare RT skinned vertices against SEModel ground truth
        using MemoryStream rtOutput = new();
        manager.Write(rtOutput, "roundtrip.fbx", fbxScene, new SceneTranslatorOptions(), token: null);
        rtOutput.Position = 0;

        Scene rtScene = manager.Read(rtOutput, "roundtrip.fbx", new SceneTranslatorOptions(), token: null);
        Mesh[] rtMeshes = rtScene.GetDescendants<Mesh>();

        StringBuilder rtReport = new();
        float rtOverallMaxDelta = 0f;
        string rtWorstMeshName = "";
        int rtWorstVertex = -1;

        foreach (Mesh seMesh in seMeshes)
        {
            Mesh? rtMesh = rtMeshes.FirstOrDefault(m =>
            {
                string suffix = m.Name;
                if (suffix.Contains(':'))
                {
                    suffix = suffix[(suffix.LastIndexOf(':') + 1)..];
                }

                return string.Equals(suffix, seMesh.Name, StringComparison.OrdinalIgnoreCase);
            });

            if (rtMesh is null)
            {
                rtReport.AppendLine($"  No RT mesh match for SE '{seMesh.Name}'");
                continue;
            }

            int sampleCount = Math.Min(rtMesh.VertexCount, seMesh.VertexCount);
            float maxDelta = 0f;
            int maxDeltaVertex = -1;
            Vector3 maxSrc = Vector3.Zero;
            Vector3 maxDst = Vector3.Zero;

            for (int v = 0; v < sampleCount; v++)
            {
                Vector3 rtSkinned = rtMesh.GetVertexPosition(v, raw: false);
                Vector3 seRaw = seMesh.GetVertexPosition(v, raw: true);
                float delta = Vector3.Distance(rtSkinned, seRaw);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxDeltaVertex = v;
                    maxSrc = rtSkinned;
                    maxDst = seRaw;
                }
            }

            rtReport.AppendLine($"\n  {rtMesh.Name} vs {seMesh.Name}: maxDelta={maxDelta:F6} at vtx={maxDeltaVertex}");
            rtReport.AppendLine($"    RT skinned: {maxSrc}");
            rtReport.AppendLine($"    SE raw:     {maxDst}");

            if (maxDelta > rtOverallMaxDelta)
            {
                rtOverallMaxDelta = maxDelta;
                rtWorstMeshName = rtMesh.Name;
                rtWorstVertex = maxDeltaVertex;
            }
        }

        // Dump worst RT bones
        SkeletonBone[] rtBones = rtScene.GetDescendants<SkeletonBone>();
        foreach (SkeletonBone rtBone in rtBones.Take(30))
        {
            string suffix = rtBone.Name;
            if (suffix.Contains(':'))
            {
                suffix = suffix[(suffix.LastIndexOf(':') + 1)..];
            }

            SkeletonBone? seBone = seBones.FirstOrDefault(b => string.Equals(b.Name, suffix, StringComparison.OrdinalIgnoreCase));
            if (seBone is null)
            {
                continue;
            }

            Vector3 rtActive = rtBone.GetActiveWorldPosition();
            Vector3 seWorld = seBone.GetBindWorldPosition();
            float boneDelta = Vector3.Distance(rtActive, seWorld);
            if (boneDelta > 0.01f)
            {
                rtReport.AppendLine($"  BONE {suffix}: rtActive={rtActive} seWorld={seWorld} delta={boneDelta:F4}");
            }
        }

        Assert.True(rtOverallMaxDelta < 0.05f,
            $"RT FBX skinned vertices differ from SEModel ground truth. Worst: {rtWorstMeshName} vtx={rtWorstVertex} delta={rtOverallMaxDelta:F4}\n{rtReport}");
    }

    [Fact]
    public void Diagnostic_FbxRoundTrip_ClusterMatricesMatch()
    {
        string fbxPath = GetWorkspaceAssetPath("RealTalk.fbx");
        if (!File.Exists(fbxPath))
        {
            return;
        }

        // Read original FBX document and extract cluster matrices
        FbxDocument originalDoc;
        using (FileStream fs = File.OpenRead(fbxPath))
        {
            originalDoc = FbxDocumentIO.Read(fs);
        }

        Dictionary<string, (Matrix4x4 Transform, Matrix4x4 TransformLink)> originalClusters = ExtractClusterMatrices(originalDoc);

        // Import scene (full pipeline with strip + rebuild)
        SceneTranslatorManager manager = new();
        manager.Register(new FbxTranslator());

        Scene scene;
        using (FileStream fs = File.OpenRead(fbxPath))
        {
            scene = manager.Read(fs, fbxPath, new SceneTranslatorOptions(), token: null);
        }

        // Export back to FBX
        FbxDocument exportedDoc = FbxSceneMapper.ExportScene(scene, FbxFormat.Binary);

        // Extract exported cluster matrices
        Dictionary<string, (Matrix4x4 Transform, Matrix4x4 TransformLink)> exportedClusters = ExtractClusterMatrices(exportedDoc);

        // Compare
        StringBuilder report = new();
        report.AppendLine($"Original clusters: {originalClusters.Count}, Exported clusters: {exportedClusters.Count}");

        float maxTransformDelta = 0f;
        float maxTransformLinkDelta = 0f;
        string worstCluster = "";

        // Also dump the mesh model transforms
        FbxNode? origObjects = originalDoc.FirstNode("Objects") ?? originalDoc.FirstNodeRecursive("Objects");
        FbxNode? exportObjects = exportedDoc.FirstNode("Objects") ?? exportedDoc.FirstNodeRecursive("Objects");

        report.AppendLine("\n=== Mesh Model Transforms (original) ===");
        if (origObjects is not null)
        {
            foreach (FbxNode obj in origObjects.Children)
            {
                if (!string.Equals(obj.Name, "Model", StringComparison.Ordinal) || obj.Properties.Count < 3)
                {
                    continue;
                }

                string modelType = obj.Properties[2].AsString();
                if (!string.Equals(modelType, "Mesh", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = FbxSceneMapper.GetNodeObjectName(obj);
                FbxNode? props = obj.FirstChild("Properties70");
                if (props is null)
                {
                    continue;
                }

                Vector3 lclT = FbxSceneMapper.GetPropertyVector3(props, "Lcl Translation", Vector3.Zero);
                Vector3 lclR = FbxSceneMapper.GetPropertyVector3(props, "Lcl Rotation", Vector3.Zero);
                Vector3 lclS = FbxSceneMapper.GetPropertyVector3(props, "Lcl Scaling", Vector3.One);
                Vector3 preRot = FbxSceneMapper.GetPropertyVector3(props, "PreRotation", Vector3.Zero);
                Vector3 geoT = FbxSceneMapper.GetPropertyVector3(props, "GeometricTranslation", Vector3.Zero);
                Vector3 geoR = FbxSceneMapper.GetPropertyVector3(props, "GeometricRotation", Vector3.Zero);
                Vector3 geoS = FbxSceneMapper.GetPropertyVector3(props, "GeometricScaling", Vector3.One);
                report.AppendLine($"  {name}: LclT={lclT} LclR={lclR} LclS={lclS} PreRot={preRot} GeoT={geoT} GeoR={geoR} GeoS={geoS}");
            }
        }

        report.AppendLine("\n=== Mesh Model Transforms (exported) ===");
        if (exportObjects is not null)
        {
            foreach (FbxNode obj in exportObjects.Children)
            {
                if (!string.Equals(obj.Name, "Model", StringComparison.Ordinal) || obj.Properties.Count < 3)
                {
                    continue;
                }

                string modelType = obj.Properties[2].AsString();
                if (!string.Equals(modelType, "Mesh", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = FbxSceneMapper.GetNodeObjectName(obj);
                FbxNode? props = obj.FirstChild("Properties70");
                if (props is null)
                {
                    continue;
                }

                Vector3 lclT = FbxSceneMapper.GetPropertyVector3(props, "Lcl Translation", Vector3.Zero);
                Vector3 lclR = FbxSceneMapper.GetPropertyVector3(props, "Lcl Rotation", Vector3.Zero);
                Vector3 lclS = FbxSceneMapper.GetPropertyVector3(props, "Lcl Scaling", Vector3.One);
                Vector3 preRot = FbxSceneMapper.GetPropertyVector3(props, "PreRotation", Vector3.Zero);
                report.AppendLine($"  {name}: LclT={lclT} LclR={lclR} LclS={lclS} PreRot={preRot}");
            }
        }

        report.AppendLine("\n=== Cluster Matrix Comparison ===");
        foreach ((string clusterName, (Matrix4x4 origTransform, Matrix4x4 origTransformLink)) in originalClusters)
        {
            if (!exportedClusters.TryGetValue(clusterName, out (Matrix4x4 Transform, Matrix4x4 TransformLink) exported))
            {
                report.AppendLine($"  {clusterName}: MISSING in exported");
                continue;
            }

            float transformDelta = MatrixDistance(origTransform, exported.Transform);
            float transformLinkDelta = MatrixDistance(origTransformLink, exported.TransformLink);

            Matrix4x4 origIbm = ComputeIbm(origTransform, origTransformLink);
            Matrix4x4 exportedIbm = ComputeIbm(exported.Transform, exported.TransformLink);
            float ibmDelta = MatrixDistance(origIbm, exportedIbm);

            if (transformDelta > 0.01f || transformLinkDelta > 0.01f || ibmDelta > 0.01f)
            {
                report.AppendLine($"  {clusterName}: Transform delta={transformDelta:F6} TransformLink delta={transformLinkDelta:F6} IBM delta={ibmDelta:F6}");
                report.AppendLine($"    Orig Transform:     [{FormatMatrix(origTransform)}]");
                report.AppendLine($"    Export Transform:   [{FormatMatrix(exported.Transform)}]");
                report.AppendLine($"    Orig TransformLink: [{FormatMatrix(origTransformLink)}]");
                report.AppendLine($"    Exp  TransformLink: [{FormatMatrix(exported.TransformLink)}]");
                report.AppendLine($"    Orig IBM:           [{FormatMatrix(origIbm)}]");
                report.AppendLine($"    Exp  IBM:           [{FormatMatrix(exportedIbm)}]");
            }

            if (transformDelta > maxTransformDelta)
            {
                maxTransformDelta = transformDelta;
                worstCluster = clusterName;
            }

            maxTransformLinkDelta = MathF.Max(maxTransformLinkDelta, transformLinkDelta);
        }

        report.AppendLine($"\nMax Transform delta: {maxTransformDelta:F6} (worst: {worstCluster})");
        report.AppendLine($"Max TransformLink delta: {maxTransformLinkDelta:F6}");

        // Also dump the scene graph post-import bind and export bind worlds
        Mesh[] meshes = scene.GetDescendants<Mesh>();
        report.AppendLine($"\n=== Scene Graph Post-Import ===");
        foreach (Mesh mesh in meshes.Take(5))
        {
            Matrix4x4 bindWorld = mesh.GetBindWorldMatrix();
            Matrix4x4 exportWorld = FbxSceneMapper.GetExportBindWorldMatrix(mesh);
            report.AppendLine($"  {mesh.Name}: bindWorld_zup pos=({bindWorld.M41:F2},{bindWorld.M42:F2},{bindWorld.M43:F2})");
            report.AppendLine($"  {mesh.Name}: exportWorld_yup pos=({exportWorld.M41:F2},{exportWorld.M42:F2},{exportWorld.M43:F2})");
            report.AppendLine($"    HasSkinning={mesh.HasSkinning} HasExplicit={mesh.HasExplicitInverseBindMatrices}");

            if (mesh.SkinnedBones is { Count: > 0 } bones && mesh.InverseBindMatrices is { Count: > 0 } ibms)
            {
                for (int i = 0; i < Math.Min(3, bones.Count); i++)
                {
                    SkeletonBone bone = bones[i];
                    Matrix4x4 boneBindWorld = bone.GetBindWorldMatrix();
                    Matrix4x4 boneExportWorld = FbxSceneMapper.GetExportBindWorldMatrix(bone);
                    report.AppendLine($"    bone[{i}] {bone.Name}: bindWorld=({boneBindWorld.M41:F2},{boneBindWorld.M42:F2},{boneBindWorld.M43:F2}) exportWorld=({boneExportWorld.M41:F2},{boneExportWorld.M42:F2},{boneExportWorld.M43:F2})");
                    report.AppendLine($"      IBM = [{FormatMatrix(ibms[i])}]");

                    Matrix4x4 exportIbm = ComputeIbm(exportWorld, boneExportWorld);
                    report.AppendLine($"      ExportIBM = [{FormatMatrix(exportIbm)}]");
                }
            }
        }

        Assert.True(maxTransformDelta < 0.1f,
            $"Cluster Transform matrices diverge after round-trip. Worst: {worstCluster} delta={maxTransformDelta:F4}\n{report}");

        // Write report to file for inspection
        string outputDir = Path.Combine(Path.GetTempPath(), "RedFox_FbxDiag");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "cluster_roundtrip.txt"), report.ToString());
    }

    private static Dictionary<string, (Matrix4x4 Transform, Matrix4x4 TransformLink)> ExtractClusterMatrices(FbxDocument document)
    {
        Dictionary<string, (Matrix4x4 Transform, Matrix4x4 TransformLink)> result = [];
        FbxNode? objectsNode = document.FirstNode("Objects") ?? document.FirstNodeRecursive("Objects");
        if (objectsNode is null)
        {
            return result;
        }

        foreach (FbxNode obj in objectsNode.Children)
        {
            if (!string.Equals(obj.Name, "Deformer", StringComparison.Ordinal))
            {
                continue;
            }

            if (obj.Properties.Count < 3 || !string.Equals(obj.Properties[2].AsString(), "Cluster", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string clusterName = obj.Properties.Count > 1 ? obj.Properties[1].AsString() : "unknown";
            int sepIdx = clusterName.IndexOf('\0');
            if (sepIdx >= 0)
            {
                clusterName = clusterName[..sepIdx];
            }

            Matrix4x4 transform = FbxSkinningMapper.ReadNodeMatrix(obj, "Transform");
            Matrix4x4 transformLink = FbxSkinningMapper.ReadNodeMatrix(obj, "TransformLink");
            result[clusterName] = (transform, transformLink);
        }

        return result;
    }

    private static Matrix4x4 ComputeIbm(Matrix4x4 transform, Matrix4x4 transformLink)
    {
        if (!Matrix4x4.Invert(transformLink, out Matrix4x4 inv))
        {
            return Matrix4x4.Identity;
        }

        return transform * inv;
    }

    private static float MatrixDistance(Matrix4x4 a, Matrix4x4 b)
    {
        float sum = 0;
        sum += MathF.Abs(a.M11 - b.M11) + MathF.Abs(a.M12 - b.M12) + MathF.Abs(a.M13 - b.M13) + MathF.Abs(a.M14 - b.M14);
        sum += MathF.Abs(a.M21 - b.M21) + MathF.Abs(a.M22 - b.M22) + MathF.Abs(a.M23 - b.M23) + MathF.Abs(a.M24 - b.M24);
        sum += MathF.Abs(a.M31 - b.M31) + MathF.Abs(a.M32 - b.M32) + MathF.Abs(a.M33 - b.M33) + MathF.Abs(a.M34 - b.M34);
        sum += MathF.Abs(a.M41 - b.M41) + MathF.Abs(a.M42 - b.M42) + MathF.Abs(a.M43 - b.M43) + MathF.Abs(a.M44 - b.M44);
        return sum;
    }

    private static string FormatMatrix(Matrix4x4 m)
    {
        return $"({m.M11:F3},{m.M12:F3},{m.M13:F3},{m.M14:F3}|{m.M21:F3},{m.M22:F3},{m.M23:F3},{m.M24:F3}|{m.M31:F3},{m.M32:F3},{m.M33:F3},{m.M34:F3}|{m.M41:F3},{m.M42:F3},{m.M43:F3},{m.M44:F3})";
    }

    [Fact]
    public void Diagnostic_FbxExport_MayaBindPoseSimulation()
    {
        // Export RealTalk.fbx to ASCII and inspect the output
        string fbxPath = GetWorkspaceAssetPath("RealTalk.fbx");
        if (!File.Exists(fbxPath))
        {
            return;
        }

        SceneTranslatorManager manager = new();
        manager.Register(new FbxTranslator());

        Scene scene;
        using (FileStream fs = File.OpenRead(fbxPath))
        {
            scene = manager.Read(fs, fbxPath, new SceneTranslatorOptions(), token: null);
        }

        // Write to temp directory for inspection
        string outputDir = Path.Combine(Path.GetTempPath(), "RedFox_FbxDiag");
        Directory.CreateDirectory(outputDir);

        string binaryOutputPath = Path.Combine(outputDir, "RealTalk_roundtrip.fbx");
        using (FileStream output = File.Create(binaryOutputPath))
        {
            manager.Write(output, binaryOutputPath, scene, new SceneTranslatorOptions(), token: null);
        }

        // Also export to ASCII for inspection
        string asciiOutputPath = Path.Combine(outputDir, "RealTalk_roundtrip.ascii.fbx");
        using (FileStream output = File.Create(asciiOutputPath))
        {
            manager.Write(output, asciiOutputPath, scene, new SceneTranslatorOptions(), token: null);
        }

        Assert.True(File.Exists(binaryOutputPath), "Binary FBX output not created");
        Assert.True(File.Exists(asciiOutputPath), "ASCII FBX output not created");

        // Re-import the binary output and compare skinned vertices
        Scene reimportedScene;
        using (FileStream fs = File.OpenRead(binaryOutputPath))
        {
            reimportedScene = manager.Read(fs, binaryOutputPath, new SceneTranslatorOptions(), token: null);
        }

        Mesh[] originalMeshes = scene.GetDescendants<Mesh>();
        Mesh[] reimportedMeshes = reimportedScene.GetDescendants<Mesh>();

        StringBuilder report = new();
        report.AppendLine($"Original meshes: {originalMeshes.Length}, Reimported meshes: {reimportedMeshes.Length}");

        float worstDelta = 0;
        string worstMesh = "";
        int worstVtx = -1;

        for (int m = 0; m < Math.Min(originalMeshes.Length, reimportedMeshes.Length); m++)
        {
            Mesh orig = originalMeshes[m];
            Mesh reimp = reimportedMeshes[m];

            report.AppendLine($"\n=== Mesh '{orig.Name}' → '{reimp.Name}' ===");
            report.AppendLine($"  Verts: {orig.VertexCount} → {reimp.VertexCount}");
            report.AppendLine($"  Skinned: {orig.HasSkinning}({orig.SkinnedBones?.Count ?? 0}) → {reimp.HasSkinning}({reimp.SkinnedBones?.Count ?? 0})");
            report.AppendLine($"  HasExplicit: {orig.HasExplicitInverseBindMatrices} → {reimp.HasExplicitInverseBindMatrices}");

            if (orig.VertexCount == 0 || reimp.VertexCount == 0)
            {
                continue;
            }

            // Compare raw vertex 0
            Vector3 origRaw = orig.GetVertexPosition(0, raw: true);
            Vector3 reimpRaw = reimp.GetVertexPosition(0, raw: true);
            report.AppendLine($"  v0 raw: {origRaw} → {reimpRaw} delta={Vector3.Distance(origRaw, reimpRaw):F6}");

            // Compare skinned vertex 0
            Vector3 origSkinned = orig.GetVertexPosition(0, raw: false);
            Vector3 reimpSkinned = reimp.GetVertexPosition(0, raw: false);
            float skinnedDelta = Vector3.Distance(origSkinned, reimpSkinned);
            report.AppendLine($"  v0 skinned: {origSkinned} → {reimpSkinned} delta={skinnedDelta:F6}");

            // Check ALL vertices
            int sampleCount = Math.Min(orig.VertexCount, reimp.VertexCount);
            float maxDelta = 0;
            int maxDeltaVtx = -1;
            for (int v = 0; v < sampleCount; v++)
            {
                Vector3 a = orig.GetVertexPosition(v, raw: false);
                Vector3 b = reimp.GetVertexPosition(v, raw: false);
                float d = Vector3.Distance(a, b);
                if (d > maxDelta)
                {
                    maxDelta = d;
                    maxDeltaVtx = v;
                }
            }

            report.AppendLine($"  Max skinned delta: {maxDelta:F6} at vtx={maxDeltaVtx}");
            if (maxDelta > worstDelta)
            {
                worstDelta = maxDelta;
                worstMesh = orig.Name;
                worstVtx = maxDeltaVtx;
            }
        }

        report.AppendLine($"\nOverall worst: {worstMesh} vtx={worstVtx} delta={worstDelta:F6}");
        report.AppendLine($"\nExported files:");
        report.AppendLine($"  Binary: {binaryOutputPath}");
        report.AppendLine($"  ASCII: {asciiOutputPath}");
        File.WriteAllText(Path.Combine(outputDir, "roundtrip_comparison.txt"), report.ToString());

        // This threshold is generous — primarily we want to generate the files + report
        Assert.True(worstDelta < 1.0f,
            $"Round-trip skinned vertex delta too large. Worst: {worstMesh} vtx={worstVtx} delta={worstDelta:F4}\n{report}");
    }
}
