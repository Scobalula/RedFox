using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
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
    public void FbxTranslator_Write_UsesExactBindLocalTransforms()
    {
        Scene sourceScene = CreateSampleScene();
        Skeleton skeleton = sourceScene.GetDescendants<Skeleton>()[0];
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
        Skeleton rigRoot = Assert.Single(scene.EnumerateChildren().OfType<Skeleton>(), static skeleton => skeleton.Name == "RigRoot");

        Assert.Equal(2, rigRoot.GetBones().Length);
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
        Skeleton rigRoot = Assert.Single(scene.EnumerateChildren().OfType<Skeleton>(), static skeleton => skeleton.Name == "RigRoot");

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
        Assert.True(File.Exists(realTalkPath), $"Expected test asset at: {realTalkPath}");

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
}
