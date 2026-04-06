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

public sealed class MeshNormalsTests
{
    private const float Eps = 1e-5f;

    // Builds a single CCW right-angled triangle in the XY plane.
    // Positions: (0,0,0), (1,0,0), (0,1,0).  Expected face normal: +Z.
    private static Mesh CreateTriangle(MeshFaceOrder order = MeshFaceOrder.CounterClockwise)
    {
        Mesh mesh = new() { Name = "tri" };
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);

        if (order == MeshFaceOrder.CounterClockwise)
            mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 2f], 1, 1);
        else
            mesh.FaceIndices = new DataBuffer<float>([0f, 2f, 1f], 1, 1);

        return mesh;
    }

    [Fact]
    public void Generate_DefaultCCW_ProducesPositiveZNormal()
    {
        Mesh mesh = CreateTriangle(MeshFaceOrder.CounterClockwise);
        MeshNormals.Generate(mesh);

        Assert.NotNull(mesh.Normals);
        // All three vertices should point toward +Z.
        for (int i = 0; i < 3; i++)
        {
            Vector3 n = mesh.Normals.GetVector3(i, 0);
            Assert.True(MathF.Abs(n.X) < Eps,      $"Vertex {i}: X should be ≈ 0, got {n.X}");
            Assert.True(MathF.Abs(n.Y) < Eps,      $"Vertex {i}: Y should be ≈ 0, got {n.Y}");
            Assert.True(MathF.Abs(n.Z - 1f) < Eps, $"Vertex {i}: Z should be ≈ 1, got {n.Z}");
        }
    }

    [Fact]
    public void Generate_ClockwiseOrder_ProducesNegativeZNormal()
    {
        Mesh mesh = CreateTriangle(MeshFaceOrder.CounterClockwise);
        MeshNormals.Generate(mesh, NormalGenerationMode.WeightedByAngle, MeshFaceOrder.Clockwise);

        Assert.NotNull(mesh.Normals);
        for (int i = 0; i < 3; i++)
        {
            Vector3 n = mesh.Normals.GetVector3(i, 0);
            Assert.True(MathF.Abs(n.Z + 1f) < Eps, $"Vertex {i}: Z should be ≈ -1, got {n.Z}");
        }
    }

    [Fact]
    public void Generate_AllModes_ProduceSameDirectionForSymmetricTriangle()
    {
        var modes = new[] { NormalGenerationMode.WeightedByAngle, NormalGenerationMode.WeightedByArea, NormalGenerationMode.EqualWeight };
        Vector3[] results = new Vector3[modes.Length];

        for (int m = 0; m < modes.Length; m++)
        {
            Mesh mesh = CreateTriangle();
            MeshNormals.Generate(mesh, modes[m]);
            results[m] = mesh.Normals!.GetVector3(0, 0);
        }

        // Different modes should agree on direction for a flat triangle.
        for (int m = 1; m < results.Length; m++)
        {
            Assert.True(MathF.Abs(results[m].Z - results[0].Z) < Eps,
                $"Mode {modes[m]} Z={results[m].Z} differs from Mode {modes[0]} Z={results[0].Z}");
        }
    }

    [Fact]
    public void Generate_ReplacesExistingNormals()
    {
        Mesh mesh = CreateTriangle();
        mesh.Normals = new DataBuffer<float>([1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f], 1, 3);

        MeshNormals.Generate(mesh);

        // After regeneration the normals buffer should differ from the old data.
        Vector3 n = mesh.Normals!.GetVector3(0, 0);
        Assert.True(MathF.Abs(n.Z - 1f) < Eps, $"Z should be ≈ 1 after regeneration, got {n.Z}");
    }

    [Fact]
    public void Generate_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshNormals.Generate(null!));
    }

    [Fact]
    public void Generate_MissingPositions_Throws()
    {
        Mesh mesh = new() { Name = "no_pos" };
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 2f], 1, 1);
        Assert.Throws<InvalidOperationException>(() => MeshNormals.Generate(mesh));
    }

    [Fact]
    public void Generate_MissingFaceIndices_Throws()
    {
        Mesh mesh = new() { Name = "no_idx" };
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        Assert.Throws<InvalidOperationException>(() => MeshNormals.Generate(mesh));
    }
}
