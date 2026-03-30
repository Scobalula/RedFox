using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Md5;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class Md5TranslatorTests
{
    // ------------------------------------------------------------------
    // MD5 Mesh round-trip tests
    // ------------------------------------------------------------------

    [Fact]
    public void Md5MeshTranslator_RoundTrip_PreservesPositions()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        Mesh[] sourceMeshes = source.GetDescendants<Mesh>();
        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();

        Assert.Single(loadedMeshes);
        Assert.NotNull(loadedMeshes[0].Positions);
        Assert.Equal(sourceMeshes[0].Positions!.ElementCount, loadedMeshes[0].Positions!.ElementCount);

        int vertCount = sourceMeshes[0].Positions!.ElementCount;
        for (int i = 0; i < vertCount; i++)
        {
            AssertVector3Near(
                sourceMeshes[0].Positions!.GetVector3(i, 0),
                loadedMeshes[0].Positions!.GetVector3(i, 0),
                1e-2f,
                $"position vertex {i}");
        }
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_PreservesUVs()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        Mesh[] sourceMeshes = source.GetDescendants<Mesh>();
        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();

        Assert.Single(loadedMeshes);
        Assert.NotNull(loadedMeshes[0].UVLayers);

        int vertCount = sourceMeshes[0].UVLayers!.ElementCount;
        for (int i = 0; i < vertCount; i++)
        {
            AssertVector2Near(
                sourceMeshes[0].UVLayers!.GetVector2(i, 0),
                loadedMeshes[0].UVLayers!.GetVector2(i, 0),
                1e-4f,
                $"UV vertex {i}");
        }
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_PreservesFaceCount()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        Mesh[] sourceMeshes = source.GetDescendants<Mesh>();
        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();

        Assert.Equal(sourceMeshes[0].FaceCount, loadedMeshes[0].FaceCount);
        Assert.Equal(sourceMeshes[0].VertexCount, loadedMeshes[0].VertexCount);
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_PreservesBoneNames()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        SkeletonBone[] sourceBones = source.GetDescendants<SkeletonBone>();
        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();

        Assert.Equal(sourceBones.Length, loadedBones.Length);
        for (int i = 0; i < sourceBones.Length; i++)
            Assert.Equal(sourceBones[i].Name, loadedBones[i].Name);
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_PreservesBindPose()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        SkeletonBone[] sourceBones = source.GetDescendants<SkeletonBone>();
        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();

        for (int i = 0; i < sourceBones.Length; i++)
        {
            AssertVector3Near(
                sourceBones[i].BindTransform.LocalPosition ?? Vector3.Zero,
                loadedBones[i].BindTransform.LocalPosition ?? Vector3.Zero,
                1e-3f,
                $"bind position bone {i} ({sourceBones[i].Name})");
        }
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_PreservesMaterialName()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();
        Assert.Single(loadedMeshes);
        Assert.NotNull(loadedMeshes[0].Materials);
        Assert.Contains(loadedMeshes[0].Materials!, m => m.Name == "test_material");
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_PreservesSkinning()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();
        Assert.NotNull(loadedMeshes[0].BoneIndices);
        Assert.NotNull(loadedMeshes[0].BoneWeights);
        Assert.NotNull(loadedMeshes[0].SkinnedBones);
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_ComputesNormals()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();
        Assert.NotNull(loadedMeshes[0].Normals);

        int vertCount = loadedMeshes[0].Normals!.ElementCount;
        for (int i = 0; i < vertCount; i++)
        {
            var n = loadedMeshes[0].Normals!.GetVector3(i, 0);
            float len = n.Length();
            Assert.True(len > 0.99f && len < 1.01f, $"Normal {i} not unit length: {len}");
        }
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_TangentsAreOptionalNotComputed()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();
        Assert.Null(loadedMeshes[0].Tangents);
    }

    [Fact]
    public void Md5MeshTranslator_RoundTrip_SkeletonHierarchyPreserved()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        Scene loaded = ReadScene(manager, data, "test.md5mesh");

        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();
        SkeletonBone? bone1 = loadedBones.FirstOrDefault(b => b.Name == "bone1");
        Assert.NotNull(bone1);
        Assert.IsType<SkeletonBone>(bone1.Parent);
        Assert.Equal("root", ((SkeletonBone)bone1.Parent!).Name);
    }

    // ------------------------------------------------------------------
    // MD5 Mesh format correctness
    // ------------------------------------------------------------------

    [Fact]
    public void Md5MeshTranslator_Write_OutputStartsWithMd5Version10()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        string text = Encoding.UTF8.GetString(data);

        Assert.StartsWith("MD5Version 10", text.TrimStart());
    }

    [Fact]
    public void Md5MeshTranslator_Write_ContainsRequiredSections()
    {
        Scene source = CreateSkinnedMeshScene();
        var manager = CreateMeshManager();

        byte[] data = WriteScene(manager, source, "test.md5mesh");
        string text = Encoding.UTF8.GetString(data);

        Assert.Contains("numJoints", text);
        Assert.Contains("numMeshes", text);
        Assert.Contains("joints {", text);
        Assert.Contains("mesh {", text);
        Assert.Contains("numverts", text);
        Assert.Contains("numtris", text);
        Assert.Contains("numweights", text);
    }

    // ------------------------------------------------------------------
    // MD5 Anim round-trip tests
    // ------------------------------------------------------------------

    [Fact]
    public void Md5AnimTranslator_RoundTrip_PreservesTrackCount()
    {
        Scene source = CreateAnimationScene();
        var manager = CreateAnimManager();

        byte[] data = WriteScene(manager, source, "test.md5anim");
        Scene loaded = ReadScene(manager, data, "test.md5anim");

        SkeletonAnimation sourceAnim = source.FirstOfType<SkeletonAnimation>();
        SkeletonAnimation loadedAnim = loaded.FirstOfType<SkeletonAnimation>();

        Assert.Equal(sourceAnim.Tracks.Count, loadedAnim.Tracks.Count);
    }

    [Fact]
    public void Md5AnimTranslator_RoundTrip_PreservesTrackBoneNames()
    {
        Scene source = CreateAnimationScene();
        var manager = CreateAnimManager();

        byte[] data = WriteScene(manager, source, "test.md5anim");
        Scene loaded = ReadScene(manager, data, "test.md5anim");

        SkeletonAnimation sourceAnim = source.FirstOfType<SkeletonAnimation>();
        SkeletonAnimation loadedAnim = loaded.FirstOfType<SkeletonAnimation>();

        for (int i = 0; i < sourceAnim.Tracks.Count; i++)
            Assert.Equal(sourceAnim.Tracks[i].Name, loadedAnim.Tracks[i].Name);
    }

    [Fact]
    public void Md5AnimTranslator_RoundTrip_PreservesBoneNames()
    {
        Scene source = CreateAnimationScene();
        var manager = CreateAnimManager();

        byte[] data = WriteScene(manager, source, "test.md5anim");
        Scene loaded = ReadScene(manager, data, "test.md5anim");

        SkeletonBone[] sourceBones = source.GetDescendants<SkeletonBone>();
        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();

        Assert.Equal(sourceBones.Length, loadedBones.Length);
        for (int i = 0; i < sourceBones.Length; i++)
            Assert.Equal(sourceBones[i].Name, loadedBones[i].Name);
    }

    [Fact]
    public void Md5AnimTranslator_RoundTrip_PreservesBindPose()
    {
        Scene source = CreateAnimationScene();
        var manager = CreateAnimManager();

        byte[] data = WriteScene(manager, source, "test.md5anim");
        Scene loaded = ReadScene(manager, data, "test.md5anim");

        SkeletonBone[] sourceBones = source.GetDescendants<SkeletonBone>();
        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();

        for (int i = 0; i < sourceBones.Length; i++)
        {
            AssertVector3Near(
                sourceBones[i].BindTransform.LocalPosition ?? Vector3.Zero,
                loadedBones[i].BindTransform.LocalPosition ?? Vector3.Zero,
                1e-3f,
                $"anim bind position bone {i} ({sourceBones[i].Name})");
        }
    }

    [Fact]
    public void Md5AnimTranslator_RoundTrip_PreservesKeyFramePositions()
    {
        Scene source = CreateAnimationScene();
        var manager = CreateAnimManager();

        byte[] data = WriteScene(manager, source, "test.md5anim");
        Scene loaded = ReadScene(manager, data, "test.md5anim");

        SkeletonAnimation sourceAnim = source.FirstOfType<SkeletonAnimation>();
        SkeletonAnimation loadedAnim = loaded.FirstOfType<SkeletonAnimation>();

        for (int t = 0; t < sourceAnim.Tracks.Count; t++)
        {
            var sSrc = sourceAnim.Tracks[t];
            var sDst = loadedAnim.Tracks[t];

            if (sSrc.TranslationCurve is null) continue;

            Assert.NotNull(sDst.TranslationCurve);

            float[] sampleTimes = [0f, 1f, 2f];
            foreach (float time in sampleTimes)
            {
                AssertVector3Near(
                    sSrc.TranslationCurve.SampleVector3(time),
                    sDst.TranslationCurve!.SampleVector3(time),
                    1e-2f,
                    $"anim track {t} ({sSrc.Name}) position at t={time}");
            }
        }
    }

    // ------------------------------------------------------------------
    // MD5 Anim format correctness
    // ------------------------------------------------------------------

    [Fact]
    public void Md5AnimTranslator_Write_OutputStartsWithMd5Version10()
    {
        Scene source = CreateAnimationScene();
        var manager = CreateAnimManager();

        byte[] data = WriteScene(manager, source, "test.md5anim");
        string text = Encoding.UTF8.GetString(data);

        Assert.StartsWith("MD5Version 10", text.TrimStart());
    }

    [Fact]
    public void Md5AnimTranslator_Write_ContainsRequiredSections()
    {
        Scene source = CreateAnimationScene();
        var manager = CreateAnimManager();

        byte[] data = WriteScene(manager, source, "test.md5anim");
        string text = Encoding.UTF8.GetString(data);

        Assert.Contains("numFrames", text);
        Assert.Contains("numJoints", text);
        Assert.Contains("frameRate", text);
        Assert.Contains("numAnimatedComponents", text);
        Assert.Contains("hierarchy {", text);
        Assert.Contains("bounds {", text);
        Assert.Contains("baseframe {", text);
        Assert.Contains("frame 0 {", text);
    }

    // ------------------------------------------------------------------
    // Edge cases
    // ------------------------------------------------------------------

    [Fact]
    public void Md5MeshTranslator_Read_EmptyStream_DoesNotThrow()
    {
        var translator = new Md5MeshTranslator();
        using var ms = new MemoryStream(0);
        var scene = new Scene("empty");
        translator.Read(scene, ms, "test", new SceneTranslatorOptions(), token: null);
        int boneCount = scene.GetDescendants<SkeletonBone>().Length;
        Assert.Equal(0, boneCount);
    }

    [Fact]
    public void Md5AnimTranslator_Read_EmptyStream_DoesNotThrow()
    {
        var translator = new Md5AnimTranslator();
        using var ms = new MemoryStream(0);
        var scene = new Scene("empty");
        translator.Read(scene, ms, "test", new SceneTranslatorOptions(), token: null);
        int boneCount = scene.GetDescendants<SkeletonBone>().Length;
        Assert.Equal(0, boneCount);
    }

    // ------------------------------------------------------------------
    // Real file reading tests
    // ------------------------------------------------------------------

    [Fact]
    public void Md5MeshTranslator_Read_InvisoMd5Mesh_ParsesCorrectly()
    {
        string path = @"Z:\Tests\RedFox\Input\Md5\inviso.md5mesh";
        if (!File.Exists(path)) return;

        var manager = CreateMeshManager();
        using var fs = File.OpenRead(path);
        Scene scene = manager.Read(fs, "inviso.md5mesh", CreateDefaultOptions(), token: null);

        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        Assert.Single(bones);
        Assert.Equal("origin", bones[0].Name);

        Mesh[] meshes = scene.GetDescendants<Mesh>();
        Assert.Single(meshes);
        Assert.Equal(3, meshes[0].VertexCount);
        Assert.Equal(1, meshes[0].FaceCount);
        Assert.NotNull(meshes[0].Positions);
        Assert.NotNull(meshes[0].UVLayers);
        Assert.NotNull(meshes[0].Normals);
        Assert.NotNull(meshes[0].FaceIndices);
    }

    [Fact]
    public void Md5MeshTranslator_Read_A2ElevatorMesh_ParsesCorrectly()
    {
        string path = @"Z:\Tests\RedFox\Input\Md5\a2_elevatormesh.md5mesh";
        if (!File.Exists(path)) return;

        var manager = CreateMeshManager();
        using var fs = File.OpenRead(path);
        Scene scene = manager.Read(fs, "a2_elevatormesh.md5mesh", CreateDefaultOptions(), token: null);

        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        Assert.Equal(7, bones.Length);
        Assert.Equal("origin", bones[0].Name);

        Mesh[] meshes = scene.GetDescendants<Mesh>();
        Assert.Single(meshes);
        Assert.Equal(30, meshes[0].VertexCount);
        Assert.Equal(36, meshes[0].FaceCount);
        Assert.NotNull(meshes[0].BoneIndices);
        Assert.NotNull(meshes[0].BoneWeights);
    }

    [Fact]
    public void Md5AnimTranslator_Read_PlatformMd5Anim_ParsesCorrectly()
    {
        string path = @"Z:\Tests\RedFox\Input\Md5\platform.md5anim";
        if (!File.Exists(path)) return;

        var manager = CreateAnimManager();
        using var fs = File.OpenRead(path);
        Scene scene = manager.Read(fs, "platform.md5anim", CreateDefaultOptions(), token: null);

        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        Assert.Equal(5, bones.Length);
        Assert.Equal("origin", bones[0].Name);
        Assert.Equal("chaintop", bones[1].Name);
        Assert.Equal("chainmid", bones[2].Name);
        Assert.Equal("pyramidtop", bones[3].Name);
        Assert.Equal("girders", bones[4].Name);

        // 1 frame, 0 animated components — animation still created with tracks
        SkeletonAnimation? anim = scene.TryGetFirstOfType<SkeletonAnimation>();
        Assert.NotNull(anim);
        Assert.Equal(5, anim!.Tracks.Count);
    }

    [Fact]
    public void Md5AnimTranslator_Read_A2ElevatorIdle_ParsesCorrectly()
    {
        string path = @"Z:\Tests\RedFox\Input\Md5\a2_elevatoridle.md5anim";
        if (!File.Exists(path)) return;

        var manager = CreateAnimManager();
        using var fs = File.OpenRead(path);
        Scene scene = manager.Read(fs, "a2_elevatoridle.md5anim", CreateDefaultOptions(), token: null);

        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        Assert.Equal(7, bones.Length);

        SkeletonAnimation? anim = scene.TryGetFirstOfType<SkeletonAnimation>();
        Assert.NotNull(anim);
        Assert.Equal(7, anim!.Tracks.Count);
        Assert.Equal(24, (int)anim.Framerate);
    }

    // ------------------------------------------------------------------
    // Corpus tests — read all test files without crashing
    // ------------------------------------------------------------------

    [Fact]
    public void Md5MeshTranslator_Read_AllCorpusFiles_DoNotThrow()
    {
        string dir = @"Z:\Tests\RedFox\Input\Md5";
        if (!Directory.Exists(dir)) return;

        var manager = CreateMeshManager();
        string[] files = Directory.GetFiles(dir, "*.md5mesh");
        Assert.NotEmpty(files);

        foreach (string file in files)
        {
            using var fs = File.OpenRead(file);
            Scene scene = manager.Read(fs, Path.GetFileName(file), CreateDefaultOptions(), token: null);

            // Basic sanity: if the file has joints, we should have bones
            SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
            Mesh[] meshes = scene.GetDescendants<Mesh>();

            // Every valid md5mesh should have at least one bone and one mesh
            Assert.True(bones.Length > 0, $"No bones in {Path.GetFileName(file)}");
            Assert.True(meshes.Length > 0, $"No meshes in {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void Md5AnimTranslator_Read_AllCorpusFiles_DoNotThrow()
    {
        string dir = @"Z:\Tests\RedFox\Input\Md5";
        if (!Directory.Exists(dir)) return;

        var manager = CreateAnimManager();
        string[] files = Directory.GetFiles(dir, "*.md5anim");
        Assert.NotEmpty(files);

        foreach (string file in files)
        {
            using var fs = File.OpenRead(file);
            Scene scene = manager.Read(fs, Path.GetFileName(file), CreateDefaultOptions(), token: null);

            SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
            Assert.True(bones.Length > 0, $"No bones in {Path.GetFileName(file)}");
        }
    }

    // ------------------------------------------------------------------
    // Scene factory helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a scene with a skinned two-bone triangle mesh suitable for MD5 mesh round-trip tests.
    /// </summary>
    private static Scene CreateSkinnedMeshScene()
    {
        Scene scene = new("Md5TestScene");

        Skeleton skel = scene.RootNode.AddNode<Skeleton>("Skeleton");
        var root = skel.AddNode(new SkeletonBone("root"));
        root.BindTransform.LocalPosition = Vector3.Zero;
        root.BindTransform.LocalRotation = Quaternion.Identity;

        var bone1 = root.AddNode(new SkeletonBone("bone1"));
        bone1.BindTransform.LocalPosition = new Vector3(0f, 2f, 0f);
        bone1.BindTransform.LocalRotation = Quaternion.Identity;

        Model model = scene.RootNode.AddNode<Model>("Model");
        Mesh mesh = model.AddNode<Mesh>("test_material");

        mesh.Positions = MakeFloat3Buffer(
        [
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
        ]);
        mesh.Normals = MakeFloat3Buffer(
        [
            Vector3.UnitZ,
            Vector3.UnitZ,
            Vector3.UnitZ,
        ]);
        mesh.UVLayers = MakeFloat2Buffer(
        [
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
        ]);
        mesh.FaceIndices = MakeIntBuffer([0, 1, 2]);

        // Skinning: 2 influences per vertex
        var boneIndices = new DataBuffer<int>(3, 2, 1);
        var boneWeights = new DataBuffer<float>(3, 2, 1);
        for (int v = 0; v < 3; v++)
        {
            boneIndices.Add(v, 0, 0, 0);   boneWeights.Add(v, 0, 0, 0.6f);
            boneIndices.Add(v, 1, 0, 1);   boneWeights.Add(v, 1, 0, 0.4f);
        }
        mesh.BoneIndices = boneIndices;
        mesh.BoneWeights = boneWeights;
        mesh.SkinnedBones = [root, bone1];

        var mat = model.AddNode<Material>("test_material");
        mesh.Materials = [mat];

        return scene;
    }

    /// <summary>
    /// Creates a scene with a skeleton and animation suitable for MD5 anim round-trip tests.
    /// </summary>
    private static Scene CreateAnimationScene()
    {
        Scene scene = new("Md5AnimScene");

        Skeleton skel = scene.RootNode.AddNode<Skeleton>("Skeleton");
        var root = skel.AddNode(new SkeletonBone("root"));
        root.BindTransform.LocalPosition = Vector3.Zero;
        root.BindTransform.LocalRotation = Quaternion.Identity;

        var bone1 = root.AddNode(new SkeletonBone("bone1"));
        bone1.BindTransform.LocalPosition = new Vector3(0f, 2f, 0f);
        bone1.BindTransform.LocalRotation = Quaternion.Identity;

        var anim = scene.RootNode.AddNode(new SkeletonAnimation("TestAnim", null, 2, TransformType.Absolute)
        {
            Framerate = 24f,
            TransformType = TransformType.Absolute,
            TransformSpace = TransformSpace.Local,
        });

        var trackRoot = new SkeletonAnimationTrack("root")
        {
            TransformType = TransformType.Absolute,
            TransformSpace = TransformSpace.Local,
        };
        var trackBone1 = new SkeletonAnimationTrack("bone1")
        {
            TransformType = TransformType.Absolute,
            TransformSpace = TransformSpace.Local,
        };

        trackRoot.AddTranslationFrame(0f, Vector3.Zero);
        trackRoot.AddTranslationFrame(1f, new Vector3(1f, 0f, 0f));
        trackRoot.AddTranslationFrame(2f, new Vector3(2f, 0f, 0f));

        trackRoot.AddRotationFrame(0f, Quaternion.Identity);
        trackRoot.AddRotationFrame(2f, Quaternion.Identity);

        trackBone1.AddTranslationFrame(0f, new Vector3(0f, 2f, 0f));
        trackBone1.AddTranslationFrame(2f, new Vector3(0f, 2f, 0f));

        trackBone1.AddRotationFrame(0f, Quaternion.Identity);
        trackBone1.AddRotationFrame(2f, Quaternion.Identity);

        anim.Tracks.Add(trackRoot);
        anim.Tracks.Add(trackBone1);

        return scene;
    }

    // ------------------------------------------------------------------
    // Buffer builders
    // ------------------------------------------------------------------

    private static DataBuffer<float> MakeFloat3Buffer(IReadOnlyList<Vector3> vectors)
    {
        var buf = new DataBuffer<float>(vectors.Count, 1, 3);
        for (int i = 0; i < vectors.Count; i++)
        {
            buf.Add(i, 0, 0, vectors[i].X);
            buf.Add(i, 0, 1, vectors[i].Y);
            buf.Add(i, 0, 2, vectors[i].Z);
        }
        return buf;
    }

    private static DataBuffer<float> MakeFloat2Buffer(IReadOnlyList<Vector2> vectors)
    {
        var buf = new DataBuffer<float>(vectors.Count, 1, 2);
        for (int i = 0; i < vectors.Count; i++)
        {
            buf.Add(i, 0, 0, vectors[i].X);
            buf.Add(i, 0, 1, vectors[i].Y);
        }
        return buf;
    }

    private static DataBuffer<int> MakeIntBuffer(IReadOnlyList<int> values)
    {
        var buf = new DataBuffer<int>(values.Count, 1, 1);
        for (int i = 0; i < values.Count; i++)
            buf.Add(i, 0, 0, values[i]);
        return buf;
    }

    // ------------------------------------------------------------------
    // Translator manager helpers
    // ------------------------------------------------------------------

    private static SceneTranslatorManager CreateMeshManager()
    {
        var mgr = new SceneTranslatorManager();
        mgr.Register(new Md5MeshTranslator());
        return mgr;
    }

    private static SceneTranslatorManager CreateAnimManager()
    {
        var mgr = new SceneTranslatorManager();
        mgr.Register(new Md5AnimTranslator());
        return mgr;
    }

    private static SceneTranslatorOptions CreateDefaultOptions() => new();

    private static byte[] WriteScene(SceneTranslatorManager manager, Scene scene, string fileName)
    {
        using var ms = new MemoryStream();
        manager.Write(ms, fileName, scene, CreateDefaultOptions(), token: null);
        return ms.ToArray();
    }

    private static Scene ReadScene(SceneTranslatorManager manager, byte[] data, string fileName)
    {
        using var ms = new MemoryStream(data);
        return manager.Read(ms, fileName, CreateDefaultOptions(), token: null);
    }

    // ------------------------------------------------------------------
    // Assertion helpers
    // ------------------------------------------------------------------

    private static void AssertVector3Near(Vector3 expected, Vector3 actual, float tolerance, string label)
    {
        Assert.True(
            MathF.Abs(expected.X - actual.X) <= tolerance &&
            MathF.Abs(expected.Y - actual.Y) <= tolerance &&
            MathF.Abs(expected.Z - actual.Z) <= tolerance,
            $"{label}: expected ({expected.X},{expected.Y},{expected.Z}) got ({actual.X},{actual.Y},{actual.Z})");
    }

    private static void AssertVector2Near(Vector2 expected, Vector2 actual, float tolerance, string label)
    {
        Assert.True(
            MathF.Abs(expected.X - actual.X) <= tolerance &&
            MathF.Abs(expected.Y - actual.Y) <= tolerance,
            $"{label}: expected ({expected.X},{expected.Y}) got ({actual.X},{actual.Y})");
    }
}
