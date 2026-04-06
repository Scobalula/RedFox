// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Tests.Graphics3D;

public sealed class MeshValidationTests
{
    private static Mesh CreateValidTriangle()
    {
        Mesh mesh = new() { Name = "valid" };
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 2f], 1, 1);
        return mesh;
    }

    private static Mesh CreateDegenerateTriangle()
    {
        Mesh mesh = new() { Name = "degen" };
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        // Last two indices are the same — degenerate face.
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 1f], 1, 1);
        return mesh;
    }

    private static Mesh CreateOutOfRangeIndexMesh()
    {
        Mesh mesh = new() { Name = "oob" };
        mesh.Positions = new DataBuffer<float>(
        [
            0f, 0f, 0f,
            1f, 0f, 0f,
            0f, 1f, 0f,
        ], 1, 3);
        // Index 99 is past end of positions.
        mesh.FaceIndices = new DataBuffer<float>([0f, 1f, 99f], 1, 1);
        return mesh;
    }

    [Fact]
    public void Validate_ValidMesh_ReturnsTrue()
    {
        Assert.True(MeshValidation.Validate(CreateValidTriangle()));
    }

    [Fact]
    public void Validate_OutOfRangeIndex_ReturnsFalse()
    {
        bool ok = MeshValidation.Validate(CreateOutOfRangeIndexMesh(), MeshValidationFlags.None, null);
        Assert.False(ok);
    }

    [Fact]
    public void Validate_DegenerateTriangle_FailsWithDegenerateFlag()
    {
        StringBuilder sb = new();
        bool ok = MeshValidation.Validate(
            CreateDegenerateTriangle(),
            MeshValidationFlags.Degenerate,
            sb);

        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(sb.ToString()));
    }

    [Fact]
    public void Validate_DegenerateTriangle_PassesWithNoFlags()
    {
        // Without the Degenerate flag, degenerate faces are not checked.
        bool ok = MeshValidation.Validate(
            CreateDegenerateTriangle(),
            MeshValidationFlags.None,
            null);

        // Index range check still applies, but degenerate check (repeated index) does not.
        // All indices are within bounds, so it should pass.
        Assert.True(ok);
    }

    [Fact]
    public void Validate_AsymmetricAdjacency_ReportedWithFlag()
    {
        Mesh mesh        = CreateValidTriangle();
        // Manually craft inconsistent adjacency: tri 0 edge 0 claims adj = 999 (non-existent face).
        uint[] adjacency = [999u, MeshAdjacency.Unused, MeshAdjacency.Unused];
        StringBuilder sb  = new();
        bool ok          = MeshValidation.Validate(mesh, MeshValidationFlags.AsymmetricAdjacency, adjacency, sb);

        Assert.False(ok);
    }

    [Fact]
    public void Validate_NullMesh_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeshValidation.Validate(null!));
    }

    [Fact]
    public void Validate_Messages_ContainsDiagnosticText()
    {
        StringBuilder sb = new();
        MeshValidation.Validate(CreateOutOfRangeIndexMesh(), MeshValidationFlags.None, sb);
        Assert.False(string.IsNullOrWhiteSpace(sb.ToString()));
    }
}
