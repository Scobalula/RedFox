using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides methods for computing a per-vertex tangent frame on a <see cref="Mesh"/> using its normals
/// and first UV layer, storing results in <see cref="Mesh.Tangents"/> and optionally <see cref="Mesh.BiTangents"/>.
/// </summary>
/// <remarks>
/// This is a managed port of the DirectXMesh <c>ComputeTangentFrame</c> algorithm. For each triangle,
/// the UV-space tangent and bitangent are computed via the standard UV-derivative / edge-vector formula.
/// Per-vertex tangents are then derived by Gram-Schmidt orthonormalization against the vertex normal.
/// Degenerate tangent frames are recovered by building an arbitrary perpendicular axis from the normal.
/// </remarks>
public static class MeshTangentFrame
{
    /// <summary>Minimum vector length below which a tangent or bitangent is treated as degenerate and rebuilt from the normal.</summary>
    private const float Epsilon = 0.0001f;

    /// <summary>
    /// Computes per-vertex tangents stored as four-component vectors where the W component encodes the
    /// bitangent handedness sign (+1 or -1). Bitangents are not stored. Existing <see cref="Mesh.Tangents"/>
    /// and <see cref="Mesh.BiTangents"/> data is replaced.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/>, <see cref="Mesh.Normals"/>,
    /// <see cref="Mesh.UVLayers"/>, and <see cref="Mesh.FaceIndices"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing any required buffer.</exception>
    public static void Generate(Mesh mesh)
        => GenerateCore(mesh, storeHandedness: true, storeBiTangents: false);

    /// <summary>
    /// Computes per-vertex tangents stored as four-component vectors where the W component encodes the
    /// bitangent handedness sign (+1 or -1), and optionally writes computed bitangents to
    /// <see cref="Mesh.BiTangents"/>. Existing tangent and bitangent data is replaced.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/>, <see cref="Mesh.Normals"/>,
    /// <see cref="Mesh.UVLayers"/>, and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="storeBiTangents">When <see langword="true"/>, the orthonormalized bitangent is also written to <see cref="Mesh.BiTangents"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing any required buffer.</exception>
    public static void Generate(Mesh mesh, bool storeBiTangents)
        => GenerateCore(mesh, storeHandedness: true, storeBiTangents: storeBiTangents);

    /// <summary>
    /// Computes per-vertex tangents stored as three-component vectors without a handedness sign.
    /// Bitangents are not stored. Existing <see cref="Mesh.Tangents"/> and <see cref="Mesh.BiTangents"/>
    /// data is replaced.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/>, <see cref="Mesh.Normals"/>,
    /// <see cref="Mesh.UVLayers"/>, and <see cref="Mesh.FaceIndices"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing any required buffer.</exception>
    public static void GenerateCompact(Mesh mesh)
        => GenerateCore(mesh, storeHandedness: false, storeBiTangents: false);

    /// <summary>
    /// Computes per-vertex tangents stored as three-component vectors without a handedness sign, and
    /// optionally writes computed bitangents to <see cref="Mesh.BiTangents"/>. Existing tangent and
    /// bitangent data is replaced.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/>, <see cref="Mesh.Normals"/>,
    /// <see cref="Mesh.UVLayers"/>, and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="storeBiTangents">When <see langword="true"/>, the orthonormalized bitangent is also written to <see cref="Mesh.BiTangents"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing any required buffer.</exception>
    public static void GenerateCompact(Mesh mesh, bool storeBiTangents)
        => GenerateCore(mesh, storeHandedness: false, storeBiTangents: storeBiTangents);

    /// <summary>
    /// Core implementation shared by all public entry-points. Generates tangent-frame data and assigns
    /// <see cref="Mesh.Tangents"/> and, optionally, <see cref="Mesh.BiTangents"/> on the mesh.
    /// </summary>
    /// <param name="mesh">The mesh to generate tangent-frame data for.</param>
    /// <param name="storeHandedness">When <see langword="true"/>, the tangent is stored as a four-component
    /// vector whose W component encodes the handedness of the tangent frame (+1 or -1).</param>
    /// <param name="storeBiTangents">When <see langword="true"/>, a separate bitangent buffer is assigned to <see cref="Mesh.BiTangents"/>.</param>
    public static void GenerateCore(Mesh mesh, bool storeHandedness, bool storeBiTangents)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.Positions is not { } positions)
            throw new InvalidOperationException("Mesh must have position data to generate a tangent frame.");

        if (mesh.Normals is not { } normals)
            throw new InvalidOperationException("Mesh must have normal data to generate a tangent frame.");

        if (mesh.UVLayers is not { } uvLayers)
            throw new InvalidOperationException("Mesh must have UV layer data to generate a tangent frame.");

        if (mesh.FaceIndices is not { } faceIndices)
            throw new InvalidOperationException("Mesh must have face index data to generate a tangent frame.");

        int vertexCount = positions.ElementCount;
        int faceCount   = faceIndices.ElementCount / 3;

        // Accumulate raw UV-derived tangent and bitangent contributions per vertex.
        Vector3[] tan1 = new Vector3[vertexCount];
        Vector3[] tan2 = new Vector3[vertexCount];
        AccumulateTangents(faceIndices, faceCount, positions, uvLayers, vertexCount, tan1, tan2);

        int     tangentComponents = storeHandedness ? 4 : 3;
        float[] tangentData       = GC.AllocateUninitializedArray<float>(vertexCount * tangentComponents);
        float[]? biTangentData    = storeBiTangents ? GC.AllocateUninitializedArray<float>(vertexCount * 3) : null;

        Span<Vector3> out3  = storeHandedness ? Span<Vector3>.Empty : MemoryMarshal.Cast<float, Vector3>(tangentData.AsSpan());
        Span<Vector4> out4  = storeHandedness ? MemoryMarshal.Cast<float, Vector4>(tangentData.AsSpan()) : Span<Vector4>.Empty;
        Span<Vector3> outBi = biTangentData is not null ? MemoryMarshal.Cast<float, Vector3>(biTangentData.AsSpan()) : Span<Vector3>.Empty;

        OrthonormalizeAndStore(vertexCount, normals, tan1, tan2, out3, out4, outBi);

        mesh.Tangents   = new DataBuffer<float>(tangentData, 1, tangentComponents);
        mesh.BiTangents = biTangentData is not null ? new DataBuffer<float>(biTangentData, 1, 3) : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when all three indices reference valid, non-sentinel vertices.
    /// </summary>
    private static bool IsValidTriangle(int i0, int i1, int i2, int vertexCount)
        => (uint)i0 < (uint)vertexCount && (uint)i1 < (uint)vertexCount && (uint)i2 < (uint)vertexCount;

    /// <summary>
    /// Iterates all triangles and accumulates UV-derived raw tangent (<paramref name="tan1"/>) and
    /// bitangent (<paramref name="tan2"/>) vectors for each vertex.
    /// </summary>
    /// <param name="faceIndices">Buffer of packed triangle indices (three consecutive entries per face).</param>
    /// <param name="faceCount">Number of triangles in the mesh.</param>
    /// <param name="positions">Per-vertex position buffer.</param>
    /// <param name="uvLayers">Per-vertex UV coordinate buffer.</param>
    /// <param name="vertexCount">Total number of vertices.</param>
    /// <param name="tan1">Output array that receives accumulated raw tangent contributions; must be pre-allocated to <paramref name="vertexCount"/> elements.</param>
    /// <param name="tan2">Output array that receives accumulated raw bitangent contributions; must be pre-allocated to <paramref name="vertexCount"/> elements.</param>
    public static void AccumulateTangents(DataBuffer faceIndices, int faceCount, DataBuffer positions, DataBuffer uvLayers, int vertexCount, Vector3[] tan1, Vector3[] tan2)
    {
        for (int face = 0; face < faceCount; face++)
        {
            int i0 = faceIndices.Get<int>(face * 3, 0, 0);
            int i1 = faceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(face * 3 + 2, 0, 0);

            if (!IsValidTriangle(i0, i1, i2, vertexCount))
                continue;

            Vector2 uv0 = uvLayers.GetVector2(i0, 0);
            Vector2 uv1 = uvLayers.GetVector2(i1, 0);
            Vector2 uv2 = uvLayers.GetVector2(i2, 0);

            Vector2 dt1 = uv1 - uv0, dt2 = uv2 - uv0;

            // Reciprocal of the UV-space determinant; fall back to 1 for degenerate UV layouts.
            float det = dt1.X * dt2.Y - dt1.Y * dt2.X;
            float d   = MathF.Abs(det) <= Epsilon ? 1.0f : 1.0f / det;

            Vector3 e1 = positions.GetVector3(i1, 0) - positions.GetVector3(i0, 0);
            Vector3 e2 = positions.GetVector3(i2, 0) - positions.GetVector3(i0, 0);

            // Solve for the tangent T = dr/du and bitangent B = dr/dv using the 2x2 UV inverse.
            Vector3 tangent   =  ( dt2.Y * d) * e1 + (-dt1.Y * d) * e2;
            Vector3 bitangent = (-dt2.X * d) * e1 + ( dt1.X * d) * e2;

            tan1[i0] += tangent;   tan1[i1] += tangent;   tan1[i2] += tangent;
            tan2[i0] += bitangent; tan2[i1] += bitangent; tan2[i2] += bitangent;
        }
    }

    /// <summary>
    /// Applies Gram-Schmidt orthonormalization to the accumulated tangent frame for each vertex and
    /// writes the results into the provided output spans. Degenerate frames are resolved by falling back
    /// to an arbitrary perpendicular axis constructed from the vertex normal.
    /// </summary>
    /// <param name="vertexCount">Number of vertices to process.</param>
    /// <param name="normals">Per-vertex normal buffer used as the first basis axis.</param>
    /// <param name="tan1">Accumulated raw tangent array produced by <see cref="AccumulateTangents"/>.</param>
    /// <param name="tan2">Accumulated raw bitangent array produced by <see cref="AccumulateTangents"/>.</param>
    /// <param name="out3">Destination span for three-component tangent output; pass <see cref="Span{T}.Empty"/> to skip.</param>
    /// <param name="out4">Destination span for four-component tangent-with-handedness output; pass <see cref="Span{T}.Empty"/> to skip.</param>
    /// <param name="outBi">Destination span for bitangent output; pass <see cref="Span{T}.Empty"/> to skip.</param>
    public static void OrthonormalizeAndStore(int vertexCount, DataBuffer normals, Vector3[] tan1, Vector3[] tan2, Span<Vector3> out3, Span<Vector4> out4, Span<Vector3> outBi)
    {
        for (int j = 0; j < vertexCount; j++)
        {
            Vector3 b0  = Vector3.Normalize(normals.GetVector3(j, 0));
            Vector3 t1v = tan1[j];
            Vector3 t2v = tan2[j];

            // Gram-Schmidt step 1: project accumulated tangent onto the normal plane.
            Vector3 b1Raw = t1v - Vector3.Dot(b0, t1v) * b0;
            float   len1  = b1Raw.Length();
            Vector3 b1    = len1 > 0f ? b1Raw / len1 : Vector3.Zero;

            // Gram-Schmidt step 2: project accumulated bitangent onto the plane spanned by N and T.
            Vector3 b2Raw = t2v - Vector3.Dot(b0, t2v) * b0 - Vector3.Dot(b1, t2v) * b1;
            float   len2  = b2Raw.Length();
            Vector3 b2    = len2 > 0f ? b2Raw / len2 : Vector3.Zero;

            // Recover degenerate axes from whichever valid vectors remain.
            if (len1 <= Epsilon || len2 <= Epsilon)
            {
                if (len1 > 0.5f)
                {
                    // Bitangent degenerated; derive from normal cross tangent.
                    b2 = Vector3.Cross(b0, b1);
                }
                else if (len2 > 0.5f)
                {
                    // Tangent degenerated; derive from bitangent cross normal.
                    b1 = Vector3.Cross(b2, b0);
                }
                else
                {
                    // Both degenerated; build an arbitrary orthonormal frame from the normal alone.
                    float dX = MathF.Abs(Vector3.Dot(Vector3.UnitX, b0));
                    float dY = MathF.Abs(Vector3.Dot(Vector3.UnitY, b0));
                    float dZ = MathF.Abs(Vector3.Dot(Vector3.UnitZ, b0));
                    Vector3 axis = dX < dY ? (dX < dZ ? Vector3.UnitX : Vector3.UnitZ)
                                           : (dY < dZ ? Vector3.UnitY : Vector3.UnitZ);
                    b1 = Vector3.Normalize(Vector3.Cross(b0, axis));
                    b2 = Vector3.Normalize(Vector3.Cross(b0, b1));
                }
            }

            if (!out3.IsEmpty)
                out3[j] = b1;

            if (!out4.IsEmpty)
            {
                float w = Vector3.Dot(Vector3.Cross(b0, t1v), t2v) < 0f ? -1f : 1f;
                out4[j] = new Vector4(b1, w);
            }

            if (!outBi.IsEmpty)
                outBi[j] = b2;
        }
    }
}
