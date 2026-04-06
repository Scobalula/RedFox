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

public sealed class MeshAdjacencyTests
{
    // Two triangles sharing the edge v1-v2.
    // Tri 0: v0,v1,v2   Tri 1: v1,v3,v2
    // Shared edge: v1-v2 (CCW of tri0) == v1-v2 (CW of tri1 from tri0's perspective)
    private static Mesh CreateTwoTriangleMesh()
    {
        Mesh mesh = new() { Name = "two_tris" };
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,   // v0
            1f, 0f, 0f,   // v1
            0f, 1f, 0f,   // v2
            1f, 1f, 0f,   // v3
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<float>(
        [
            0f, 1f, 2f,   // tri 0
            1f, 3f, 2f,   // tri 1 — edge v1-v2 is shared with tri 0
        ], 1, 1);
        return mesh;
    }

    private static Mesh CreateSingleTriangleMesh()
    {
        Mesh mesh = new() { Name = "one_tri" };
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 2f], 1, 1);
        return mesh;
    }

    [Fact]
    public void GeneratePointReps_UniquePositions_AllSelfReferencing()
    {
        Mesh mesh = CreateSingleTriangleMesh();
        uint[] pr = MeshAdjacency.GeneratePointReps(mesh);

        Assert.Equal(3, pr.Length);
        Assert.Equal(0u, pr[0]);
        Assert.Equal(1u, pr[1]);
        Assert.Equal(2u, pr[2]);
    }

    [Fact]
    public void GenerateAdjacency_SingleTriangle_AllEdgesUnused()
    {
        Mesh mesh = CreateSingleTriangleMesh();
        uint[] adj = MeshAdjacency.GenerateAdjacency(mesh);

        Assert.Equal(3, adj.Length);
        Assert.Equal(MeshAdjacency.Unused, adj[0]);
        Assert.Equal(MeshAdjacency.Unused, adj[1]);
        Assert.Equal(MeshAdjacency.Unused, adj[2]);
    }

    [Fact]
    public void GenerateAdjacency_TwoSharedTriangles_FindsSharedEdge()
    {
        Mesh mesh = CreateTwoTriangleMesh();
        uint[] adj = MeshAdjacency.GenerateAdjacency(mesh);

        // 6 adjacency entries (3 per face).
        Assert.Equal(6, adj.Length);

        // At least one entry in each face should identify the other face.
        bool tri0Linked = adj[0] == 1u || adj[1] == 1u || adj[2] == 1u;
        bool tri1Linked = adj[3] == 0u || adj[4] == 0u || adj[5] == 0u;

        Assert.True(tri0Linked, "Tri 0 should have at least one adjacency to tri 1.");
        Assert.True(tri1Linked, "Tri 1 should have at least one adjacency to tri 0.");
    }

    [Fact]
    public void ConvertPointRepsToAdjacency_IsConsistentWithGenerateAdjacency()
    {
        Mesh mesh = CreateTwoTriangleMesh();
        uint[] pr  = MeshAdjacency.GeneratePointReps(mesh);
        uint[] adj1 = MeshAdjacency.GenerateAdjacency(mesh);
        uint[] adj2 = MeshAdjacency.ConvertPointRepsToAdjacency(mesh, pr);

        Assert.Equal(adj1.Length, adj2.Length);
        for (int i = 0; i < adj1.Length; i++)
            Assert.Equal(adj1[i], adj2[i]);
    }

    [Fact]
    public void GenerateAdjacency_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshAdjacency.GenerateAdjacency(null!));
    }

    [Fact]
    public void GeneratePointReps_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshAdjacency.GeneratePointReps(null!));
    }
}
