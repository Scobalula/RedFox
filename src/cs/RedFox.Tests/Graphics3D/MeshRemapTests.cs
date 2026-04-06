// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Tests.Graphics3D;

public sealed class MeshRemapTests
{
    private static Mesh CreateTwoFaceMesh()
    {
        Mesh mesh      = new() { Name = "two_faces" };
        mesh.Positions = new DataBuffer<float>(new float[4 * 3], 1, 3);
        // Face 0: 0,1,2   Face 1: 1,3,2
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 2f, 1f, 3f, 2f], 1, 1);
        return mesh;
    }

    // ---- ReorderFaces --------------------------------------------------------

    [Fact]
    public void ReorderFaces_ReverseOrder_ProducesReorderedBuffer()
    {
        Mesh  mesh     = CreateTwoFaceMesh();
        // Swap the two faces: newFace[0] = originalFace[1], newFace[1] = originalFace[0].
        int[] faceRemap = [1, 0];

        MeshRemap.ReorderFaces(mesh, faceRemap);

        Assert.NotNull(mesh.FaceIndices);
        Assert.Equal(6, mesh.FaceIndices.ElementCount);

        // New face 0 should be original face 1 indices: 1,3,2
        Assert.Equal(1, mesh.FaceIndices.Get<int>(0, 0, 0));
        Assert.Equal(3, mesh.FaceIndices.Get<int>(1, 0, 0));
        Assert.Equal(2, mesh.FaceIndices.Get<int>(2, 0, 0));

        // New face 1 should be original face 0 indices: 0,1,2
        Assert.Equal(0, mesh.FaceIndices.Get<int>(3, 0, 0));
        Assert.Equal(1, mesh.FaceIndices.Get<int>(4, 0, 0));
        Assert.Equal(2, mesh.FaceIndices.Get<int>(5, 0, 0));
    }

    [Fact]
    public void ReorderFaces_IdentityRemap_PreservesBuffer()
    {
        Mesh  mesh     = CreateTwoFaceMesh();
        int[] origVals = [0, 1, 2, 1, 3, 2];
        MeshRemap.ReorderFaces(mesh, [0, 1]);

        for (int i = 0; i < origVals.Length; i++)
            Assert.Equal(origVals[i], mesh.FaceIndices!.Get<int>(i, 0, 0));
    }

    [Fact]
    public void ReorderFaces_WrongRemapLength_Throws()
    {
        Mesh mesh = CreateTwoFaceMesh();
        Assert.Throws<ArgumentException>(() => MeshRemap.ReorderFaces(mesh, [0]));
    }

    [Fact]
    public void ReorderFaces_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshRemap.ReorderFaces(null!, [0, 1]));
    }

    // ---- RemapVertices -------------------------------------------------------

    [Fact]
    public void RemapVertices_TranslatesIndices()
    {
        Mesh mesh = CreateTwoFaceMesh();
        // vertexRemap[newIndex] = originalIndex:  [1, 0, 3, 2] swaps v0↔v1 and v2↔v3.
        // Inverse: orig=0→new=1, orig=1→new=0, orig=2→new=3, orig=3→new=2.
        MeshRemap.RemapVertices(mesh, [1, 0, 3, 2]);

        Assert.NotNull(mesh.FaceIndices);
        // Original: [0,1,2,  1,3,2]. After remap: [1,0,3,  0,2,3].
        Assert.Equal(1, mesh.FaceIndices.Get<int>(0, 0, 0));
        Assert.Equal(0, mesh.FaceIndices.Get<int>(1, 0, 0));
        Assert.Equal(3, mesh.FaceIndices.Get<int>(2, 0, 0));
        Assert.Equal(0, mesh.FaceIndices.Get<int>(3, 0, 0));
        Assert.Equal(2, mesh.FaceIndices.Get<int>(4, 0, 0));
        Assert.Equal(3, mesh.FaceIndices.Get<int>(5, 0, 0));
    }

    [Fact]
    public void RemapVertices_IdentityRemap_PreservesIndices()
    {
        Mesh  mesh   = CreateTwoFaceMesh();
        int[] orig   = [0, 1, 2, 1, 3, 2];
        MeshRemap.RemapVertices(mesh, [0, 1, 2, 3]);

        for (int i = 0; i < orig.Length; i++)
            Assert.Equal(orig[i], mesh.FaceIndices!.Get<int>(i, 0, 0));
    }

    [Fact]
    public void RemapVertices_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshRemap.RemapVertices(null!, [0, 1, 2, 3]));
    }

    [Fact]
    public void RemapVertices_NullRemap_Throws()
    {
        Mesh mesh = CreateTwoFaceMesh();
        Assert.Throws<ArgumentNullException>(() => MeshRemap.RemapVertices(mesh, null!));
    }
}
