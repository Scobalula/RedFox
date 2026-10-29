using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Formats.StudioMDL;

namespace RedFox.Tests.Graphics3D;

public sealed class SmdTranslatorTests
{

    [Fact]
    public void SmdTranslator_ReferenceSmd_RoundTrip_PreservesPositionsNormalsUVs()
    {
        Scene source	= CreateSingleTriangleScene();
        var manager		= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "mesh.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "mesh.smd");

        Mesh[] sourceMeshes= source.GetDescendants<Mesh>();
        Mesh[] loadedMeshes= loaded.GetDescendants<Mesh>();

        Assert.Single(loadedMeshes);
        Assert.Equal(sourceMeshes[0].Positions!.ElementCount, loadedMeshes[0].Positions!.ElementCount);

        int vertCount = sourceMeshes[0].Positions!.ElementCount;
        for (int i = 0; i < vertCount; i++)
        {
            AssertVector3Near(sourceMeshes[0].Positions!.GetVector3(i, 0), loadedMeshes[0].Positions!.GetVector3(i, 0), 1e-4f, $"position vertex {i}");

            AssertVector3Near(sourceMeshes[0].Normals!.GetVector3(i, 0), loadedMeshes[0].Normals!.GetVector3(i, 0), 1e-4f, $"normal vertex {i}");

            AssertVector2Near(sourceMeshes[0].UVLayers!.GetVector2(i, 0), loadedMeshes[0].UVLayers!.GetVector2(i, 0), 1e-4f, $"UV vertex {i}");
        }
    }

    [Fact]
    public void SmdTranslator_ReferenceSmd_RoundTrip_PreservesSkeletonBoneNames()
    {
        Scene	source	= CreateSkinnedMeshScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "skinned.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "skinned.smd");

        SkeletonBone[] sourceBones= source.GetDescendants<SkeletonBone>();
        SkeletonBone[] loadedBones= loaded.GetDescendants<SkeletonBone>();

        Assert.Equal(sourceBones.Length, loadedBones.Length);

        for (int i = 0; i < sourceBones.Length; i++)
            Assert.Equal(sourceBones[i].Name, loadedBones[i].Name);
    }

    [Fact]
    public void SmdTranslator_ReferenceSmd_RoundTrip_PreservesBindPose()
    {
        Scene	source	= CreateSkinnedMeshScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "skinned.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "skinned.smd");

        SkeletonBone[] sourceBones= source.GetDescendants<SkeletonBone>();
        SkeletonBone[] loadedBones= loaded.GetDescendants<SkeletonBone>();

        for (int i = 0; i < sourceBones.Length; i++)
        {
            AssertVector3Near(sourceBones[i].BindTransform.LocalPosition ?? Vector3.Zero, loadedBones[i].BindTransform.LocalPosition ?? Vector3.Zero, 1e-4f, $"bind position bone {i} ({sourceBones[i].Name})");
        }
    }

    [Fact]
    public void SmdTranslator_ReferenceSmd_RoundTrip_PreservesMaterialName()
    {
        Scene	source	= CreateSingleTriangleScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "mesh.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "mesh.smd");

        Mesh[] loadedMeshes = loaded.GetDescendants<Mesh>();
        Assert.Single(loadedMeshes);
        Assert.NotNull(loadedMeshes[0].Materials);
        Assert.Contains(loadedMeshes[0].Materials!, m => m.Name == "lambert1");
    }

    [Fact]
    public void SmdTranslator_ReferenceSmd_RoundTrip_FaceCountPreserved()
    {
        Scene	source	= CreateSingleTriangleScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "mesh.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "mesh.smd");

        Mesh[] sourceMeshes= source.GetDescendants<Mesh>();
        Mesh[] loadedMeshes= loaded.GetDescendants<Mesh>();

        Assert.Equal(sourceMeshes[0].FaceCount,		loadedMeshes[0].FaceCount);
        Assert.Equal(sourceMeshes[0].VertexCount, loadedMeshes[0].VertexCount);
    }

    [Fact]
    public void SmdTranslator_ReferenceSmd_RoundTrip_SkinnedBoneCountPreserved()
    {
        Scene	source	= CreateSkinnedMeshScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "skinned.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "skinned.smd");

        Mesh[] sourceMeshes= source.GetDescendants<Mesh>();
        Mesh[] loadedMeshes= loaded.GetDescendants<Mesh>();

        Assert.NotNull(loadedMeshes[0].Skin!.BoneIndices);
        Assert.NotNull(loadedMeshes[0].Skin!.BoneWeights);
        Assert.Equal(sourceMeshes[0].Skin!.BoneIndices!.ValueCount, loadedMeshes[0].Skin!.BoneIndices!.ValueCount);
    }

    [Fact]
    public void SmdTranslator_AnimationSmd_RoundTrip_PreservesTrackCount()
    {
        Scene	source	= CreateAnimationOnlyScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "anim.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "anim.smd");

        SkeletonAnimation sourceAnim= source.FirstOfType<SkeletonAnimation>();
        SkeletonAnimation loadedAnim= loaded.FirstOfType<SkeletonAnimation>();

        Assert.Equal(sourceAnim.Tracks.Count, loadedAnim.Tracks.Count);
    }

    [Fact]
    public void SmdTranslator_AnimationSmd_RoundTrip_PreservesTrackBoneNames()
    {
        Scene	source	= CreateAnimationOnlyScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "anim.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "anim.smd");

        SkeletonAnimation sourceAnim= source.FirstOfType<SkeletonAnimation>();
        SkeletonAnimation loadedAnim= loaded.FirstOfType<SkeletonAnimation>();

        for (int i = 0; i < sourceAnim.Tracks.Count; i++)
            Assert.Equal(sourceAnim.Tracks[i].Name, loadedAnim.Tracks[i].Name);
    }

    [Fact]
    public void SmdTranslator_AnimationSmd_RoundTrip_PreservesKeyFramePositions()
    {
        Scene	source	= CreateAnimationOnlyScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "anim.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "anim.smd");

        SkeletonAnimation sourceAnim= source.FirstOfType<SkeletonAnimation>();
        SkeletonAnimation loadedAnim= loaded.FirstOfType<SkeletonAnimation>();

        for (int t = 0; t < sourceAnim.Tracks.Count; t++)
        {
            var sSrc= sourceAnim.Tracks[t];
            var sDst= loadedAnim.Tracks[t];

            if (sSrc.TranslationCurve is null) continue;

            Assert.NotNull(sDst.TranslationCurve);
            float[] sampleTimes = [0f, 5f, 10f];
            foreach (float time in sampleTimes)
            {
                AssertVector3Near(sSrc.TranslationCurve.SampleVector3(time), sDst.TranslationCurve!.SampleVector3(time), 1e-4f, $"track {t} ({sSrc.Name}) at t={time}");
            }
        }
    }

    [Fact]
    public void SmdTranslator_EulerRoundTrip_IdentityQuaternionRoundTripsToIdentity()
    {
        Scene	source	= CreateSkinnedMeshScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "bpose.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "bpose.smd");

        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();
        Assert.NotEmpty(loadedBones);

        foreach (var bone in loadedBones)
        {
            Quaternion rot	= Quaternion.Normalize(bone.BindTransform.LocalRotation ?? Quaternion.Identity);
            float dot		= MathF.Abs(rot.X * 0f + rot.Y * 0f + rot.Z * 0f + rot.W * 1f);
            Assert.True(dot > 0.999f, $"Bone '{bone.Name}': rotation not close to Identity ({rot}).");
        }
    }

    [Fact]
    public void SmdTranslator_WriteThenRead_SkeletonHierarchy_ParentChildPreserved()
    {
        Scene	source	= CreateSkinnedMeshScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "hierarchy.smd");
        Scene	loaded		= ReadScene(manager, smdBytes, "hierarchy.smd");

        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();

        SkeletonBone? bone1 = loadedBones.FirstOrDefault(b => b.Name is "bone1");
        Assert.NotNull(bone1);
        Assert.IsType<SkeletonBone>(bone1.Parent);
    }

    [Fact]
    public void SmdTranslator_Write_OutputStartsWithVersion1()
    {
        Scene	source	= CreateSingleTriangleScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "mesh.smd");
        string	smdText		= Encoding.ASCII.GetString(smdBytes);

        Assert.StartsWith("version 1", smdText.TrimStart());
    }

    [Fact]
    public void SmdTranslator_Write_ContainsRequiredSections()
    {
        Scene	source	= CreateSingleTriangleScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "mesh.smd");
        string	smdText		= Encoding.ASCII.GetString(smdBytes);

        Assert.Contains("nodes",		smdText);
        Assert.Contains("skeleton",		smdText);
        Assert.Contains("triangles", smdText);
    }

    [Fact]
    public void SmdTranslator_Write_AnimationContainsMultipleTimeBlocks()
    {
        Scene	source	= CreateAnimationOnlyScene();
        var		manager	= CreateManager();

        byte[]	smdBytes	= WriteScene(manager, source, "anim.smd");
        string	smdText		= Encoding.ASCII.GetString(smdBytes);

        int timeCount = CountOccurrences(smdText, "  time ");
        Assert.True(timeCount > 1, "Animation SMD must contain more than one 'time' block.");
    }

    [Fact]
    public void SmdTranslator_Read_EmptyStream_DoesNotThrow()
    {
        var			translator	= new SmdTranslator();
        using var	ms			= new MemoryStream(0);
        var			scene		= new Scene("empty");
        translator.Read(scene, ms, new SceneTranslationContext("test", new SceneTranslatorOptions()), null);
        int boneCount	= scene.GetDescendants<SkeletonBone>().Length;
        Assert.Equal(0, boneCount);
    }

    private static Scene CreateSingleTriangleScene()
    {
        Scene scene		= new("TestScene");
        MeshGroup model	= scene.RootNode.AddNode<MeshGroup>("TestModel");

        Skeleton skel	= scene.RootNode.AddNode(new Skeleton("TestSkeleton"));
        var root		= skel.AddNode(new SkeletonBone("root"));
        root.BindTransform.LocalPosition	= Vector3.Zero;
        root.BindTransform.LocalRotation	= Quaternion.Identity;

        Mesh mesh = model.AddNode<Mesh>("lambert1");
        mesh.Positions		= MakeFloat3Buffer([new Vector3(0f,	0f, 0f), new Vector3(1f,	0f, 0f), new Vector3(0f,	1f, 0f),]);
        mesh.Normals		= MakeFloat3Buffer([Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,]);
        mesh.UVLayers		= MakeFloat2Buffer([new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f),]);
        mesh.FaceIndices	= MakeIntBuffer([0, 1, 2]);

        var mat = scene.RootNode.AddNode<Material>("lambert1");
        mesh.Materials = [mat];

        return scene;
    }

    private static Scene CreateSkinnedMeshScene()
    {
        Scene scene = new("SkinnedScene");

        Skeleton skel	= scene.RootNode.AddNode(new Skeleton("Skeleton"));
        var root		= skel.AddNode(new SkeletonBone("root"));
        root.BindTransform.LocalPosition	= Vector3.Zero;
        root.BindTransform.LocalRotation	= Quaternion.Identity;

        var bone1 = root.AddNode(new SkeletonBone("bone1"));
        bone1.BindTransform.LocalPosition	= new Vector3(0f, 1f, 0f);
        bone1.BindTransform.LocalRotation	= Quaternion.Identity;

        MeshGroup	model	= scene.RootNode.AddNode<MeshGroup>("SkinnedModel");
        Mesh		mesh	= model.AddNode<Mesh>("skin_mesh");

        mesh.Positions	= MakeFloat3Buffer([new Vector3(0f,	0f, 0f), new Vector3(1f,	0f, 0f), new Vector3(0f,	1f, 0f),]);
        mesh.Normals	= MakeFloat3Buffer([Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,]);
        mesh.UVLayers	= MakeFloat2Buffer([new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f),]);

        var boneIndices= new DataBuffer<int>(3, 2, 1);
        var boneWeights= new DataBuffer<float>(3, 2, 1);
        for (int v = 0; v < 3; v++)
        {
            var boneIndexElement	= boneIndices.Add();
            var boneWeightElement	= boneWeights.Add();
            boneIndexElement.Add(0); boneWeightElement.Add(0.6f);
            boneIndexElement.Add(1); boneWeightElement.Add(0.4f);
        }
        DataBuffer indices= boneIndices;
        DataBuffer weights= boneWeights;
        mesh.FaceIndices	= MakeIntBuffer([0, 1, 2]);
        mesh.Skin			= new Skin([root, bone1], indices, weights);

        var mat = model.AddNode<Material>("skin_mat");
        mesh.Materials = [mat];

        return scene;
    }

    private static Scene CreateAnimationOnlyScene()
    {
        Scene		scene	= new("AnimScene");
        Skeleton	skel	= scene.RootNode.AddNode(new Skeleton("Skeleton"));
        var			root	= skel.AddNode(new SkeletonBone("root"));
        var			bone1	= root.AddNode(new SkeletonBone("bone1"));
        root.BindTransform.LocalPosition	= Vector3.Zero;
        bone1.BindTransform.LocalPosition	= new Vector3(0f, 2f, 0f);

        var anim = scene.RootNode.AddNode(new SkeletonAnimation("TestAnim", 2, TransformType.Absolute)
        {
            Framerate		= 30f,
            TransformType	= TransformType.Absolute,
        });

        var	trackRoot	= new SkeletonAnimationTrack("root")		{ TransformType = TransformType.Absolute };
        var	trackBone1	= new SkeletonAnimationTrack("bone1") { TransformType = TransformType.Absolute };

        trackRoot.AddTranslationFrame(0f,	Vector3.Zero);
        trackRoot.AddTranslationFrame(5f,	new Vector3(1f, 0f, 0f));
        trackRoot.AddTranslationFrame(10f, new Vector3(2f, 0f, 0f));

        trackRoot.AddRotationFrame(0f,	Quaternion.Identity);
        trackRoot.AddRotationFrame(10f, Quaternion.Normalize(new Quaternion(0f, 0.70710677f, 0f, 0.70710677f)));

        trackBone1.AddTranslationFrame(0f,	new Vector3(0f, 2f, 0f));
        trackBone1.AddTranslationFrame(10f, new Vector3(0f, 2f, 0f));

        anim.Tracks.Add(trackRoot);
        anim.Tracks.Add(trackBone1);

        return scene;
    }

    private static DataBuffer<float> MakeFloat3Buffer(IReadOnlyList<Vector3> vectors)
    {
        var buf = new DataBuffer<float>(vectors.Count, 1, 3);
        for (int i = 0; i < vectors.Count; i++)
            buf.Add(vectors[i]);
        return buf;
    }

    private static DataBuffer<float> MakeFloat2Buffer(IReadOnlyList<Vector2> vectors)
    {
        var buf = new DataBuffer<float>(vectors.Count, 1, 2);
        for (int i = 0; i < vectors.Count; i++)
            buf.Add(vectors[i]);
        return buf;
    }

    private static DataBuffer<int> MakeIntBuffer(IReadOnlyList<int> values)
    {
        var buf = new DataBuffer<int>(values.Count, 1, 1);
        for (int i = 0; i < values.Count; i++)
            buf.Add(values[i]);
        return buf;
    }

    private static SceneTranslatorManager CreateManager()
    {
        var mgr = new SceneTranslatorManager();
        mgr.Register(new SmdTranslator());
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

    private static void AssertVector3Near(Vector3 expected, Vector3 actual, float tolerance, string label)
    {
        Assert.True(MathF.Abs(expected.X - actual.X) <= tolerance && MathF.Abs(expected.Y - actual.Y) <= tolerance && MathF.Abs(expected.Z - actual.Z) <= tolerance, $"{label}: expected ({expected.X},{expected.Y},{expected.Z}) got ({actual.X},{actual.Y},{actual.Z})");
    }

    private static void AssertVector2Near(Vector2 expected, Vector2 actual, float tolerance, string label)
    {
        Assert.True(MathF.Abs(expected.X - actual.X) <= tolerance && MathF.Abs(expected.Y - actual.Y) <= tolerance, $"{label}: expected ({expected.X},{expected.Y}) got ({actual.X},{actual.Y})");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int	count	= 0;
        int	idx		= 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
