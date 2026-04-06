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

public sealed class MeshOptimizerTests
{
    // ---- SortFacesByAttribute ------------------------------------------------

    [Fact]
    public void SortFacesByAttribute_SortsByAttribute_CorrectOrder()
    {
        uint[] attrs = [3u, 1u, 2u, 1u];
        int[]  remap = MeshOptimizer.SortFacesByAttribute(attrs);

        Assert.Equal([1u, 1u, 2u, 3u], attrs);
        Assert.Equal(4,  remap.Length);
        // The two attr=1 faces should come first (original indices 1 and 3).
        Assert.Contains(1, remap[..2]);
        Assert.Contains(3, remap[..2]);
        // attr=2 face (original index 2) at position 2.
        Assert.Equal(2, remap[2]);
        // attr=3 face (original index 0) at position 3.
        Assert.Equal(0, remap[3]);
    }

    [Fact]
    public void SortFacesByAttribute_AlreadySorted_RemapIsIdentity()
    {
        uint[] attrs = [1u, 2u, 3u];
        int[]  remap = MeshOptimizer.SortFacesByAttribute(attrs);

        Assert.Equal([0, 1, 2], remap);
    }

    [Fact]
    public void SortFacesByAttribute_NullAttributes_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshOptimizer.SortFacesByAttribute(null!));
    }

    // ---- OptimizeVertexOrder -------------------------------------------------

    private static Mesh CreateTwoFaceMesh()
    {
        Mesh mesh = new() { Name = "two_faces" };
        // Positions: v0..v3
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
            1f, 1f, 0f,
        ], 1, 3);
        // Face 0: v2,v0,v1  — first use order: v2,v0,v1
        // Face 1: v3,v2,v1  — first use order: v3 (v2,v1 already seen)
        mesh.FaceIndices = new DataBuffer<float>([2f, 0f, 1f, 3f, 2f, 1f], 1, 1);
        return mesh;
    }

    [Fact]
    public void OptimizeVertexOrder_ProducesFirstUseOrdering()
    {
        Mesh mesh       = CreateTwoFaceMesh();
        int[] remap     = MeshOptimizer.OptimizeVertexOrder(mesh);

        // remap[newIndex] = originalIndex.
        // New vertex indices in first-use order: remap[0]=2, remap[1]=0, remap[2]=1, remap[3]=3
        Assert.Equal(4, remap.Length);
        Assert.Equal(2, remap[0]);
        Assert.Equal(0, remap[1]);
        Assert.Equal(1, remap[2]);
        Assert.Equal(3, remap[3]);
    }

    [Fact]
    public void OptimizeVertexOrder_ReturnsUnusedCount()
    {
        Mesh mesh        = new() { Name = "unused_verts" };
        // 5 positions, only 3 referenced.
        mesh.Positions   = new DataBuffer<float>(new float[5 * 3], 1, 3);
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 2f], 1, 1);
        int[] remap      = MeshOptimizer.OptimizeVertexOrder(mesh, out int unused);

        Assert.Equal(2, unused);
    }

    [Fact]
    public void OptimizeVertexOrder_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshOptimizer.OptimizeVertexOrder(null!));
    }

    // ---- OptimizeFacesLRU ----------------------------------------------------

    private static Mesh CreateLinearStripMesh(int faceCount)
    {
        // Creates a row of triangles each sharing an edge with the next.
        // Vertex layout: v[i], v[i+1], v[i+2] for face i.
        int vertexCount = faceCount + 2;
        float[] positions = new float[vertexCount * 3];
        for (int i = 0; i < vertexCount; i++) positions[i * 3] = i;

        float[] indices = new float[faceCount * 3];
        for (int f = 0; f < faceCount; f++)
        {
            indices[f * 3]     = f;
            indices[f * 3 + 1] = f + 1;
            indices[f * 3 + 2] = f + 2;
        }

        Mesh mesh      = new() { Name = "strip" };
        mesh.Positions = new DataBuffer<float>(positions, 1, 3);
        mesh.FaceIndices = new DataBuffer<float>(indices, 1, 1);
        return mesh;
    }

    [Fact]
    public void OptimizeFacesLRU_ReturnsFaceRemapWithAllFaces()
    {
        int   faceCount = 12;
        Mesh  mesh      = CreateLinearStripMesh(faceCount);
        int[] remap     = MeshOptimizer.OptimizeFacesLRU(mesh);

        Assert.Equal(faceCount, remap.Length);
        // Every face should appear exactly once.
        Assert.Equal(faceCount, remap.Distinct().Count());
        Assert.All(remap, f => Assert.InRange(f, 0, faceCount - 1));
    }

    [Fact]
    public void OptimizeFacesLRU_EmptyMesh_ReturnsEmpty()
    {
        Mesh mesh        = new() { Name = "empty" };
        mesh.Positions   = new DataBuffer<float>(new float[0], 1, 3);
        mesh.FaceIndices = new DataBuffer<float>(new float[0], 1, 1);
        int[] remap      = MeshOptimizer.OptimizeFacesLRU(mesh);

        Assert.Empty(remap);
    }

    [Fact]
    public void OptimizeFacesLRU_InvalidCacheSize_Throws()
    {
        Mesh mesh        = CreateLinearStripMesh(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptimizer.OptimizeFacesLRU(mesh, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptimizer.OptimizeFacesLRU(mesh, 65));
    }

    [Fact]
    public void OptimizeFacesLRU_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshOptimizer.OptimizeFacesLRU(null!));
    }
}
