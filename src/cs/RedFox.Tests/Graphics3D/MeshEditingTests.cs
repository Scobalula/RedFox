using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Tests.Graphics3D;

public sealed class MeshEditingTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void ValidateStructure_ValidMesh_ReturnsTrue()
    {
        Group root = new("Root");
        SkeletonBone bone = root.AddNode(new SkeletonBone("Bone"));
        Mesh mesh = root.AddNode(CreateSplitQuad("Quad"));
        mesh.Skin = CreateSkin(bone, mesh.VertexCount);

        StringBuilder messages = new();

        Assert.True(MeshValidation.ValidateStructure(mesh, messages), messages.ToString());
    }

    [Fact]
    public void ValidateStructure_ReportsIndexAndAttributeProblems()
    {
        Mesh mesh = CreateSplitQuad("Quad");
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 9, 3, 4], 1, 1);
        mesh.Normals = new DataBuffer<float>([0f, 0f, 1f], 1, 3);

        StringBuilder messages = new();

        Assert.False(MeshValidation.ValidateStructure(mesh, messages));
        Assert.Contains("not a multiple of 3", messages.ToString());
        Assert.Contains("outside [0, 6)", messages.ToString());
        Assert.Contains("Normals has 1 elements", messages.ToString());
    }

    [Fact]
    public void ValidateStructure_ReportsSkinProblems()
    {
        Group root = new("Root");
        SkeletonBone bone = root.AddNode(new SkeletonBone("Bone"));
        Mesh mesh = root.AddNode(CreateSplitQuad("Quad"));
        mesh.Skin = new Skin([bone, new SkeletonBone("Detached")], new DataBuffer<int>([0, 0, 0, 0, 0, 5], 1, 1), new DataBuffer<float>([1f, 1f, 1f, 1f, 0.5f, 1f], 1, 1));

        StringBuilder messages = new();

        Assert.False(MeshValidation.ValidateStructure(mesh, messages));
        Assert.Contains("'Detached', which is not in the mesh's hierarchy", messages.ToString());
        Assert.Contains("reference a bone index outside", messages.ToString());
        Assert.Contains("do not sum to 1", messages.ToString());
    }

    [Fact]
    public void ReverseWinding_SwapsSecondAndThirdIndexOfEachTriangle()
    {
        Mesh mesh = CreateSplitQuad("Quad");

        mesh.ReverseWinding();

        Assert.Equal([0, 2, 1, 3, 5, 4], GetIndices(mesh));
    }

    [Fact]
    public void Weld_MergesIdenticalVerticesAndRemapsFaces()
    {
        Mesh mesh = CreateSplitQuad("Quad");

        int removed = MeshWelder.Weld(mesh, 0f);

        Assert.Equal(2, removed);
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(4, mesh.UVLayers!.ElementCount);
        Assert.Equal([0, 1, 2, 0, 2, 3], GetIndices(mesh));
        Assert.True(MeshValidation.ValidateStructure(mesh));
    }

    [Fact]
    public void Weld_KeepsVerticesWhoseAttributesDiffer()
    {
        Mesh mesh = CreateSplitQuad("Quad");
        mesh.UVLayers!.Set(3, 0, 0, 0.5f);

        int removed = MeshWelder.Weld(mesh, 1e-3f);

        Assert.Equal(1, removed);
        Assert.Equal([0, 1, 2, 3, 2, 4], GetIndices(mesh));
    }

    [Fact]
    public void Weld_RespectsPositionTolerance()
    {
        Assert.Equal(1, MeshWelder.Weld(CreateSplitQuadWithOffset(0.0005f), 0f));
        Assert.Equal(2, MeshWelder.Weld(CreateSplitQuadWithOffset(0.0005f), 1e-3f, 0f));
    }

    [Fact]
    public void Weld_DoesNotMergeVerticesBoundToDifferentBones()
    {
        SkeletonBone first = new("First");
        SkeletonBone second = new("Second");
        Mesh mesh = CreateSplitQuad("Quad");
        mesh.Skin = new Skin([first, second], new DataBuffer<int>([0, 0, 0, 1, 0, 0], 1, 1), new DataBuffer<float>([1f, 1f, 1f, 1f, 1f, 1f], 1, 1));

        int removed = MeshWelder.Weld(mesh, 0f);

        Assert.Equal(1, removed);
        Assert.Equal(5, mesh.Skin.BoneIndices.ElementCount);
        Assert.Equal(1, mesh.Skin.BoneIndices.Get<int>(3, 0, 0));
    }

    [Fact]
    public void Combine_ExpressesGeometryInFirstMeshSpaceAndOffsetsFaces()
    {
        Material material = new("Material");
        Mesh first = CreateSplitQuad("First");
        first.BindTransform.LocalPosition = new Vector3(1f, 0f, 0f);
        first.Materials = [material];
        Mesh second = CreateSplitQuad("Second");
        second.BindTransform.LocalPosition = new Vector3(10f, 0f, 0f);
        second.Materials = [material];

        Mesh combined = MeshCombiner.Combine([first, second]);

        Assert.Equal(12, combined.VertexCount);
        Assert.Same(material, Assert.Single(combined.Materials!));
        AssertApproximately(new Vector3(9f, 0f, 0f), combined.GetVertexPosition(6, raw: true));
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], GetIndices(combined));
        AssertApproximately(new Vector3(1f, 0f, 0f), combined.GetBindWorldPosition());
    }

    [Fact]
    public void Combine_MergesSkinsSoPosedVerticesAreUnchanged()
    {
        Group root = new("Root");
        SkeletonBone bone = root.AddNode(new SkeletonBone("Bone"));
        bone.BindTransform.LocalPosition = new Vector3(0f, 2f, 0f);
        Mesh first = root.AddNode(CreateSplitQuad("First"));
        first.Skin = CreateSkin(bone, first.VertexCount);
        Mesh second = root.AddNode(CreateSplitQuad("Second"));
        second.BindTransform.LocalPosition = new Vector3(10f, 0f, 0f);
        second.Skin = CreateSkin(bone, second.VertexCount);
        bone.LiveTransform.LocalPosition = new Vector3(0f, 5f, 0f);

        Mesh combined = MeshCombiner.Combine([first, second]);

        Assert.Same(bone, Assert.Single(combined.Skin!.Bones));
        AssertApproximately(first.GetVertexPosition(1), combined.GetVertexPosition(1));
        AssertApproximately(second.GetVertexPosition(1), combined.GetVertexPosition(7));
    }

    [Fact]
    public void Combine_WithDifferentMaterials_Throws()
    {
        Mesh first = CreateSplitQuad("First");
        first.Materials = [new Material("A")];
        Mesh second = CreateSplitQuad("Second");
        second.Materials = [new Material("B")];

        Assert.Throws<ArgumentException>(() => MeshCombiner.Combine([first, second]));
    }

    [Fact]
    public void CombineByMaterial_ProducesOneMeshPerMaterial()
    {
        Material a = new("A");
        Material b = new("B");
        Mesh first = CreateSplitQuad("First");
        first.Materials = [a];
        Mesh second = CreateSplitQuad("Second");
        second.Materials = [b];
        Mesh third = CreateSplitQuad("Third");
        third.Materials = [a];

        IReadOnlyList<Mesh> combined = MeshCombiner.CombineByMaterial([first, second, third]);

        Assert.Equal(2, combined.Count);
        Assert.Equal(12, combined[0].VertexCount);
        Assert.Same(a, combined[0].Materials![0]);
        Assert.Equal(6, combined[1].VertexCount);
        Assert.Same(b, combined[1].Materials![0]);
    }

    [Fact]
    public void MeshRemap_ReorderFaces_KeepsIntegerIndices()
    {
        Mesh mesh = CreateSplitQuad("Quad");

        MeshRemap.ReorderFaces(mesh, [1, 0]);

        Assert.IsType<DataBuffer<int>>(mesh.FaceIndices);
        Assert.Equal([3, 4, 5, 0, 1, 2], GetIndices(mesh));
    }

    private static Mesh CreateSplitQuadWithOffset(float offset)
    {
        Mesh mesh = CreateSplitQuad("Quad");
        mesh.Positions!.Set(3, 0, 0, offset);
        return mesh;
    }

    private static Mesh CreateSplitQuad(string name)
    {
        Mesh mesh = new() { Name = name };
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.Normals = new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f], 1, 3);
        mesh.UVLayers = new DataBuffer<float>([0f, 0f, 1f, 0f, 1f, 1f, 0f, 0f, 1f, 1f, 0f, 1f], 1, 2);
        mesh.FaceIndices = new DataBuffer<ushort>(new ushort[] { 0, 1, 2, 3, 4, 5 }, 1, 1);
        return mesh;
    }

    private static Skin CreateSkin(SkeletonBone bone, int vertexCount) => new([bone], new DataBuffer<int>(new int[vertexCount], 1, 1), new DataBuffer<float>(Enumerable.Repeat(1f, vertexCount).ToArray(), 1, 1));

    private static int[] GetIndices(Mesh mesh) => mesh.FaceIndices!.ToArray<int>();

    private static void AssertApproximately(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}");
}
