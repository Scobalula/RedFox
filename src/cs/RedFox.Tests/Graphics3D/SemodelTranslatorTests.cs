using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Formats.SEModel;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class SemodelTranslatorTests
{
    [Fact]
    public void SemodelTranslator_RoundTripViaManager_PreservesSceneStructure()
    {
        Scene sourceScene				= CreateSemodelSampleScene();
        SceneTranslatorManager manager	= CreateManagerWithSemodelTranslator();
        byte[] firstWrite				= WriteSceneWithManager(manager, sourceScene, "sample.semodel");
        Scene firstLoad					= ReadSceneWithManager(manager, firstWrite, "sample.semodel");
        byte[] secondWrite				= WriteSceneWithManager(manager, firstLoad, "sample.semodel");
        Scene secondLoad				= ReadSceneWithManager(manager, secondWrite, "sample.semodel");

        AssertSceneStructureEquivalent(firstLoad, secondLoad);
    }

    [Fact]
    public void Mesh_GetVertexAccessors_ApplyActiveSkinningUnlessRawRequested()
    {
        Scene scene	= CreatePosedSkinningScene();
        Mesh mesh	= scene.GetDescendants<Mesh>().Single();

        Vector3 rawPosition		= mesh.GetVertexPosition(0, raw: true);
        Vector3 posedPosition	= mesh.GetVertexPosition(0);
        Vector3 rawNormal		= mesh.GetVertexNormal(0, raw: true);
        Vector3 posedNormal		= mesh.GetVertexNormal(0);

        AssertVector3Equal(new Vector3(1f, 0f, 0f), rawPosition, 1e-5f);
        AssertVector3Equal(new Vector3(2f, 0f, 0f), posedPosition, 1e-5f);
        AssertVector3Equal(Vector3.UnitX, rawNormal, 1e-5f);
        AssertVector3Equal(Vector3.UnitY, posedNormal, 1e-5f);
    }

    [Fact]
    public void SemodelTranslator_Write_HonorsWriteRawVerticesForSkinnedMeshes()
    {
        Scene sourceScene				= CreatePosedSkinningScene();
        SceneTranslatorManager manager	= CreateManagerWithSemodelTranslator();
        Mesh sourceMesh					= sourceScene.GetDescendants<Mesh>().Single();

        Scene posedScene	= ReadSceneWithManager(manager, WriteSceneWithManager(manager, sourceScene, "sample.semodel", CreateDefaultOptions()), "sample.semodel");
        Scene rawScene		= ReadSceneWithManager(manager, WriteSceneWithManager(manager, sourceScene, "sample.semodel", CreateRawVertexOptions()), "sample.semodel");

        Mesh posedMesh	= posedScene.GetDescendants<Mesh>().Single();
        Mesh rawMesh	= rawScene.GetDescendants<Mesh>().Single();

        AssertVector3Equal(sourceMesh.GetVertexPosition(0), posedMesh.Positions!.GetVector3(0, 0), 1e-5f);
        AssertVector3Equal(sourceMesh.GetVertexNormal(0), posedMesh.Normals!.GetVector3(0, 0), 1e-5f);
        AssertVector3Equal(sourceMesh.GetVertexPosition(0, raw: true), rawMesh.Positions!.GetVector3(0, 0), 1e-5f);
        AssertVector3Equal(sourceMesh.GetVertexNormal(0, raw: true), rawMesh.Normals!.GetVector3(0, 0), 1e-5f);
    }

    [CorpusFact]
    public void SemodelSamples_RoundTripViaManager_DeterministicRewriteAndStructuralMatch()
    {
        string semodelDirectory = GetRequiredSemodelDirectory();
        if (string.IsNullOrWhiteSpace(semodelDirectory))
        {
            return;
        }

        string[] semodelFiles = Directory.GetFiles(semodelDirectory, "*.semodel", SearchOption.AllDirectories);
        if (semodelFiles.Length == 0)
        {
            return;
        }

        SceneTranslatorManager manager = CreateManagerWithSemodelTranslator();
        foreach (string semodelFile in semodelFiles)
        {
            using FileStream inputFileStream = File.OpenRead(semodelFile);
            Scene sourceScene	= manager.Read(inputFileStream, semodelFile, CreateDefaultOptions(), token: null);
            byte[] firstWrite	= WriteSceneWithManager(manager, sourceScene, semodelFile);
            Scene firstLoad		= ReadSceneWithManager(manager, firstWrite, semodelFile);
            byte[] secondWrite	= WriteSceneWithManager(manager, firstLoad, semodelFile);
            Scene secondLoad	= ReadSceneWithManager(manager, secondWrite, semodelFile);

            AssertSceneStructureEquivalent(firstLoad, secondLoad);
        }
    }

    [Fact]
    public void Mesh_GetVertexPosition_RespondsToSkeletonRootMovement()
    {
        Scene scene			= CreatePosedSkinningScene();
        Skeleton skeleton	= scene.GetDescendants<Skeleton>().Single();
        Mesh mesh			= scene.GetDescendants<Mesh>().Single();

        skeleton.LiveTransform.LocalPosition = new Vector3(5f, 0f, 0f);

        AssertVector3Equal(new Vector3(7f, 0f, 0f), mesh.GetVertexPosition(0), 1e-5f);
    }

    [Fact]
    public void Mesh_BakeCurrentSkinningToVertices_RebasesToCurrentPoseAndClearsSkinning()
    {
        Scene scene					= CreatePosedSkinningScene();
        Mesh mesh					= scene.GetDescendants<Mesh>().Single();
        SkeletonBone bone			= scene.GetDescendants<SkeletonBone>().Single();
        Vector3 posedBeforeBake		= mesh.GetVertexPosition(0);
        Vector3 normalBeforeBake	= mesh.GetVertexNormal(0);

        mesh.BakeCurrentPoseToVertices();

        Assert.Null(mesh.Skin?.BoneIndices);
        Assert.Null(mesh.Skin?.BoneWeights);
        Assert.Null(mesh.Skin?.Bones);
        Assert.Null(mesh.Skin?.InverseBindMatrices);
        AssertVector3Equal(posedBeforeBake, mesh.GetVertexPosition(0, raw: true), 1e-5f);
        AssertVector3Equal(posedBeforeBake, mesh.GetVertexPosition(0), 1e-5f);
        AssertVector3Equal(normalBeforeBake, mesh.GetVertexNormal(0, raw: true), 1e-5f);

        bone.LiveTransform.LocalPosition = new Vector3(9f, 0f, 0f);

        AssertVector3Equal(posedBeforeBake, mesh.GetVertexPosition(0), 1e-5f);
    }

    [Fact]
    public void SemodelTranslator_Read_ResolvesRelativeTexturePathsAgainstSourceFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"RedFoxSemodel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string filePath					= Path.Combine(tempDirectory, "textured.semodel");
            Scene scene						= CreateSemodelSampleScene();
            SceneTranslatorManager manager	= CreateManagerWithSemodelTranslator();

            manager.Write(filePath, scene, CreateDefaultOptions(), token: null);

            Scene loadedScene	= manager.Read(filePath, CreateDefaultOptions(), token: null);
            Material material	= loadedScene.GetDescendants<Material>().Single();

            Assert.True(material.TryGetDiffuseMap(out Texture? diffuseTexture));
            Assert.NotNull(diffuseTexture);
            Assert.Equal("textures/diffuse_a.dds", diffuseTexture!.FilePath);
            Assert.Equal(Path.GetFullPath(Path.Combine(tempDirectory, "textures\\diffuse_a.dds")), diffuseTexture.ResolvedFilePath);
            Assert.Equal("diffuse", material.DiffuseMapName);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SemodelTranslator_Write_RelativizesResolvedTexturePathsAgainstOutputFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"RedFoxSemodelWrite_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string texturesDirectory = Path.Combine(tempDirectory, "textures");
            Directory.CreateDirectory(texturesDirectory);

            string outputPath			= Path.Combine(tempDirectory, "portable.semodel");
            string absoluteTexturePath	= Path.Combine(texturesDirectory, "diffuse_a.dds");

            Scene scene			= CreateSemodelSampleScene();
            Material material	= scene.GetDescendants<Material>().Single();
            Texture texture		= material.GetDiffuseMap();
            texture.FilePath			= absoluteTexturePath;
            texture.ResolvedFilePath	= absoluteTexturePath;

            SceneTranslatorManager manager = CreateManagerWithSemodelTranslator();
            manager.Write(outputPath, scene, CreateDefaultOptions(), token: null);

            Scene loadedScene		= manager.Read(outputPath, CreateDefaultOptions(), token: null);
            Material loadedMaterial	= loadedScene.GetDescendants<Material>().Single();

            Assert.True(loadedMaterial.TryGetDiffuseMap(out Texture? loadedTexture));
            Assert.NotNull(loadedTexture);
            Assert.Equal("textures/diffuse_a.dds", loadedTexture!.FilePath);
            Assert.Equal(absoluteTexturePath, loadedTexture.ResolvedFilePath);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SemodelTranslator_Write_Filter_ExportsSelectedNodesAndRemapsBoneParents()
    {
        Scene scene = new("FilteredSemodelScene");

        Skeleton skeleton		= scene.RootNode.AddNode(new Skeleton("Armature"));
        SkeletonBone rootBone	= skeleton.AddNode(new SkeletonBone("root"));
        SkeletonBone midBone	= rootBone.AddNode(new SkeletonBone("mid") { Flags = SceneNodeFlags.Selected });
        SkeletonBone tipBone	= midBone.AddNode(new SkeletonBone("tip") { Flags = SceneNodeFlags.Selected });

        MeshGroup model	= scene.RootNode.AddNode(new MeshGroup { Name = "ModelRoot" });
        Mesh mesh		= model.AddNode(new Mesh
        {
            Name	= "mesh_0",
            Flags	= SceneNodeFlags.Selected,
        });
        mesh.Positions	= new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.Normals	= new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f], 1, 3);
        mesh.UVLayers	= new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f], 1, 2);
        DataBuffer indices	= new DataBuffer<byte>([0, 1, 0, 1, 0, 1], 2, 1);
        DataBuffer weights	= new DataBuffer<float>([0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f], 2, 1);
        mesh.Skin			= new Skin([midBone, tipBone], indices, weights);
        mesh.FaceIndices	= new DataBuffer<ushort>(new ushort[] { 0, 1, 2 }, 1, 1);

        Material selectedMaterial = model.AddNode(new Material("material_selected") { Flags = SceneNodeFlags.Selected });
        selectedMaterial.Connect("diffuse", selectedMaterial.AddNode(new Texture("textures\\selected.dds") { Name = "diffuse" }));
        mesh.Materials = [selectedMaterial];

        SceneTranslatorManager manager	= CreateManagerWithSemodelTranslator();
        SceneTranslatorOptions options	= new()
        {
            Filter = SceneNodeFlags.Selected,
        };

        Scene loaded = ReadSceneWithManager(manager, WriteSceneWithManager(manager, scene, "filtered.semodel", options), "filtered.semodel");

        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();
        Assert.Equal(2, loadedBones.Length);
        Assert.Equal("mid", loadedBones[0].Name);
        Assert.Equal("tip", loadedBones[1].Name);
        Assert.False(loadedBones[0].Parent is SkeletonBone);
        Assert.Same(loadedBones[0], loadedBones[1].Parent);

        Mesh loadedMesh = loaded.GetDescendants<Mesh>().Single();
        Assert.NotNull(loadedMesh.Materials);
        Assert.Single(loadedMesh.Materials!);
        Assert.Equal("material_selected", loadedMesh.Materials[0].Name);
    }

    [Fact]
    public void SemodelTranslator_Write_Filter_ThrowsWhenSelectedMeshReferencesFilteredMaterial()
    {
        Scene scene			= CreateSemodelSampleScene();
        Mesh mesh			= scene.GetDescendants<Mesh>().Single();
        Material material	= scene.GetDescendants<Material>().Single();
        foreach (SkeletonBone bone in scene.GetDescendants<SkeletonBone>())
            bone.Flags = SceneNodeFlags.Selected;
        Material fallbackMaterial = material.Parent!.AddNode(new Material("material_fallback")
        {
            Flags			= SceneNodeFlags.Selected,
            DiffuseMapName	= "diffuse_fallback",
        });
        fallbackMaterial.Connect("diffuse", fallbackMaterial.AddNode(new Texture("textures\\fallback.dds") { Name = "diffuse" }));

        mesh.Flags		= SceneNodeFlags.Selected;
        mesh.Materials	= [material, fallbackMaterial];
        material.Flags	= SceneNodeFlags.None;

        SceneTranslatorManager manager	= CreateManagerWithSemodelTranslator();
        SceneTranslatorOptions options	= new()
        {
            Filter = SceneNodeFlags.Selected,
        };

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => WriteSceneWithManager(manager, scene, "filtered_materials.semodel", options));

        Assert.Contains("references material", ex.Message);
        Assert.Contains(mesh.Name, ex.Message);
        Assert.Contains(material.Name, ex.Message);
    }

    [Fact]
    public void SemodelTranslator_Write_Filter_ThrowsWhenSelectedMeshReferencesUnexportedBones()
    {
        Scene scene				= CreateSemodelSampleScene();
        Mesh mesh				= scene.GetDescendants<Mesh>().Single();
        Material material		= scene.GetDescendants<Material>().Single();
        SkeletonBone[] bones	= scene.GetDescendants<SkeletonBone>();

        mesh.Flags		= SceneNodeFlags.Selected;
        material.Flags	= SceneNodeFlags.Selected;
        bones[0].Flags = SceneNodeFlags.Selected;
        bones[1].Flags = SceneNodeFlags.None;

        SceneTranslatorManager manager	= CreateManagerWithSemodelTranslator();
        SceneTranslatorOptions options	= new()
        {
            Filter = SceneNodeFlags.Selected,
        };

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => WriteSceneWithManager(manager, scene, "filtered_bones.semodel", options));

        Assert.Contains("not included in the export selection", ex.Message);
        Assert.Contains(mesh.Name, ex.Message);
        Assert.Contains(bones[1].Name, ex.Message);
    }

    private static Scene CreateSemodelSampleScene()
    {
        Scene scene = new("SemodelSampleScene");

        Skeleton skeleton		= scene.RootNode.AddNode(new Skeleton("Armature"));
        SkeletonBone rootBone	= skeleton.AddNode(new SkeletonBone("root"));
        rootBone.BindTransform.LocalPosition	= new Vector3(0f, 0f, 0f);
        rootBone.BindTransform.LocalRotation	= Quaternion.Identity;
        rootBone.BindTransform.Scale			= new Vector3(1f, 1f, 1f);

        SkeletonBone childBone = rootBone.AddNode(new SkeletonBone("child"));
        childBone.BindTransform.LocalPosition	= new Vector3(0f, 1f, 0f);
        childBone.BindTransform.LocalRotation	= Quaternion.Identity;
        childBone.BindTransform.Scale			= new Vector3(1f, 1f, 1f);

        MeshGroup model	= scene.RootNode.AddNode(new MeshGroup { Name = "ModelRoot" });
        Mesh mesh		= model.AddNode(new Mesh { Name = "mesh_0" });

        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f,], 1, 3);

        mesh.Normals = new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f,], 1, 3);

        mesh.UVLayers = new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f,], 1, 2);

        mesh.ColorLayers = new DataBuffer<byte>([255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255,], 1, 4);

        DataBuffer indices = new DataBuffer<byte>([0, 1, 0, 1, 0, 1,], 2, 1);

        DataBuffer weights = new DataBuffer<float>([0.75f, 0.25f, 0.60f, 0.40f, 0.85f, 0.15f,], 2, 1);

        mesh.Skin			= new Skin([rootBone, childBone], indices, weights);
        mesh.FaceIndices	= new DataBuffer<ushort>(new ushort[] { 0, 1, 2 }, 1, 1);

        Material material = model.AddNode(new Material("material_0")
        {
            DiffuseMapName	= "diffuse",
            NormalMapName	= "normal",
            SpecularMapName	= "specular",
        });
        material.Connect("diffuse", material.AddNode(new Texture("textures\\diffuse_a.dds") { Name = "diffuse" }));
        material.Connect("normal", material.AddNode(new Texture("textures\\normal_a.dds") { Name = "normal" }));
        material.Connect("specular", material.AddNode(new Texture("textures\\spec_a.dds") { Name = "specular" }));

        return scene;
    }

    private static Scene CreatePosedSkinningScene()
    {
        Scene scene = new("PosedSkinningScene");

        Skeleton skeleton		= scene.RootNode.AddNode(new Skeleton("Armature"));
        SkeletonBone childBone	= skeleton.AddNode(new SkeletonBone("child"));
        childBone.BindTransform.LocalPosition	= new Vector3(1f, 0f, 0f);
        childBone.BindTransform.LocalRotation	= Quaternion.Identity;
        childBone.BindTransform.Scale			= Vector3.One;
        childBone.LiveTransform.LocalPosition	= new Vector3(2f, 0f, 0f);
        childBone.LiveTransform.LocalRotation	= Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f);

        MeshGroup model	= scene.RootNode.AddNode(new MeshGroup { Name = "ModelRoot" });
        Mesh mesh		= model.AddNode(new Mesh { Name = "skinned_mesh" });

        mesh.Positions = new DataBuffer<float>([1f, 0f, 0f, 2f, 0f, 0f, 1f, 1f, 0f,], 1, 3);

        mesh.Normals = new DataBuffer<float>([1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f,], 1, 3);

        DataBuffer indices = new DataBuffer<byte>([0, 0, 0,], 1, 1);

        DataBuffer weights = new DataBuffer<float>([1f, 1f, 1f,], 1, 1);

        mesh.Skin			= new Skin([childBone], indices, weights);
        mesh.FaceIndices	= new DataBuffer<ushort>(new ushort[] { 0, 1, 2 }, 1, 1);
        return scene;
    }

    private static void AssertSceneStructureEquivalent(Scene expectedScene, Scene actualScene)
    {
        SkeletonBone[] expectedSkeletons	= expectedScene.GetDescendants<SkeletonBone>().Where(b => b.Parent is not SkeletonBone).ToArray();
        SkeletonBone[] actualSkeletons		= actualScene.GetDescendants<SkeletonBone>().Where(b => b.Parent is not SkeletonBone).ToArray();

        Assert.Equal(expectedSkeletons.Length, actualSkeletons.Length);
        if (expectedSkeletons.Length > 0)
        {
            AssertSkeletonStructureEquivalent(expectedSkeletons[0], actualSkeletons[0]);
        }

        MeshGroup[] expectedModels	= expectedScene.GetDescendants<MeshGroup>();
        MeshGroup[] actualModels	= actualScene.GetDescendants<MeshGroup>();
        Assert.Equal(expectedModels.Length, actualModels.Length);

        Mesh[] expectedMeshes	= expectedScene.GetDescendants<Mesh>();
        Mesh[] actualMeshes		= actualScene.GetDescendants<Mesh>();
        Assert.Equal(expectedMeshes.Length, actualMeshes.Length);

        for (int i = 0; i < expectedMeshes.Length; i++)
        {
            AssertMeshStructureEquivalent(expectedMeshes[i], actualMeshes[i]);
        }

        Material[] expectedMaterials	= expectedScene.GetDescendants<Material>();
        Material[] actualMaterials		= actualScene.GetDescendants<Material>();
        Assert.Equal(expectedMaterials.Length, actualMaterials.Length);

        for (int i = 0; i < expectedMaterials.Length; i++)
        {
            AssertMaterialStructureEquivalent(expectedMaterials[i], actualMaterials[i]);
        }
    }

    private static void AssertSkeletonStructureEquivalent(SkeletonBone expectedSkeleton, SkeletonBone actualSkeleton)
    {
        SkeletonBone[] expectedBones	= expectedSkeleton.GetDescendants<SkeletonBone>();
        SkeletonBone[] actualBones		= actualSkeleton.GetDescendants<SkeletonBone>();
        Assert.Equal(expectedBones.Length, actualBones.Length);

        for (int i = 0; i < expectedBones.Length; i++)
        {
            SkeletonBone expectedBone	= expectedBones[i];
            SkeletonBone actualBone		= actualBones[i];
            Assert.Equal(expectedBone.Name, actualBone.Name);

            string expectedParentName	= expectedBone.Parent is SkeletonBone expectedParent ? expectedParent.Name : string.Empty;
            string actualParentName		= actualBone.Parent is SkeletonBone actualParent ? actualParent.Name : string.Empty;
            Assert.Equal(expectedParentName, actualParentName);
        }
    }

    private static void AssertMeshStructureEquivalent(Mesh expectedMesh, Mesh actualMesh)
    {
        Assert.Equal(expectedMesh.VertexCount, actualMesh.VertexCount);
        Assert.Equal(expectedMesh.FaceCount, actualMesh.FaceCount);
        Assert.Equal(expectedMesh.IndexCount, actualMesh.IndexCount);
        Assert.Equal(expectedMesh.UVLayerCount, actualMesh.UVLayerCount);
        Assert.Equal(expectedMesh.ColorLayerCount, actualMesh.ColorLayerCount);
        Assert.Equal(expectedMesh.Skin?.InfluenceCount, actualMesh.Skin?.InfluenceCount);
        Assert.Equal(expectedMesh.HasSkinning, actualMesh.HasSkinning);
        Assert.Equal((expectedMesh.Skin is not null), (actualMesh.Skin is not null));

        int expectedSkinnedBoneCount	= expectedMesh.Skin?.Bones?.Count ?? 0;
        int actualSkinnedBoneCount		= actualMesh.Skin?.Bones?.Count ?? 0;
        Assert.Equal(expectedSkinnedBoneCount, actualSkinnedBoneCount);

        if (expectedSkinnedBoneCount > 0)
        {
            for (int i = 0; i < expectedSkinnedBoneCount; i++)
            {
                Assert.Equal(expectedMesh.Skin?.Bones![i].Name, actualMesh.Skin?.Bones![i].Name);
            }
        }
    }

    private static void AssertMaterialStructureEquivalent(Material expectedMaterial, Material actualMaterial)
    {
        Assert.Equal(expectedMaterial.Name, actualMaterial.Name);
        Assert.Equal(expectedMaterial.DiffuseMapName, actualMaterial.DiffuseMapName);
        Assert.Equal(expectedMaterial.NormalMapName, actualMaterial.NormalMapName);
        Assert.Equal(expectedMaterial.SpecularMapName, actualMaterial.SpecularMapName);

        Texture[] expectedTextures	= expectedMaterial.GetDescendants<Texture>();
        Texture[] actualTextures	= actualMaterial.GetDescendants<Texture>();
        Assert.Equal(expectedTextures.Length, actualTextures.Length);

        foreach (Texture expectedTexture in expectedTextures)
        {
            var message = $"Expected texture '{expectedTexture.Name}' was not found on material '{actualMaterial.Name}'.";

            Assert.True(actualMaterial.TryFindDescendant(expectedTexture.Name, StringComparison.OrdinalIgnoreCase, out Texture? actualTexture), message);

            Assert.Equal(expectedTexture.Name, actualTexture!.Name);
            Assert.Equal(expectedTexture.FilePath, actualTexture.FilePath);
        }
    }

    private static SceneTranslatorManager CreateManagerWithSemodelTranslator()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new SemodelTranslator());
        return manager;
    }

    private static SceneTranslatorOptions CreateDefaultOptions()
    {
        return new SceneTranslatorOptions();
    }

    private static SceneTranslatorOptions CreateRawVertexOptions()
    {
        return new SceneTranslatorOptions
        {
            WriteRawVertices = true,
        };
    }

    private static byte[] WriteSceneWithManager(SceneTranslatorManager manager, Scene scene, string sourcePath)
    {
        return WriteSceneWithManager(manager, scene, sourcePath, CreateDefaultOptions());
    }

    private static byte[] WriteSceneWithManager(SceneTranslatorManager manager, Scene scene, string sourcePath, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, scene, options, token: null);
        return stream.ToArray();
    }

    private static Scene ReadSceneWithManager(SceneTranslatorManager manager, byte[] data, string sourcePath)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, sourcePath, CreateDefaultOptions(), token: null);
    }

    private static void AssertVector3Equal(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"Expected {expected} but received {actual}.");
    }

    private static string GetRequiredSemodelDirectory()
    {
        string? testsRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (string.IsNullOrWhiteSpace(testsRoot))
        {
            return string.Empty;
        }

        string inputDirectory = Path.Combine(testsRoot, "Input", "SEModel");
        if (Directory.Exists(inputDirectory))
        {
            return inputDirectory;
        }

        string legacyDirectory = Path.Combine(testsRoot, "SEModel");
        if (Directory.Exists(legacyDirectory))
        {
            return legacyDirectory;
        }

        return string.Empty;
    }
}
