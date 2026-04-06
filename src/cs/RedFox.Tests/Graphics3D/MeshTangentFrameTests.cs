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

public sealed class MeshTangentFrameTests
{
    private const float Eps = 1e-4f;

    // Triangle in XY plane, UV mapped.
    // Positions: (0,0,0),(1,0,0),(0,1,0)  UVs: (0,0),(1,0),(0,1)
    // Expected tangent: +X (U increases along X axis).
    private static Mesh CreateUVTriangle()
    {
        Mesh mesh = new() { Name = "uvtri" };
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.UVLayers = new DataBuffer<float>(
        [
            0f, 0f,
            1f, 0f,
            0f, 1f,
        ], 1, 2);
        mesh.Normals = new DataBuffer<float>(
        [
            0f, 0f, 1f,
            0f, 0f, 1f,
            0f, 0f, 1f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 2f], 1, 1);
        return mesh;
    }

    [Fact]
    public void Generate_ProducesTangentsAlignedWithUDirection()
    {
        Mesh mesh = CreateUVTriangle();
        MeshTangentFrame.Generate(mesh);

        Assert.NotNull(mesh.Tangents);
        // Each tangent should be approximately +X (component count = 4 including handedness sign).
        Assert.Equal(4, mesh.Tangents.ComponentCount);

        for (int i = 0; i < 3; i++)
        {
            Vector4 t = mesh.Tangents.GetVector4(i, 0);
            Assert.True(MathF.Abs(t.X - 1f) < Eps, $"Vertex {i}: tangent X should be ≈ 1, got {t.X}");
            Assert.True(MathF.Abs(t.Y) < Eps,       $"Vertex {i}: tangent Y should be ≈ 0, got {t.Y}");
            Assert.True(MathF.Abs(t.Z) < Eps,       $"Vertex {i}: tangent Z should be ≈ 0, got {t.Z}");
        }
    }

    [Fact]
    public void Generate_TangentIsPerpendicularToNormal()
    {
        Mesh mesh = CreateUVTriangle();
        MeshTangentFrame.Generate(mesh);

        Assert.NotNull(mesh.Tangents);
        Assert.NotNull(mesh.Normals);

        for (int i = 0; i < 3; i++)
        {
            Vector4 t4 = mesh.Tangents.GetVector4(i, 0);
            Vector3 t  = new(t4.X, t4.Y, t4.Z);
            Vector3 n  = mesh.Normals.GetVector3(i, 0);
            float dot  = Vector3.Dot(Vector3.Normalize(t), Vector3.Normalize(n));
            Assert.True(MathF.Abs(dot) < Eps, $"Vertex {i}: tangent dot normal = {dot}, expected ≈ 0");
        }
    }

    [Fact]
    public void GenerateCompact_ProducesVector3Tangents()
    {
        Mesh mesh = CreateUVTriangle();
        MeshTangentFrame.GenerateCompact(mesh);

        Assert.NotNull(mesh.Tangents);
        Assert.Equal(3, mesh.Tangents.ComponentCount);
    }

    [Fact]
    public void Generate_WithBiTangents_PopulatesBiTangentBuffer()
    {
        Mesh mesh = CreateUVTriangle();
        MeshTangentFrame.Generate(mesh, storeBiTangents: true);

        Assert.NotNull(mesh.Tangents);
        Assert.NotNull(mesh.BiTangents);
        Assert.Equal(3, mesh.BiTangents!.ComponentCount);
    }

    [Fact]
    public void Generate_WithoutBiTangents_LeavesBiTangentBufferNull()
    {
        Mesh mesh = CreateUVTriangle();
        mesh.BiTangents = null;
        MeshTangentFrame.Generate(mesh);

        Assert.Null(mesh.BiTangents);
    }

    [Fact]
    public void Generate_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshTangentFrame.Generate(null!));
    }
}
