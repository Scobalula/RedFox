using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides methods for computing per-vertex normals on a <see cref="Mesh"/> from its triangle faces and
/// storing the result directly in <see cref="Mesh.Normals"/>, replacing any pre-existing data.
/// </summary>
/// <remarks>
/// This is a managed port of the DirectXMesh <c>ComputeNormals</c> algorithm. Each face contributes its
/// normalized cross-product face normal to each of its three vertices; only the per-face weighting applied
/// during accumulation differs between modes. The final per-vertex normal is the normalized sum of all
/// weighted contributions from adjacent triangles.
/// </remarks>
public static class MeshNormals
{
    /// <summary>Minimum length below which a normal vector is treated as degenerate and clamped to zero before normalization.</summary>
    private const float Epsilon = 0.0001f;

    /// <summary>
    /// Computes per-vertex normals using angle-weighted averaging for a counter-clockwise mesh,
    /// and stores the result in <see cref="Mesh.Normals"/>.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.Positions"/> or <see cref="Mesh.FaceIndices"/>.</exception>
    public static void Generate(Mesh mesh)
        => Generate(mesh, NormalGenerationMode.WeightedByAngle, MeshFaceOrder.CounterClockwise);

    /// <summary>
    /// Computes per-vertex normals using the specified weighting mode for a counter-clockwise mesh,
    /// and stores the result in <see cref="Mesh.Normals"/>.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="mode">The weighting strategy applied when accumulating face contributions.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.Positions"/> or <see cref="Mesh.FaceIndices"/>.</exception>
    public static void Generate(Mesh mesh, NormalGenerationMode mode)
        => Generate(mesh, mode, MeshFaceOrder.CounterClockwise);

    /// <summary>
    /// Computes per-vertex normals using the specified weighting mode and winding order,
    /// and stores the result in <see cref="Mesh.Normals"/>.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="mode">The weighting strategy applied when accumulating face contributions.</param>
    /// <param name="faceOrder">The winding order of the mesh triangles.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.Positions"/> or <see cref="Mesh.FaceIndices"/>.</exception>
    public static void Generate(Mesh mesh, NormalGenerationMode mode, MeshFaceOrder faceOrder)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.Positions is not { } positions)
            throw new InvalidOperationException("Mesh must have position data to generate normals.");

        if (mesh.FaceIndices is not { } faceIndices)
            throw new InvalidOperationException("Mesh must have face index data to generate normals.");

        int vertexCount = positions.ElementCount;
        int faceCount   = faceIndices.ElementCount / 3;
        bool clockwise  = faceOrder == MeshFaceOrder.Clockwise;

        Vector3[] accum = new Vector3[vertexCount];

        switch (mode)
        {
            case NormalGenerationMode.EqualWeight:    AccumulateEqualWeight(faceIndices, faceCount, positions, vertexCount, accum);    break;
            case NormalGenerationMode.WeightedByArea: AccumulateWeightedByArea(faceIndices, faceCount, positions, vertexCount, accum); break;
            default:                                  AccumulateWeightedByAngle(faceIndices, faceCount, positions, vertexCount, accum); break;
        }

        float[] normalData = GC.AllocateUninitializedArray<float>(vertexCount * 3);
        Span<Vector3> normalSpan = MemoryMarshal.Cast<float, Vector3>(normalData.AsSpan());

        for (int v = 0; v < vertexCount; v++)
        {
            Vector3 n = Vector3.Normalize(accum[v]);
            normalSpan[v] = clockwise ? -n : n;
        }

        mesh.Normals = new DataBuffer<float>(normalData, 1, 3);
    }

    /// <summary>
    /// Returns <see langword="true"/> when all three indices reference valid vertices for a non-degenerate, non-sentinel triangle.
    /// </summary>
    /// <param name="i0">First corner index.</param>
    /// <param name="i1">Second corner index.</param>
    /// <param name="i2">Third corner index.</param>
    /// <param name="vertexCount">Total number of vertices in the mesh.</param>
    public static bool IsValidTriangle(int i0, int i1, int i2, int vertexCount)
        => (uint)i0 < (uint)vertexCount && (uint)i1 < (uint)vertexCount && (uint)i2 < (uint)vertexCount;

    /// <summary>
    /// Accumulates face normals with equal weight at each vertex. Every adjacent triangle contributes its
    /// normalized face normal once regardless of shape or size.
    /// </summary>
    /// <param name="faceIndices">The mesh index buffer.</param>
    /// <param name="faceCount">Number of triangles.</param>
    /// <param name="positions">The vertex position buffer.</param>
    /// <param name="vertexCount">Total vertex count.</param>
    /// <param name="accum">Per-vertex accumulation array to which contributions are added in-place.</param>
    public static void AccumulateEqualWeight(DataBuffer faceIndices, int faceCount, DataBuffer positions, int vertexCount, Vector3[] accum)
    {
        for (int face = 0; face < faceCount; face++)
        {
            int i0 = faceIndices.Get<int>(face * 3, 0, 0);
            int i1 = faceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(face * 3 + 2, 0, 0);

            if (!IsValidTriangle(i0, i1, i2, vertexCount))
                continue;

            Vector3 p0 = positions.GetVector3(i0, 0);
            Vector3 p1 = positions.GetVector3(i1, 0);
            Vector3 p2 = positions.GetVector3(i2, 0);
            Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(p1 - p0, p2 - p0));

            accum[i0] += faceNormal;
            accum[i1] += faceNormal;
            accum[i2] += faceNormal;
        }
    }

    /// <summary>
    /// Accumulates face normals weighted by the interior angle of the triangle at each shared vertex.
    /// </summary>
    /// <param name="faceIndices">The mesh index buffer.</param>
    /// <param name="faceCount">Number of triangles.</param>
    /// <param name="positions">The vertex position buffer.</param>
    /// <param name="vertexCount">Total vertex count.</param>
    /// <param name="accum">Per-vertex accumulation array to which contributions are added in-place.</param>
    public static void AccumulateWeightedByAngle(DataBuffer faceIndices, int faceCount, DataBuffer positions, int vertexCount, Vector3[] accum)
    {
        for (int face = 0; face < faceCount; face++)
        {
            int i0 = faceIndices.Get<int>(face * 3, 0, 0);
            int i1 = faceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(face * 3 + 2, 0, 0);

            if (!IsValidTriangle(i0, i1, i2, vertexCount))
                continue;

            Vector3 p0 = positions.GetVector3(i0, 0);
            Vector3 p1 = positions.GetVector3(i1, 0);
            Vector3 p2 = positions.GetVector3(i2, 0);

            Vector3 e01 = p1 - p0, e02 = p2 - p0;
            Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(e01, e02));

            float w0 = MathF.Acos(float.Clamp(Vector3.Dot(Vector3.Normalize(e01), Vector3.Normalize(e02)), -1f, 1f));
            float w1 = MathF.Acos(float.Clamp(Vector3.Dot(Vector3.Normalize(p2 - p1), Vector3.Normalize(p0 - p1)), -1f, 1f));
            float w2 = MathF.Acos(float.Clamp(Vector3.Dot(Vector3.Normalize(p0 - p2), Vector3.Normalize(p1 - p2)), -1f, 1f));

            accum[i0] += faceNormal * w0;
            accum[i1] += faceNormal * w1;
            accum[i2] += faceNormal * w2;
        }
    }

    /// <summary>
    /// Accumulates face normals weighted by the surface area of the triangle at each shared vertex.
    /// </summary>
    /// <param name="faceIndices">The mesh index buffer.</param>
    /// <param name="faceCount">Number of triangles.</param>
    /// <param name="positions">The vertex position buffer.</param>
    /// <param name="vertexCount">Total vertex count.</param>
    /// <param name="accum">Per-vertex accumulation array to which contributions are added in-place.</param>
    public static void AccumulateWeightedByArea(DataBuffer faceIndices, int faceCount, DataBuffer positions, int vertexCount, Vector3[] accum)
    {
        for (int face = 0; face < faceCount; face++)
        {
            int i0 = faceIndices.Get<int>(face * 3, 0, 0);
            int i1 = faceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(face * 3 + 2, 0, 0);

            if (!IsValidTriangle(i0, i1, i2, vertexCount))
                continue;

            Vector3 p0 = positions.GetVector3(i0, 0);
            Vector3 p1 = positions.GetVector3(i1, 0);
            Vector3 p2 = positions.GetVector3(i2, 0);

            Vector3 e01 = p1 - p0, e02 = p2 - p0;
            Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(e01, e02));

            // The magnitude of the cross product equals twice the triangle area; all three corners share the same triangle.
            float w = Vector3.Cross(e01, e02).Length();

            accum[i0] += faceNormal * w;
            accum[i1] += faceNormal * w;
            accum[i2] += faceNormal * w;
        }
    }
}
