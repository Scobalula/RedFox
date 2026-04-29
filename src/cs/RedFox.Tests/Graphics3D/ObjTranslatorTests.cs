// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.WavefrontObj;

namespace RedFox.Tests.Graphics3D;

public sealed class ObjTranslatorTests
{
    [Fact]
    public void ObjTranslator_RoundTrip_PreservesPositionsNormalsUVs()
    {
        Scene sourceScene = CreateTriangleScene();
        SceneTranslatorManager manager = CreateManagerWithObjTranslator();

        byte[] objData = WriteSceneToObj(manager, sourceScene);
        Scene loadedScene = ReadSceneFromObj(manager, objData);

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
    public void ObjTranslator_RoundTrip_PreservesMultipleMeshes()
    {
        Scene sourceScene = CreateMultiMeshScene();
        SceneTranslatorManager manager = CreateManagerWithObjTranslator();

        byte[] objData = WriteSceneToObj(manager, sourceScene);
        Scene loadedScene = ReadSceneFromObj(manager, objData);

        Mesh[] loadedMeshes = loadedScene.GetDescendants<Mesh>();
        Assert.Equal(2, loadedMeshes.Length);
        Assert.Equal("BoxA", loadedMeshes[0].Name);
        Assert.Equal("BoxB", loadedMeshes[1].Name);
    }

    [Fact]
    public void ObjTranslator_RoundTrip_PreservesMaterialNames()
    {
        Scene sourceScene = CreateSceneWithMaterials();
        SceneTranslatorManager manager = CreateManagerWithObjTranslator();

        byte[] objData = WriteSceneToObj(manager, sourceScene);
        Scene loadedScene = ReadSceneFromObj(manager, objData);

        Mesh[] loadedMeshes = loadedScene.GetDescendants<Mesh>();
        Assert.Single(loadedMeshes);
        Assert.NotNull(loadedMeshes[0].Materials);
        Assert.Single(loadedMeshes[0].Materials!);
        Assert.Equal("TestMaterial", loadedMeshes[0].Materials![0].Name);
    }

    [Fact]
    public void ObjTranslator_ManagerRead_ResolvesMtlAndTexturePathsRelativeToObj()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"RedFoxObj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string objPath = Path.Combine(tempDirectory, "sample.obj");
            string mtlPath = Path.Combine(tempDirectory, "sample.mtl");
            File.WriteAllText(objPath, """
                mtllib sample.mtl
                v 0 0 0
                v 1 0 0
                v 0 1 0
                usemtl TestMat
                f 1 2 3
                """, Encoding.UTF8);
            File.WriteAllText(mtlPath, """
                newmtl TestMat
                map_Kd textures/diffuse.tga
                """, Encoding.UTF8);

            SceneTranslatorManager manager = CreateManagerWithObjTranslator();
            Scene loadedScene = manager.Read(objPath, new SceneTranslatorOptions(), token: null);

            Mesh mesh = loadedScene.GetDescendants<Mesh>().Single();
            Assert.NotNull(mesh.Materials);
            Assert.Single(mesh.Materials!);
            Assert.Equal("TestMat", mesh.Materials![0].Name);

            Material material = mesh.Materials[0];
            Assert.True(material.TryGetDiffuseMap(out Texture? diffuseTexture));
            Assert.NotNull(diffuseTexture);
            Assert.Equal("textures/diffuse.tga", diffuseTexture!.FilePath);
            Assert.Equal(Path.GetFullPath(Path.Combine(tempDirectory, "textures/diffuse.tga")), diffuseTexture.ResolvedFilePath);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ObjTranslator_RoundTrip_DeterministicRewriteAndStructuralMatch()
    {
        Scene sourceScene = CreateTriangleScene();
        SceneTranslatorManager manager = CreateManagerWithObjTranslator();

        byte[] firstWrite = WriteSceneToObj(manager, sourceScene);
        Scene firstLoad = ReadSceneFromObj(manager, firstWrite);
        byte[] secondWrite = WriteSceneToObj(manager, firstLoad);
        Scene secondLoad = ReadSceneFromObj(manager, secondWrite);

        AssertSceneStructureEquivalent(firstLoad, secondLoad);
    }

    [Fact]
    public void ObjTranslator_PositionsOnly_RoundTrips()
    {
        Scene scene = new("PositionsOnlyScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });
        Mesh mesh = model.AddNode(new Mesh { Name = "pos_only" });
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        byte[] objData = WriteSceneToObj(manager, scene);
        Scene loaded = ReadSceneFromObj(manager, objData);

        Mesh loadedMesh = loaded.GetDescendants<Mesh>().Single();
        Assert.Equal(3, loadedMesh.VertexCount);
        Assert.Equal(1, loadedMesh.FaceCount);
        Assert.Null(loadedMesh.Normals);
        Assert.Null(loadedMesh.UVLayers);
    }

    [Fact]
    public void ObjTranslator_QuadFace_IsTriangulated()
    {
        string objText = """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            f 1 2 3 4
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText));

        Mesh mesh = loaded.GetDescendants<Mesh>().Single();
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(2, mesh.FaceCount);
    }

    [Fact]
    public void ObjTranslator_NegativeIndices_AreResolved()
    {
        string objText = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f -3 -2 -1
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText));

        Mesh mesh = loaded.GetDescendants<Mesh>().Single();
        Assert.Equal(3, mesh.VertexCount);

        AssertVector3Equal(new Vector3(0f, 0f, 0f), mesh.Positions!.GetVector3(0, 0), 1e-5f);
        AssertVector3Equal(new Vector3(1f, 0f, 0f), mesh.Positions!.GetVector3(1, 0), 1e-5f);
        AssertVector3Equal(new Vector3(0f, 1f, 0f), mesh.Positions!.GetVector3(2, 0), 1e-5f);
    }

    [Fact]
    public void ObjTranslator_VertexNormalOnly_ParsesCorrectly()
    {
        string objText = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vn 0 0 1
            vn 0 0 1
            vn 0 0 1
            f 1//1 2//2 3//3
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText));

        Mesh mesh = loaded.GetDescendants<Mesh>().Single();
        Assert.NotNull(mesh.Normals);
        Assert.Null(mesh.UVLayers);

        for (int i = 0; i < 3; i++)
        {
            AssertVector3Equal(new Vector3(0f, 0f, 1f), mesh.Normals!.GetVector3(i, 0), 1e-5f);
        }
    }

    [Fact]
    public void ObjTranslator_MultipleGroupsCreateSeparateMeshes()
    {
        string objText = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            v 2 0 0
            v 3 0 0
            v 2 1 0
            g GroupA
            f 1 2 3
            g GroupB
            f 4 5 6
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText));

        Mesh[] meshes = loaded.GetDescendants<Mesh>();
        Assert.Equal(2, meshes.Length);
        Assert.Equal("GroupA", meshes[0].Name);
        Assert.Equal("GroupB", meshes[1].Name);
    }

    [Fact]
    public void ObjTranslator_UsemtlSplitsMeshes()
    {
        string objText = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            v 2 0 0
            v 3 0 0
            v 2 1 0
            usemtl MatA
            f 1 2 3
            usemtl MatB
            f 4 5 6
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText));

        Mesh[] meshes = loaded.GetDescendants<Mesh>();
        Assert.Equal(2, meshes.Length);
        Assert.Equal("MatA", meshes[0].Materials![0].Name);
        Assert.Equal("MatB", meshes[1].Materials![0].Name);
    }

    [Fact]
    public void ObjTranslator_MergeStaticMeshesOption_CollapsesGroupsByMaterial()
    {
        string objText = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            v 2 0 0
            v 3 0 0
            v 2 1 0
            v 4 0 0
            v 5 0 0
            v 4 1 0
            usemtl MatA
            g GroupA
            f 1 2 3
            g GroupB
            f 4 5 6
            usemtl MatB
            g GroupC
            f 7 8 9
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        SceneTranslatorOptions options = new();
        options.Set(ObjTranslator.MergeStaticMeshesOption, true);
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText), options);

        Mesh[] meshes = loaded.GetDescendants<Mesh>();
        Assert.Equal(2, meshes.Length);

        Mesh matAMesh = meshes.Single(mesh => mesh.Materials![0].Name == "MatA");
        Mesh matBMesh = meshes.Single(mesh => mesh.Materials![0].Name == "MatB");
        Assert.Equal(6, matAMesh.VertexCount);
        Assert.Equal(2, matAMesh.FaceCount);
        Assert.Equal(3, matBMesh.VertexCount);
        Assert.Equal(1, matBMesh.FaceCount);
    }

    [Fact]
    public void MtlReader_ParsesMaterialTextures()
    {
        string mtlText = """
            newmtl TestMat
            map_Kd textures/diffuse.tga
            map_Ks textures/specular.tga
            map_Bump textures/normal.tga
            map_Ke textures/emissive.tga
            """;

        Dictionary<string, Material> materials = new(StringComparer.Ordinal);
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(mtlText));
        ObjMtlReader.Read(stream, materials);

        Assert.Single(materials);
        Assert.True(materials.ContainsKey("TestMat"));
        Material mat = materials["TestMat"];
        Assert.Equal("diffuse", mat.DiffuseMapName);
        Assert.Equal("specular", mat.SpecularMapName);
        Assert.Equal("normal", mat.NormalMapName);
        Assert.Equal("emissive", mat.EmissiveMapName);

        Texture[] textures = mat.GetDescendants<Texture>();
        Assert.Equal(4, textures.Length);
    }

    [Fact]
    public void MtlWriter_WritesExpectedContent()
    {
        Material mat = new("TestMat");
        Texture diffTex = mat.AddNode(new Texture("textures/diffuse_tex.tga"));
        mat.DiffuseMapName = "diffuse";
        mat.Connect("diffuse", diffTex);
        Texture normTex = mat.AddNode(new Texture("textures/normal_tex.tga"));
        mat.NormalMapName = "normal";
        mat.Connect("normal", normTex);

        using MemoryStream stream = new();
        ObjMtlWriter.Write(stream, [mat]);

        string content = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("newmtl TestMat", content);
        Assert.Contains("map_Kd textures/diffuse_tex.tga", content);
        Assert.Contains("map_Bump textures/normal_tex.tga", content);
    }

    [Fact]
    public void ObjTranslator_EmptyScene_ProducesValidOutput()
    {
        Scene scene = new("EmptyScene");
        SceneTranslatorManager manager = CreateManagerWithObjTranslator();

        byte[] objData = WriteSceneToObj(manager, scene);
        string content = Encoding.UTF8.GetString(objData);

        Assert.Contains("# Wavefront OBJ exported by RedFox", content);
    }

    [Fact]
    public void ObjTranslator_CommentsAndBlankLinesAreIgnored()
    {
        string objText = """
            # This is a comment
            
            v 0 0 0
            # Another comment
            v 1 0 0
            v 0 1 0
            
            f 1 2 3
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText));

        Mesh mesh = loaded.GetDescendants<Mesh>().Single();
        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(1, mesh.FaceCount);
    }

    [Fact]
    public void ObjTranslator_SharedVertexDeduplicated()
    {
        // Two triangles sharing a vertex: positions match but use same global index
        string objText = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            v 1 1 0
            f 1 2 3
            f 2 4 3
            """;

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        Scene loaded = ReadSceneFromObj(manager, Encoding.UTF8.GetBytes(objText));

        Mesh mesh = loaded.GetDescendants<Mesh>().Single();
        // Shared vertices with same pos/tex/normal tuple should be deduplicated
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(2, mesh.FaceCount);
    }

    [Fact]
    public void MtlReader_BumpMapWithOptions_ParsesFilename()
    {
        string mtlText = """
            newmtl BumpMat
            bump -bm 1.0 textures/bump_map.tga
            """;

        Dictionary<string, Material> materials = new(StringComparer.Ordinal);
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(mtlText));
        ObjMtlReader.Read(stream, materials);

        Material mat = materials["BumpMat"];
        Assert.Equal("bump_map", mat.NormalMapName);
    }

    [Fact]
    public void ObjTranslator_LargeIndexBuffer_UsesIntIndices()
    {
        // Create a mesh with more than ushort.MaxValue vertices to force int indices
        Scene scene = new("LargeScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "LargeModel" });
        Mesh mesh = model.AddNode(new Mesh { Name = "large_mesh" });

        int vertexCount = ushort.MaxValue + 10;
        float[] posData = new float[vertexCount * 3];
        for (int i = 0; i < vertexCount; i++)
        {
            posData[i * 3] = i * 0.001f;
            posData[i * 3 + 1] = 0f;
            posData[i * 3 + 2] = 0f;
        }

        mesh.Positions = new DataBuffer<float>(posData, 1, 3);

        // Just 1 face referencing first 3 verts
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        byte[] objData = WriteSceneToObj(manager, scene);
        Scene loaded = ReadSceneFromObj(manager, objData);

        Mesh loadedMesh = loaded.GetDescendants<Mesh>().Single();
        Assert.Equal(3, loadedMesh.VertexCount);
        Assert.Equal(1, loadedMesh.FaceCount);
    }

    [Fact]
    public void ObjTranslator_Write_Filter_ExportsSelectedMeshAndMaterial()
    {
        Scene scene = new("FilteredObjScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "Model" });

        Material selectedMaterial = model.AddNode(new Material("SelectedMaterial") { Flags = SceneNodeFlags.Selected });
        Mesh selectedMesh = model.AddNode(new Mesh { Name = "SelectedMesh", Flags = SceneNodeFlags.Selected });
        selectedMesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        selectedMesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        selectedMesh.Materials = [selectedMaterial];

        Material ignoredMaterial = model.AddNode(new Material("IgnoredMaterial"));
        Mesh ignoredMesh = model.AddNode(new Mesh { Name = "IgnoredMesh" });
        ignoredMesh.Positions = new DataBuffer<float>([0f, 0f, 1f, 1f, 0f, 1f, 0f, 1f, 1f], 1, 3);
        ignoredMesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        ignoredMesh.Materials = [ignoredMaterial];

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        byte[] objData = WriteSceneToObj(manager, scene, new SceneTranslatorOptions { Filter = SceneNodeFlags.Selected });
        Scene loaded = ReadSceneFromObj(manager, objData);

        Mesh loadedMesh = loaded.GetDescendants<Mesh>().Single();
        Assert.Equal("SelectedMesh", loadedMesh.Name);
        Assert.NotNull(loadedMesh.Materials);
        Assert.Single(loadedMesh.Materials!);
        Assert.Equal("SelectedMaterial", loadedMesh.Materials[0].Name);
    }

    [Fact]
    public void ObjTranslator_Write_Filter_ThrowsWhenSelectedMeshReferencesFilteredMaterial()
    {
        Scene scene = new("FilteredObjMaterialScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "Model" });
        Material material = model.AddNode(new Material("FilteredMaterial"));
        Mesh mesh = model.AddNode(new Mesh { Name = "SelectedMesh", Flags = SceneNodeFlags.Selected });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        mesh.Materials = [material];

        SceneTranslatorManager manager = CreateManagerWithObjTranslator();
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            WriteSceneToObj(manager, scene, new SceneTranslatorOptions { Filter = SceneNodeFlags.Selected }));

        Assert.Contains(mesh.Name, ex.Message);
        Assert.Contains(material.Name, ex.Message);
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
        meshA.Normals = new DataBuffer<float>(
        [
            0f, 0f, 1f,
            0f, 0f, 1f,
            0f, 0f, 1f,
        ], 1, 3);
        meshA.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        Mesh meshB = model.AddNode(new Mesh { Name = "BoxB" });
        meshB.Positions = new DataBuffer<float>(
        [
            2f, 0f, 0f,
            3f, 0f, 0f,
            2f, 1f, 0f,
        ], 1, 3);
        meshB.Normals = new DataBuffer<float>(
        [
            0f, 0f, -1f,
            0f, 0f, -1f,
            0f, 0f, -1f,
        ], 1, 3);
        meshB.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        return scene;
    }

    private static Scene CreateSceneWithMaterials()
    {
        Scene scene = new("MaterialScene");
        Model model = scene.RootNode.AddNode(new Model { Name = "TestModel" });

        Mesh mesh = model.AddNode(new Mesh { Name = "textured_mesh" });
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        Material material = model.AddNode(new Material("TestMaterial"));
        Texture tex = material.AddNode(new Texture("textures/diffuse_a.tga"));
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", tex);

        mesh.Materials = [material];
        return scene;
    }

    private static void AssertSceneStructureEquivalent(Scene expectedScene, Scene actualScene)
    {
        Mesh[] expectedMeshes = expectedScene.GetDescendants<Mesh>();
        Mesh[] actualMeshes = actualScene.GetDescendants<Mesh>();
        Assert.Equal(expectedMeshes.Length, actualMeshes.Length);

        for (int i = 0; i < expectedMeshes.Length; i++)
        {
            Mesh expected = expectedMeshes[i];
            Mesh actual = actualMeshes[i];
            Assert.Equal(expected.VertexCount, actual.VertexCount);
            Assert.Equal(expected.FaceCount, actual.FaceCount);
            Assert.Equal(expected.Name, actual.Name);

            for (int v = 0; v < expected.VertexCount; v++)
            {
                AssertVector3Equal(
                    expected.Positions!.GetVector3(v, 0),
                    actual.Positions!.GetVector3(v, 0),
                    1e-5f);
            }
        }

        Material[] expectedMats = expectedScene.GetDescendants<Material>();
        Material[] actualMats = actualScene.GetDescendants<Material>();
        Assert.Equal(expectedMats.Length, actualMats.Length);
    }

    private static SceneTranslatorManager CreateManagerWithObjTranslator()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new ObjTranslator());
        return manager;
    }

    private static byte[] WriteSceneToObj(SceneTranslatorManager manager, Scene scene)
    {
        return WriteSceneToObj(manager, scene, new SceneTranslatorOptions());
    }

    private static byte[] WriteSceneToObj(SceneTranslatorManager manager, Scene scene, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new();
        manager.Write(stream, "test.obj", scene, options, token: null);
        return stream.ToArray();
    }

    private static Scene ReadSceneFromObj(SceneTranslatorManager manager, byte[] data)
    {
        return ReadSceneFromObj(manager, data, new SceneTranslatorOptions());
    }

    private static Scene ReadSceneFromObj(SceneTranslatorManager manager, byte[] data, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, "test.obj", options, token: null);
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
