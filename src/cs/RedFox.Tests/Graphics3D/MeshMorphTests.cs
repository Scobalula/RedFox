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
using RedFox.Graphics3D.Formats.Cast;
using RedFox.Graphics3D.Formats.GLTransmissionFormat;
using RedFox.Graphics3D.Formats.KaydaraFbx;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class MeshMorphTests
{
    private static Scene CreateMorphedScene()
    {
        Scene scene = new("morph");
        MeshGroup model = scene.RootNode.AddNode(new MeshGroup { Name = "ModelRoot" });
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });

        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        mesh.Morph = new Morph(["Smile", "Blink"], new DataBuffer<float>([1f, 0f, 0f, 0f, 1f, 0f, 2f, 0f, 0f, 0f, 2f, 0f, 3f, 0f, 0f, 0f, 3f, 0f], 2, 3));
        mesh.Morph.Weights[0] = 0.25f;
        mesh.Morph.Weights[1] = 0.75f;

        return scene;
    }

    [Fact]
    public void Morph_WithMismatchedTargetCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Morph(["A"], new DataBuffer<float>(new float[6], 2, 3)));
    }

    [Fact]
    public void Morph_WithMismatchedVertexCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Morph(["A"], new DataBuffer<float>(new float[6], 1, 3), new DataBuffer<float>(new float[3], 1, 3), null));
    }

    [Fact]
    public void GetVertexPosition_AppliesWeightedDeltas()
    {
        Mesh mesh = CreateMorphedScene().GetDescendants<Mesh>()[0];

        Assert.Equal(new Vector3(0.25f, 0.75f, 0f), mesh.GetVertexPosition(0));
        Assert.Equal(Vector3.Zero, mesh.GetVertexPosition(0, raw: true));
    }

    [Fact]
    public void GetVertexPosition_AppliesMorphBeforeSkinning()
    {
        SkeletonBone bone = new("Bone");
        Mesh mesh = CreateMorphedScene().GetDescendants<Mesh>()[0];
        mesh.Skin = new Skin([bone], new DataBuffer<int>([0, 0, 0], 1, 1), new DataBuffer<float>([1f, 1f, 1f], 1, 1));
        bone.LiveTransform.LocalPosition = new Vector3(0f, 10f, 0f);

        Assert.Equal(new Vector3(0.25f, 10.75f, 0f), mesh.GetVertexPosition(0));
    }

    [Fact]
    public void BakeCurrentPoseToVertices_WritesMorphedPositionsAndClearsMorph()
    {
        Mesh mesh = CreateMorphedScene().GetDescendants<Mesh>()[0];
        Vector3 posed = mesh.GetVertexPosition(1);

        mesh.BakeCurrentPoseToVertices();

        Assert.Null(mesh.Morph);
        Assert.Equal(posed, mesh.GetVertexPosition(1, raw: true));
    }

    [Fact]
    public void TryGetSceneBounds_ContainsMorphedPositions()
    {
        Mesh mesh = CreateMorphedScene().GetDescendants<Mesh>()[0];

        Assert.True(mesh.TryGetSceneBounds(out SceneBounds bounds));

        for (int v = 0; v < mesh.VertexCount; v++)
        {
            Vector3 position = mesh.GetVertexPosition(v);
            Assert.True(Vector3.Clamp(position, bounds.Min, bounds.Max) == position, $"Vertex {v} at {position} outside {bounds.Min}..{bounds.Max}");
        }
    }

    [Fact]
    public void Mesh_Clone_SharesDeltasButOwnsWeights()
    {
        Mesh source = CreateMorphedScene().GetDescendants<Mesh>()[0];

        Mesh clone = (Mesh)source.Clone();
        clone.Morph!.Weights[0] = 1f;

        Assert.Same(source.Morph!.DeltaPositions, clone.Morph.DeltaPositions);
        Assert.Equal(source.Morph.TargetNames, clone.Morph.TargetNames);
        Assert.Equal(0.25f, source.Morph.Weights[0]);
    }

    [Fact]
    public void Scene_CreateAnimationPlayers_DrivesMorphWeightsByTargetName()
    {
        Scene scene = CreateMorphedScene();
        Morph morph = scene.GetDescendants<Mesh>()[0].Morph!;

        MorphAnimation animation = scene.RootNode.AddNode(new MorphAnimation("Face"));
        MorphAnimationTrack blink = animation.GetOrCreateTrack("Blink");
        blink.AddWeightFrame(0f, 0f);
        blink.AddWeightFrame(10f, 1f);
        animation.GetOrCreateTrack("Missing").AddWeightFrame(0f, 1f);

        AnimationPlayer player = Assert.Single(scene.CreateAnimationPlayers());
        MorphAnimationSampler sampler = Assert.IsType<MorphAnimationSampler>(Assert.Single(player.Layers));
        sampler.Update(5f, AnimationSampleType.AbsoluteFrameTime);

        Assert.Single(sampler.Bindings);
        Assert.Equal(0.25f, morph.Weights[0]);
        Assert.Equal(0.5f, morph.Weights[1], 1e-4f);
    }

    [Fact]
    public void GltfTranslator_RoundTrip_PreservesMorph()
    {
        Scene source = CreateMorphedScene();
        Scene loaded = RoundTrip(new GltfTranslator(), source, "morph.glb");

        AssertMorphEqual(source, loaded, compareWeights: true);
    }

    [Fact]
    public void FbxTranslator_RoundTrip_PreservesMorph()
    {
        Scene source = CreateMorphedScene();
        Scene loaded = RoundTrip(new FbxTranslator(), source, "morph.fbx");

        AssertMorphEqual(source, loaded, compareWeights: true);
    }

    [Fact]
    public void FbxTranslator_RoundTrip_PreservesMorphNormals()
    {
        Scene source = new("morph");
        Mesh mesh = source.RootNode.AddNode(new MeshGroup { Name = "ModelRoot" }).AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.Normals = new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        mesh.Morph = new Morph(["Bend"], new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f], 1, 3), new DataBuffer<float>([0f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f], 1, 3), null);

        Morph loaded = Assert.Single(RoundTrip(new FbxTranslator(), source, "morph.fbx").GetDescendants<Mesh>()).Morph!;

        Assert.Equal(new Vector3(0f, 0.5f, 0f), loaded.DeltaNormals!.GetVector3(0, 0));
        Assert.Equal(Vector3.Zero, loaded.DeltaNormals.GetVector3(1, 0));
    }

    [Fact]
    public void CastTranslator_RoundTrip_PreservesMorph()
    {
        Scene source = CreateMorphedScene();
        Scene loaded = RoundTrip(new CastTranslator(), source, "morph.cast");

        AssertMorphEqual(source, loaded, compareWeights: false);
    }

    [Fact]
    public void CastTranslator_RoundTrip_PreservesMorphAnimation()
    {
        Scene source = CreateMorphedScene();
        MorphAnimation animation = source.RootNode.AddNode(new MorphAnimation("Face"));
        animation.GetOrCreateTrack("Blink").AddWeightFrame(0f, 0f);
        animation.GetOrCreateTrack("Blink").AddWeightFrame(10f, 1f);

        Scene loaded = RoundTrip(new CastTranslator(), source, "morph.cast");

        MorphAnimationTrack track = Assert.Single(Assert.Single(loaded.GetDescendants<MorphAnimation>()).Tracks);
        Assert.Equal("Blink", track.Name);
        Assert.Equal(2, track.KeyFrameCount);
        Assert.Equal(1f, track.SampleWeight(10f));
        Assert.Empty(loaded.GetDescendants<SkeletonAnimation>());
    }

    private static Scene RoundTrip(SceneTranslator translator, Scene source, string fileName)
    {
        SceneTranslatorManager manager = new();
        manager.Register(translator);

        using MemoryStream stream = new();
        manager.Write(stream, fileName, source, new SceneTranslatorOptions(), token: null);
        stream.Position = 0;
        return manager.Read(stream, fileName, new SceneTranslatorOptions(), token: null);
    }

    private static void AssertMorphEqual(Scene source, Scene loaded, bool compareWeights)
    {
        Morph sourceMorph = source.GetDescendants<Mesh>()[0].Morph!;
        Morph loadedMorph = Assert.Single(loaded.GetDescendants<Mesh>()).Morph!;

        Assert.Equal(sourceMorph.TargetNames, loadedMorph.TargetNames);

        if (compareWeights)
            Assert.Equal(sourceMorph.Weights, loadedMorph.Weights);

        for (int v = 0; v < sourceMorph.VertexCount; v++)
        {
            for (int t = 0; t < sourceMorph.TargetCount; t++)
                Assert.True(Vector3.Distance(sourceMorph.DeltaPositions!.GetVector3(v, t), loadedMorph.DeltaPositions!.GetVector3(v, t)) < 1e-5f);
        }
    }
}
