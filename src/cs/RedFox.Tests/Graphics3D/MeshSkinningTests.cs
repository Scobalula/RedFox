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

namespace RedFox.Tests.Graphics3D;

public sealed class MeshSkinningTests
{
    private const float Tolerance = 1e-4f;

    private static (Mesh Mesh, SkeletonBone Bone) CreateSkinnedMesh()
    {
        SkeletonBone bone = new("Bone");
        bone.BindTransform.LocalPosition = new Vector3(5f, 0f, 0f);

        Vector3 diagonal = Vector3.Normalize(new Vector3(1f, 1f, 0f));

        Mesh mesh = new() { Name = "Skinned" };
        mesh.Positions = new DataBuffer<float>([6f, 0f, 0f], 1, 3);
        mesh.Normals = new DataBuffer<float>([diagonal.X, diagonal.Y, 0f], 1, 3);
        mesh.Tangents = new DataBuffer<float>([diagonal.X, diagonal.Y, 0f, -1f], 1, 4);
        mesh.Skin = new Skin([bone], new DataBuffer<ushort>(new ushort[] { 0 }, 1, 1), new DataBuffer<float>([1f], 1, 1));

        return (mesh, bone);
    }

    private static Skin CreateTwoBoneSkin(SkeletonBone first, SkeletonBone second) => new([first, second], new DataBuffer<int>([0, 1, 1, 0], 2, 1), new DataBuffer<float>([0.25f, 0.75f, 1f, 0f], 2, 1));

    private static void AssertApproximately(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}");

    [Fact]
    public void Skin_WithoutExplicitMatrices_DerivesFromCurrentBindPose()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();

        Assert.Null(mesh.Skin!.InverseBindMatrices);
        Assert.Equal(Matrix4x4.CreateTranslation(-5f, 0f, 0f), mesh.Skin.GetInverseBindMatrix(0, mesh.GetBindWorldMatrix()));

        bone.BindTransform.LocalPosition = new Vector3(8f, 0f, 0f);

        Assert.Equal(Matrix4x4.CreateTranslation(-8f, 0f, 0f), mesh.Skin.GetInverseBindMatrix(0, mesh.GetBindWorldMatrix()));
    }

    [Fact]
    public void Skin_WithExplicitMatrices_IgnoresBindPoseChanges()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();
        mesh.Skin!.InverseBindMatrices = [Matrix4x4.CreateTranslation(-5f, 0f, 0f)];

        bone.BindTransform.LocalPosition = new Vector3(8f, 0f, 0f);

        Assert.Equal(Matrix4x4.CreateTranslation(-5f, 0f, 0f), mesh.Skin.GetInverseBindMatrix(0, mesh.GetBindWorldMatrix()));
    }

    [Fact]
    public void Skin_WithMismatchedMatrixCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Skin([new SkeletonBone("A"), new SkeletonBone("B")], new DataBuffer<int>(1, 1, 1), new DataBuffer<float>(1, 1, 1), [Matrix4x4.Identity]));
    }

    [Fact]
    public void CaptureInverseBindMatrices_FreezesCurrentBindPose()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();

        mesh.Skin!.CaptureInverseBindMatrices(mesh.GetBindWorldMatrix());
        bone.BindTransform.LocalPosition = new Vector3(8f, 0f, 0f);

        Assert.Equal([Matrix4x4.CreateTranslation(-5f, 0f, 0f)], mesh.Skin.InverseBindMatrices!);
    }

    [Fact]
    public void AddBone_ReturnsExistingIndexOrAppends()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();
        SkeletonBone other = new("Other");

        Assert.Equal(0, mesh.Skin!.AddBone(bone));
        Assert.Equal(1, mesh.Skin.AddBone(other));
        Assert.Equal([bone, other], mesh.Skin.Bones);
    }

    [Fact]
    public void AddBone_RequiresMatrixOnlyWhenExplicit()
    {
        (Mesh mesh, _) = CreateSkinnedMesh();

        Assert.Throws<InvalidOperationException>(() => mesh.Skin!.AddBone(new SkeletonBone("Other"), Matrix4x4.Identity));

        mesh.Skin!.CaptureInverseBindMatrices(mesh.GetBindWorldMatrix());

        Assert.Throws<InvalidOperationException>(() => mesh.Skin.AddBone(new SkeletonBone("Other")));
        Assert.Equal(1, mesh.Skin.AddBone(new SkeletonBone("Other"), Matrix4x4.Identity));
        Assert.Equal(2, mesh.Skin.InverseBindMatrices!.Count);
    }

    [Fact]
    public void RemoveBoneAt_RenumbersIndicesAndRenormalizesWeights()
    {
        SkeletonBone first = new("First");
        SkeletonBone second = new("Second");
        Skin skin = CreateTwoBoneSkin(first, second);

        skin.RemoveBoneAt(0);

        Assert.Equal([second], skin.Bones);
        Assert.Equal(0, skin.BoneIndices.Get<int>(0, 1, 0));
        Assert.Equal(1f, skin.BoneWeights.Get<float>(0, 1, 0));
        Assert.Equal(0f, skin.BoneWeights.Get<float>(0, 0, 0));
        Assert.Equal(1f, skin.BoneWeights.Get<float>(1, 0, 0));
    }

    [Fact]
    public void RemoveBone_DoesNotMutateSharedBuffers()
    {
        SkeletonBone first = new("First");
        SkeletonBone second = new("Second");
        Skin skin = CreateTwoBoneSkin(first, second);
        DataBuffer originalWeights = skin.BoneWeights;

        Assert.True(skin.RemoveBone(first));
        Assert.False(skin.RemoveBone(first));
        Assert.Equal(0.25f, originalWeights.Get<float>(0, 0, 0));
    }

    [Fact]
    public void RemapBones_ReplacesReferencesOnly()
    {
        SkeletonBone first = new("First");
        SkeletonBone second = new("Second");
        SkeletonBone replacement = new("Replacement");
        Skin skin = CreateTwoBoneSkin(first, second);

        Assert.Equal(1, skin.RemapBones(new Dictionary<SkeletonBone, SkeletonBone> { [second] = replacement }));
        Assert.Equal([first, replacement], skin.Bones);
        Assert.Equal(1, skin.BoneIndices.Get<int>(0, 1, 0));
    }

    [Fact]
    public void GetVertexPosition_AppliesLivePose()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();
        bone.LiveTransform.LocalPosition = new Vector3(5f, 0f, 2f);

        AssertApproximately(new Vector3(6f, 0f, 2f), mesh.GetVertexPosition(0));
        AssertApproximately(new Vector3(6f, 0f, 0f), mesh.GetVertexPosition(0, raw: true));
    }

    [Fact]
    public void GetVertexDirections_UnderNonUniformScale_UseCorrectTransforms()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();
        bone.LiveTransform.Scale = new Vector3(2f, 1f, 1f);

        Vector4 tangent = mesh.GetVertexTangent(0);

        AssertApproximately(Vector3.Normalize(new Vector3(0.5f, 1f, 0f)), mesh.GetVertexNormal(0));
        AssertApproximately(Vector3.Normalize(new Vector3(2f, 1f, 0f)), tangent.AsVector3());
        Assert.Equal(-1f, tangent.W);
    }

    [Fact]
    public void TryGetSceneBounds_FollowsLivePose()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();
        bone.LiveTransform.LocalPosition = new Vector3(5f, 3f, 0f);

        Assert.True(mesh.TryGetSceneBounds(out SceneBounds bounds));
        AssertApproximately(new Vector3(6f, 3f, 0f), bounds.Min);
        AssertApproximately(new Vector3(6f, 3f, 0f), bounds.Max);
    }

    [Fact]
    public void Clone_CopiesSkinIndependently()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();
        Mesh clone = (Mesh)mesh.Clone();

        clone.Skin!.AddBone(new SkeletonBone("Other"));

        Assert.NotSame(mesh.Skin, clone.Skin);
        Assert.Equal([bone], mesh.Skin!.Bones);
    }

    [Fact]
    public void BakeCurrentPoseToVertices_WritesPoseAndClearsSkin()
    {
        (Mesh mesh, SkeletonBone bone) = CreateSkinnedMesh();
        bone.LiveTransform.LocalPosition = new Vector3(5f, 0f, 2f);

        mesh.BakeCurrentPoseToVertices();

        Assert.Null(mesh.Skin);
        AssertApproximately(new Vector3(6f, 0f, 2f), mesh.GetVertexPosition(0));
        Assert.Equal(-1f, mesh.GetVertexTangent(0).W);
    }
}
