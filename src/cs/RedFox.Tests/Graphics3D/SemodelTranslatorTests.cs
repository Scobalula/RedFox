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
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Semodel;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class SemodelTranslatorTests
{
    [Fact]
    public void SemodelTranslator_RoundTripViaManager_PreservesSceneStructure()
    {
        Scene sourceScene = CreateSemodelSampleScene();
        SceneTranslatorManager manager = CreateManagerWithSemodelTranslator();
        byte[] firstWrite = WriteSceneWithManager(manager, sourceScene, "sample.semodel");
        Scene firstLoad = ReadSceneWithManager(manager, firstWrite, "sample.semodel");
        byte[] secondWrite = WriteSceneWithManager(manager, firstLoad, "sample.semodel");
        Scene secondLoad = ReadSceneWithManager(manager, secondWrite, "sample.semodel");

        AssertSceneStructureEquivalent(firstLoad, secondLoad);
    }

    [Fact]
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
            Scene sourceScene = manager.Read(inputFileStream, semodelFile, CreateDefaultOptions(), token: null);
            byte[] firstWrite = WriteSceneWithManager(manager, sourceScene, semodelFile);
            Scene firstLoad = ReadSceneWithManager(manager, firstWrite, semodelFile);
            byte[] secondWrite = WriteSceneWithManager(manager, firstLoad, semodelFile);
            Scene secondLoad = ReadSceneWithManager(manager, secondWrite, semodelFile);

            AssertSceneStructureEquivalent(firstLoad, secondLoad);
        }
    }

    private static Scene CreateSemodelSampleScene()
    {
        Scene scene = new("SemodelSampleScene");

        Skeleton skeleton = scene.RootNode.AddNode(new Skeleton("Armature"));
        SkeletonBone rootBone = skeleton.AddNode(new SkeletonBone("root"));
        rootBone.BindTransform.LocalPosition = new Vector3(0f, 0f, 0f);
        rootBone.BindTransform.LocalRotation = Quaternion.Identity;
        rootBone.BindTransform.Scale = new Vector3(1f, 1f, 1f);

        SkeletonBone childBone = rootBone.AddNode(new SkeletonBone("child"));
        childBone.BindTransform.LocalPosition = new Vector3(0f, 1f, 0f);
        childBone.BindTransform.LocalRotation = Quaternion.Identity;
        childBone.BindTransform.Scale = new Vector3(1f, 1f, 1f);

        Model model = scene.RootNode.AddNode(new Model { Name = "ModelRoot" });
        Mesh mesh = model.AddNode(new Mesh { Name = "mesh_0" });

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

        mesh.ColorLayers = new DataBuffer<byte>(
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
        ], 1, 4);

        mesh.BoneIndices = new DataBuffer<byte>(
        [
            0, 1,
            0, 1,
            0, 1,
        ], 2, 1);

        mesh.BoneWeights = new DataBuffer<float>(
        [
            0.75f, 0.25f,
            0.60f, 0.40f,
            0.85f, 0.15f,
        ], 2, 1);

        mesh.SetSkinBinding(skeleton, [rootBone, childBone]);
        mesh.FaceIndices = new DataBuffer<ushort>([0, 1, 2], 1, 1);

        Material material = model.AddNode(new Material("material_0")
        {
            DiffuseMapName = "diffuse_a",
            NormalMapName = "normal_a",
            SpecularMapName = "spec_a",
        });
        material.AddNode(new Texture("textures\\diffuse_a.dds", "diffuse"));
        material.AddNode(new Texture("textures\\normal_a.dds", "normal"));
        material.AddNode(new Texture("textures\\spec_a.dds", "specular"));

        return scene;
    }

    private static void AssertSceneStructureEquivalent(Scene expectedScene, Scene actualScene)
    {
        Skeleton[] expectedSkeletons = [.. expectedScene.GetDescendants<Skeleton>().Where(static skeleton => skeleton is not SkeletonBone)];
        Skeleton[] actualSkeletons = [.. actualScene.GetDescendants<Skeleton>().Where(static skeleton => skeleton is not SkeletonBone)];

        Assert.Equal(expectedSkeletons.Length, actualSkeletons.Length);
        if (expectedSkeletons.Length > 0)
        {
            AssertSkeletonStructureEquivalent(expectedSkeletons[0], actualSkeletons[0]);
        }

        Model[] expectedModels = expectedScene.GetDescendants<Model>();
        Model[] actualModels = actualScene.GetDescendants<Model>();
        Assert.Equal(expectedModels.Length, actualModels.Length);

        Mesh[] expectedMeshes = expectedScene.GetDescendants<Mesh>();
        Mesh[] actualMeshes = actualScene.GetDescendants<Mesh>();
        Assert.Equal(expectedMeshes.Length, actualMeshes.Length);

        for (int i = 0; i < expectedMeshes.Length; i++)
        {
            AssertMeshStructureEquivalent(expectedMeshes[i], actualMeshes[i]);
        }

        Material[] expectedMaterials = expectedScene.GetDescendants<Material>();
        Material[] actualMaterials = actualScene.GetDescendants<Material>();
        Assert.Equal(expectedMaterials.Length, actualMaterials.Length);

        for (int i = 0; i < expectedMaterials.Length; i++)
        {
            AssertMaterialStructureEquivalent(expectedMaterials[i], actualMaterials[i]);
        }
    }

    private static void AssertSkeletonStructureEquivalent(Skeleton expectedSkeleton, Skeleton actualSkeleton)
    {
        SkeletonBone[] expectedBones = expectedSkeleton.GetBones();
        SkeletonBone[] actualBones = actualSkeleton.GetBones();
        Assert.Equal(expectedBones.Length, actualBones.Length);

        for (int i = 0; i < expectedBones.Length; i++)
        {
            SkeletonBone expectedBone = expectedBones[i];
            SkeletonBone actualBone = actualBones[i];
            Assert.Equal(expectedBone.Name, actualBone.Name);

            string expectedParentName = expectedBone.Parent is SkeletonBone expectedParent ? expectedParent.Name : string.Empty;
            string actualParentName = actualBone.Parent is SkeletonBone actualParent ? actualParent.Name : string.Empty;
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
        Assert.Equal(expectedMesh.SkinInfluenceCount, actualMesh.SkinInfluenceCount);
        Assert.Equal(expectedMesh.HasSkinning, actualMesh.HasSkinning);
        Assert.Equal(expectedMesh.HasExplicitSkinPalette, actualMesh.HasExplicitSkinPalette);

        int expectedSkinnedBoneCount = expectedMesh.SkinnedBones?.Count ?? 0;
        int actualSkinnedBoneCount = actualMesh.SkinnedBones?.Count ?? 0;
        Assert.Equal(expectedSkinnedBoneCount, actualSkinnedBoneCount);

        if (expectedSkinnedBoneCount > 0)
        {
            for (int i = 0; i < expectedSkinnedBoneCount; i++)
            {
                Assert.Equal(expectedMesh.SkinnedBones![i].Name, actualMesh.SkinnedBones![i].Name);
            }
        }
    }

    private static void AssertMaterialStructureEquivalent(Material expectedMaterial, Material actualMaterial)
    {
        Assert.Equal(expectedMaterial.Name, actualMaterial.Name);
        Assert.Equal(expectedMaterial.DiffuseMapName, actualMaterial.DiffuseMapName);
        Assert.Equal(expectedMaterial.NormalMapName, actualMaterial.NormalMapName);
        Assert.Equal(expectedMaterial.SpecularMapName, actualMaterial.SpecularMapName);

        Texture[] expectedTextures = expectedMaterial.GetDescendants<Texture>();
        Texture[] actualTextures = actualMaterial.GetDescendants<Texture>();
        Assert.Equal(expectedTextures.Length, actualTextures.Length);

        foreach (Texture expectedTexture in expectedTextures)
        {
            var message = $"Expected texture '{expectedTexture.Name}' was not found on material '{actualMaterial.Name}'.";

            Assert.True(actualMaterial.TryFindDescendant(expectedTexture.Name, StringComparison.OrdinalIgnoreCase, out Texture? actualTexture), message);

            Assert.Equal(expectedTexture.Slot, actualTexture!.Slot);
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

    private static byte[] WriteSceneWithManager(SceneTranslatorManager manager, Scene scene, string sourcePath)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, scene, CreateDefaultOptions(), token: null);
        return stream.ToArray();
    }

    private static Scene ReadSceneWithManager(SceneTranslatorManager manager, byte[] data, string sourcePath)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, sourcePath, CreateDefaultOptions(), token: null);
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
