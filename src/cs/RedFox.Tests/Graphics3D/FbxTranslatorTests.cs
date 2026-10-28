using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Formats.KaydaraFbx;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.Tests.Audio;

namespace RedFox.Tests.Graphics3D;

public sealed class FbxTranslatorTests
{
    [Theory]
    [InlineData("sample.fbx")]
    [InlineData("sample.ascii.fbx")]
    public void FbxTranslator_RoundTrip_PreservesMeshMaterialSkeletonAndSkin(string fileName)
    {
        Scene source = CreateSampleScene();
        Scene loaded = RoundTrip(source, fileName);

        Mesh sourceMesh = Assert.Single(source.GetDescendants<Mesh>());
        Mesh loadedMesh = Assert.Single(loaded.GetDescendants<Mesh>());

        Assert.Equal(sourceMesh.FaceCount, loadedMesh.FaceCount);
        Assert.Equal(2, loadedMesh.UVLayerCount);
        Assert.Equal(2, loaded.GetDescendants<SkeletonBone>().Length);
        Assert.NotNull(loadedMesh.Skin);
        Assert.Equal("material_0", Assert.Single(loadedMesh.Materials!).Name);
        Assert.Equal(new Vector4(0.7f, 0.6f, 0.5f, 1.0f), loadedMesh.Materials![0].DiffuseColor);

        AssertSkinnedPositionsEqual(sourceMesh, loadedMesh);
    }

    [Fact]
    public void FbxTranslator_Write_FlipsUVsToBottomLeftOriginAndBackOnRead()
    {
        Scene       source      = CreateSampleScene();
        Mesh        mesh        = Assert.Single(source.GetDescendants<Mesh>());
        FbxDocument document    = FbxSceneExporter.Export(new SceneTranslationSelection(source, SceneNodeFlags.None), new FbxWriteOptions(), null);
        FbxNode     geometry    = document.FirstNode("Objects")!.ChildrenNamed("Geometry").First();
        double[]    uvs         = geometry.FirstChild("LayerElementUV")!.FirstChild("UV")!.GetDoubles();

        Assert.Equal(1.0 - mesh.UVLayers!.Get<float>(1, 0, 1), uvs[3], 5);

        Mesh loaded = Assert.Single(RoundTrip(source, "uvs.fbx").GetDescendants<Mesh>());

        for (int vertex = 0; vertex < loaded.VertexCount; vertex++)
        {
            Vector3 position = loaded.GetVertexPosition(vertex, true);
            int     original = Enumerable.Range(0, mesh.VertexCount).First(index => mesh.GetVertexPosition(index, true) == position);

            Assert.True(Vector2.Distance(mesh.UVLayers.GetVector2(original, 0), loaded.UVLayers!.GetVector2(vertex, 0)) < 1e-5f);
            Assert.True(Vector2.Distance(mesh.UVLayers.GetVector2(original, 1), loaded.UVLayers.GetVector2(vertex, 1)) < 1e-5f);
        }
    }

    [Fact]
    public void FbxTranslator_RoundTrip_PreservesBoneWorldTransforms()
    {
        Scene source = CreateSampleScene();
        SkeletonBone child = source.GetDescendants<SkeletonBone>().Single(static bone => bone.Name == "child");

        child.BindTransform.LocalRotation = Quaternion.CreateFromYawPitchRoll(0.3f, -1.2f, 2.1f);
        child.BindTransform.Scale = new Vector3(1.0f, 2.0f, 0.5f);

        Scene loaded = RoundTrip(source, "bones.fbx");
        SkeletonBone loadedChild = loaded.GetDescendants<SkeletonBone>().Single(static bone => bone.Name == "child");

        AssertMatrixEqual(child.GetBindWorldMatrix(), loadedChild.GetBindWorldMatrix());
    }

    [Fact]
    public void FbxTranslator_RoundTrip_PreservesUpAxisCameraAndLight()
    {
        Scene source = new("Lights")
        {
            UpAxis = SceneUpAxis.Z,
        };

        source.RootNode.AddNode(new Camera("camera") { FieldOfView = 40.0f, NearPlane = 0.5f, FarPlane = 500.0f });
        source.RootNode.AddNode(new Light("light") { Type = LightType.Spot, Intensity = 2.0f, Color = new Vector3(1.0f, 0.5f, 0.25f), InnerConeAngle = 10.0f, OuterConeAngle = 30.0f });

        Scene	loaded	= RoundTrip(source, "lights.fbx");
        Camera	camera	= Assert.Single(loaded.GetDescendants<Camera>());
        Light	light	= Assert.Single(loaded.GetDescendants<Light>());

        Assert.Equal(SceneUpAxis.Z, loaded.UpAxis);
        Assert.Equal(40.0f, camera.FieldOfView, 3);
        Assert.Equal(0.5f, camera.NearPlane, 3);
        Assert.Equal(LightType.Spot, light.Type);
        Assert.Equal(2.0f, light.Intensity, 3);
        Assert.Equal(30.0f, light.OuterConeAngle, 3);
        Assert.Equal(new Vector3(1.0f, 0.5f, 0.25f), light.Color);
    }

    [Fact]
    public void FbxTranslator_RoundTrip_PreservesSkeletonAnimation()
    {
        Scene					source		= CreateSampleScene();
        SkeletonAnimation		animation	= new("walk") { Framerate = 30.0f, TransformType = TransformType.Absolute, TransformSpace = TransformSpace.Local };
        SkeletonAnimationTrack	track		= new("child") { TransformType = TransformType.Absolute, TransformSpace = TransformSpace.Local };

        for (int frame = 0; frame <= 10; frame++)
        {
            track.AddTranslationFrame(frame, new Vector3(0.0f, 1.0f + frame * 0.1f, 0.0f));
            track.AddRotationFrame(frame, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, frame * 0.4f));
        }

        animation.Tracks.Add(track);
        source.RootNode.AddNode(animation);

        Scene					loaded			= RoundTrip(source, "walk.fbx");
        SkeletonAnimation		imported		= Assert.Single(loaded.GetDescendants<SkeletonAnimation>());
        SkeletonAnimationTrack	importedTrack	= Assert.Single(imported.Tracks, static candidate => candidate.Name == "child");

        Assert.Equal("walk", imported.Name);
        Assert.Equal(30.0f, imported.Framerate);

        for (int frame = 0; frame <= 10; frame++)
        {
            Assert.True(Vector3.Distance(track.TranslationCurve!.SampleVector3(frame), importedTrack.TranslationCurve!.SampleVector3(frame)) < 1e-4f);
            Assert.True(MathF.Abs(Quaternion.Dot(track.RotationCurve!.SampleQuaternion(frame), importedTrack.RotationCurve!.SampleQuaternion(frame))) > 0.9999f);
        }
    }

    [Fact]
    public void FbxTranslator_RoundTrip_PreservesMorphTargetsAndMorphAnimation()
    {
        Scene	source	= CreateSampleScene();
        Mesh	mesh	= Assert.Single(source.GetDescendants<Mesh>());

        mesh.Morph = new Morph(["smile"], new DataBuffer<float>([0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 2.0f], 1, 3));

        MorphAnimation animation = new("blink") { Framerate = 24.0f };
        MorphAnimationTrack track = animation.GetOrCreateTrack("smile");

        track.AddWeightFrame(0, 0.0f);
        track.AddWeightFrame(4, 1.0f);
        source.RootNode.AddNode(animation);

        Scene				loaded			= RoundTrip(source, "morph.fbx");
        Mesh				loadedMesh		= Assert.Single(loaded.GetDescendants<Mesh>());
        MorphAnimationTrack	importedTrack	= Assert.Single(Assert.Single(loaded.GetDescendants<MorphAnimation>()).Tracks);

        Assert.Equal(["smile"], loadedMesh.Morph!.TargetNames);
        Assert.Equal("smile", importedTrack.Name);
        Assert.Equal(0.5f, importedTrack.SampleWeight(2), 3);

        loadedMesh.Morph.Weights[0] = 1.0f;
        mesh.Morph.Weights[0] = 1.0f;
        AssertSkinnedPositionsEqual(mesh, loadedMesh);
    }

    [Fact]
    public void FbxTranslator_RoundTrip_PreservesConstraints()
    {
        Scene			source	= CreateSampleScene();
        SkeletonBone[]	bones	= source.GetDescendants<SkeletonBone>();

        source.RootNode.AddNode(new ParentConstraintNode("follow", bones[1], bones[0]) { Weight = 0.5f, TranslationOffset = new Vector3(1.0f, 2.0f, 3.0f) });
        source.RootNode.AddNode(new PointConstraintNode("point", bones[0], bones[1]) { SkipY = true });

        Scene					loaded	= RoundTrip(source, "constraints.fbx");
        ParentConstraintNode	parent	= Assert.Single(loaded.GetDescendants<ParentConstraintNode>());
        PointConstraintNode		point	= Assert.Single(loaded.GetDescendants<PointConstraintNode>());

        Assert.Equal("child", parent.ConstrainedNode.Name);
        Assert.Equal("root", parent.SourceNode.Name);
        Assert.Equal(0.5f, parent.Weight, 3);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), parent.TranslationOffset);
        Assert.True(point.SkipY);
        Assert.False(point.SkipX);
    }

    [Fact]
    public void FbxTranslator_Read_SplitsMultipleMaterialsIntoSubMeshes()
    {
        Scene		source	= CreateSampleScene();
        Mesh		mesh	= Assert.Single(source.GetDescendants<Mesh>());
        Material	second	= mesh.Parent!.AddNode(new Material("material_1") { DiffuseColor = Vector4.One });

        mesh.Materials = [mesh.Materials![0], second];
        mesh.FaceIndices			= new DataBuffer<int>([0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2], 1, 1);
        mesh.MaterialIndexRanges	= [(0, 6), (6, 6)];

        Scene	loaded	= RoundTrip(source, "materials.fbx");
        Mesh[]	meshes	= loaded.GetDescendants<Mesh>();

        Assert.Equal(2, meshes.Length);
        Assert.All(meshes, static subMesh => Assert.Equal(2, subMesh.FaceCount));
        Assert.Equal(["material_0", "material_1"], meshes.Select(static subMesh => subMesh.Materials![0].Name).Order());
        Assert.All(meshes, static subMesh => Assert.NotNull(subMesh.Skin));
    }

    [Fact]
    public void FbxTranslator_Read_RenamesDuplicateSiblings()
    {
        Scene source = new("Duplicates");
        source.RootNode.AddNode(new Group("A"));
        source.RootNode.AddNode(new Group("B"));

        FbxDocument document = FbxSceneExporter.Export(new SceneTranslationSelection(source, SceneNodeFlags.None), new FbxWriteOptions(), null);

        foreach (FbxNode model in document.FirstNode("Objects")!.ChildrenNamed("Model"))
            model.Properties[1] = new FbxProperty("Same\0\u0001Model");

        Scene loaded = new("Loaded");
        FbxSceneImporter.Import(document, loaded, null);

        Assert.Equal(["Same", "Same_1"], loaded.RootNode.EnumerateChildren().Select(static node => node.Name).Order());
    }

    [Fact]
    public void FbxTranslator_Write_Filter_ReparentsSelectedNodesToNearestExportedAncestor()
    {
        Scene		scene		= new("filtered-fbx");
        MeshGroup	exportRoot	= scene.RootNode.AddNode(new MeshGroup { Name = "ExportRoot", Flags = SceneNodeFlags.Selected });
        Group		omitted		= exportRoot.AddNode(new Group("FilteredOutMid"));
        Mesh		mesh		= omitted.AddNode(new Mesh { Name = "SelectedMesh", Flags = SceneNodeFlags.Selected });

        exportRoot.BindTransform.LocalPosition	= new Vector3(5.0f, 0.0f, 0.0f);
        omitted.BindTransform.LocalPosition		= new Vector3(2.0f, 0.0f, 0.0f);
        mesh.BindTransform.LocalPosition		= new Vector3(1.0f, 0.0f, 0.0f);
        mesh.Positions							= new DataBuffer<float>([0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f], 1, 3);
        mesh.FaceIndices						= new DataBuffer<int>([0, 1, 2], 1, 1);

        Scene loaded = Read(Write(scene, "filtered.fbx", new SceneTranslatorOptions { Filter = SceneNodeFlags.Selected }), "filtered.fbx");
        Mesh loadedMesh = Assert.Single(loaded.GetDescendants<Mesh>());

        Assert.Equal("ExportRoot", loadedMesh.Parent?.Name);
        Assert.True(Vector3.Distance(loadedMesh.GetBindWorldPosition(), new Vector3(8.0f, 0.0f, 0.0f)) < 1e-4f);
    }

    [Fact]
    public void FbxTranslator_Write_Filter_ThrowsWhenMeshReferencesUnexportedMaterialOrBones()
    {
        Scene source = CreateSampleScene();
        Mesh mesh = Assert.Single(source.GetDescendants<Mesh>());

        mesh.Flags = SceneNodeFlags.Selected;
        mesh.Parent!.Flags = SceneNodeFlags.Selected;

        InvalidDataException material = Assert.Throws<InvalidDataException>(() => Write(source, "missing.fbx", new SceneTranslatorOptions { Filter = SceneNodeFlags.Selected }));
        Assert.Contains("material_0", material.Message);

        mesh.Materials![0].Flags = SceneNodeFlags.Selected;

        InvalidDataException bones = Assert.Throws<InvalidDataException>(() => Write(source, "missing.fbx", new SceneTranslatorOptions { Filter = SceneNodeFlags.Selected }));
        Assert.Contains("root", bones.Message);
    }

    [Fact]
    public void FbxBinarySerializer_RoundTrip_PreservesEveryPropertyType()
    {
        FbxDocument document = CreatePropertyDocument();

        using MemoryStream stream = new();
        FbxBinarySerializer.Write(stream, document);

        AssertPropertyDocument(FbxDocumentIO.Read(stream.ToArray()));
    }

    [Fact]
    public void FbxAsciiSerializer_RoundTrip_PreservesValuesQuotesAndSpecialNumbers()
    {
        FbxDocument document = CreatePropertyDocument();

        using MemoryStream stream = new();
        FbxAsciiSerializer.Write(stream, document);

        FbxDocument	loaded	= FbxDocumentIO.Read(stream.ToArray());
        FbxNode		node	= Assert.Single(loaded.Nodes);

        Assert.Equal(FbxFormat.Ascii, loaded.Format);
        Assert.Equal(7500, loaded.Version);
        Assert.Equal("quote \" inside", node.Properties[6].AsString());
        Assert.True(double.IsNaN(node.Properties[5].AsDouble()));
        Assert.Equal([1.5, double.PositiveInfinity, -2.0], node.FirstChild("Doubles")!.GetDoubles());
        Assert.Equal([1, -2, 3], node.FirstChild("Ints")!.GetInt32s());
    }

    [Fact]
    public void FbxAsciiSerializer_Read_HandlesLegacyMultiLineValues()
    {
        string text = "; FBX 6.1.0 project file\nObjects:  {\n\tModel: \"Model::Box\", \"Mesh\" {\n\t\tVertices: 1,2,3,\n4,5,6\n\t\t,7,8,9\n\t\tKey: \n\t\t\t0,1.5,L\n\t\tPolygonVertexIndex: 0,1,-3\n\t}\n}\n";

        FbxDocument	document	= FbxDocumentIO.Read(System.Text.Encoding.UTF8.GetBytes(text));
        FbxNode		model		= document.FirstNode("Objects")!.FirstChild("Model")!;

        Assert.Equal(6100, document.Version);
        Assert.Equal([1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0], model.FirstChild("Vertices")!.GetDoubles());
        Assert.Equal(3, model.FirstChild("Key")!.Properties.Count);
        Assert.Equal([0, 1, -3], model.FirstChild("PolygonVertexIndex")!.GetInt32s());
    }

    [Fact]
    public void FbxBinarySerializer_Read_RejectsMalformedData()
    {
        FbxDocument document = CreatePropertyDocument();

        using MemoryStream stream = new();
        FbxBinarySerializer.Write(stream, document);

        byte[] data = stream.ToArray();

        Assert.Throws<InvalidDataException>(() => FbxDocumentIO.Read(data[..(data.Length / 2)]));

        data[27] = 0xFF;
        data[28] = 0xFF;

        Assert.Throws<InvalidDataException>(() => FbxDocumentIO.Read(data));
    }

    [Fact]
    public void FbxBinarySerializer_Write_DoesNotRequireSeekableStream()
    {
        using MemoryStream			buffer		= new();
        using NonSeekableStream		stream		= new(buffer);

        FbxBinarySerializer.Write(stream, CreatePropertyDocument());

        AssertPropertyDocument(FbxDocumentIO.Read(buffer.ToArray()));
    }

    [Fact]
    public void FbxTransform_EulerConversion_RoundTripsEveryRotationOrder()
    {
        Vector3 angles = new(30.0f, -50.0f, 120.0f);

        foreach (FbxRotationOrder order in Enum.GetValues<FbxRotationOrder>())
        {
            Quaternion rotation = FbxTransform.EulerToQuaternion(angles, order);
            Assert.True(MathF.Abs(Quaternion.Dot(rotation, Quaternion.Normalize(rotation))) > 0.9999f);
        }

        Quaternion expected	= FbxTransform.EulerToQuaternion(angles, FbxRotationOrder.EulerXYZ);
        Quaternion actual	= FbxTransform.EulerToQuaternion(FbxTransform.QuaternionToEulerXyz(expected), FbxRotationOrder.EulerXYZ);

        Assert.True(MathF.Abs(Quaternion.Dot(expected, actual)) > 0.99999f);
        Vector3 unwrapped = FbxTransform.QuaternionToEulerXyz(FbxTransform.EulerToQuaternion(new Vector3(0.0f, 0.0f, 190.0f), FbxRotationOrder.EulerXYZ), new Vector3(0.0f, 0.0f, 180.0f));
        Assert.True(Vector3.Distance(new Vector3(0.0f, 0.0f, 190.0f), unwrapped) < 1e-3f);
    }

    [Fact]
    public void MeshTriangulator_TriangulatePolygon_HandlesConcavePolygons()
    {
        Vector3[]	polygon		= [new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(2, 1, 0), new(0, 4, 0)];
        int[]		triangles	= new int[(polygon.Length - 2) * 3];
        int			written		= MeshTriangulator.TriangulatePolygon(polygon, triangles);
        float		area		= 0.0f;

        Assert.Equal(9, written);

        for (int i = 0; i < written; i += 3)
        {
            float signedArea = Vector3.Cross(polygon[triangles[i + 1]] - polygon[triangles[i]], polygon[triangles[i + 2]] - polygon[triangles[i]]).Z * 0.5f;

            Assert.True(signedArea > 0.0f);
            area += signedArea;
        }

        Assert.Equal(10.0f, area, 3);
    }

    private static Scene RoundTrip(Scene scene, string fileName)
    {
        return Read(Write(scene, fileName, new SceneTranslatorOptions()), fileName);
    }

    private static SceneTranslatorManager CreateManager()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new FbxTranslator());
        return manager;
    }

    private static byte[] Write(Scene scene, string fileName, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new();
        CreateManager().Write(stream, fileName, scene, options, token: null);
        return stream.ToArray();
    }

    private static Scene Read(byte[] data, string fileName)
    {
        using MemoryStream stream = new(data, writable: false);
        return CreateManager().Read(stream, fileName, new SceneTranslatorOptions(), token: null);
    }

    private static void AssertSkinnedPositionsEqual(Mesh expected, Mesh actual)
    {
        HashSet<Vector3> expectedPositions = [];

        for (int i = 0; i < expected.VertexCount; i++)
            expectedPositions.Add(Round(GetWorldPosition(expected, i)));

        for (int i = 0; i < actual.VertexCount; i++)
            Assert.Contains(Round(GetWorldPosition(actual, i)), expectedPositions);
    }

    private static Vector3 GetWorldPosition(Mesh mesh, int vertex)
    {
        Vector3 position = mesh.GetVertexPosition(vertex);
        return mesh.Skin is null ? Vector3.Transform(position, mesh.GetActiveWorldMatrix()) : position;
    }

    private static Vector3 Round(Vector3 value)
    {
        return new Vector3(MathF.Round(value.X, 3), MathF.Round(value.Y, 3), MathF.Round(value.Z, 3));
    }

    private static void AssertMatrixEqual(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
                Assert.Equal(expected[row, column], actual[row, column], 3);
        }
    }

    private static FbxDocument CreatePropertyDocument()
    {
        FbxDocument	document	= new() { Version = 7500 };
        FbxNode		node		= new("Values");

        node.Properties.Add(new FbxProperty((short)-7));
        node.Properties.Add(FbxProperty.CreateBoolean((byte)'Y'));
        node.Properties.Add(new FbxProperty(123456));
        node.Properties.Add(new FbxProperty(9876543210L));
        node.Properties.Add(new FbxProperty(1.25f));
        node.Properties.Add(new FbxProperty(double.NaN));
        node.Properties.Add(new FbxProperty("quote \" inside"));
        node.Properties.Add(new FbxProperty(new byte[] { 1, 2, 3, 255 }));
        node.AddChild("Doubles", new FbxProperty(new[] { 1.5, double.PositiveInfinity, -2.0 }));
        node.AddChild("Ints", new FbxProperty(new[] { 1, -2, 3 }));
        node.AddChild("Large", new FbxProperty(Enumerable.Range(0, 1000).Select(static value => (float)value).ToArray()));
        node.AddChild("Empty");
        document.Nodes.Add(node);

        return document;
    }

    private static void AssertPropertyDocument(FbxDocument document)
    {
        FbxNode node = Assert.Single(document.Nodes);

        Assert.Equal(7500, document.Version);
        Assert.Equal(FbxPropertyType.Int16, node.Properties[0].Type);
        Assert.Equal(-7, node.Properties[0].AsInt32());
        Assert.Equal('Y', (char)node.Properties[1].AsInt64());
        Assert.Equal(123456, node.Properties[2].AsInt32());
        Assert.Equal(9876543210L, node.Properties[3].AsInt64());
        Assert.Equal(1.25f, node.Properties[4].AsSingle());
        Assert.True(double.IsNaN(node.Properties[5].AsDouble()));
        Assert.Equal("quote \" inside", node.Properties[6].AsString());
        Assert.Equal(new byte[] { 1, 2, 3, 255 }, node.Properties[7].AsBytes());
        Assert.Equal(1000, node.FirstChild("Large")!.GetSingles().Length);
        Assert.Equal(999.0f, node.FirstChild("Large")!.GetSingles()[^1]);
        Assert.NotNull(node.FirstChild("Empty"));
    }

    private static Scene CreateSampleScene()
    {
        Scene			scene		= new("FbxSample");
        SkeletonBone	rootBone	= scene.RootNode.AddNode(new SkeletonBone("root"));
        SkeletonBone	childBone	= rootBone.AddNode(new SkeletonBone("child"));
        MeshGroup		model		= scene.RootNode.AddNode(new MeshGroup { Name = "ModelRoot" });
        Mesh			mesh		= model.AddNode(new Mesh { Name = "mesh_0" });
        Material		material	= model.AddNode(new Material("material_0") { DiffuseColor = new Vector4(0.7f, 0.6f, 0.5f, 1.0f) });

        childBone.BindTransform.LocalPosition	= new Vector3(0.0f, 1.0f, 0.0f);
        mesh.Positions							= new DataBuffer<float>([0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f], 1, 3);
        mesh.Normals							= new DataBuffer<float>([0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f], 1, 3);
        mesh.UVLayers							= new DataBuffer<float>([0.0f, 0.0f, 0.05f, 0.05f, 1.0f, 0.0f, 0.95f, 0.05f, 1.0f, 1.0f, 0.95f, 0.95f, 0.0f, 1.0f, 0.05f, 0.95f], 2, 2);
        mesh.FaceIndices						= new DataBuffer<int>([0, 1, 2, 0, 2, 3], 1, 1);
        mesh.Skin								= new Skin([rootBone, childBone], new DataBuffer<int>([0, 1, 0, 1, 0, 1, 0, 1], 2, 1), new DataBuffer<float>([0.75f, 0.25f, 0.6f, 0.4f, 0.35f, 0.65f, 0.8f, 0.2f], 2, 1));
        mesh.Materials							= [material];

        return scene;
    }
}
