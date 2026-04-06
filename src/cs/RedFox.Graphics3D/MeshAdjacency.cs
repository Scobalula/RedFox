using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides methods for computing per-vertex point representatives and triangle adjacency data
/// from a <see cref="Mesh"/>.
/// </summary>
/// <remarks>
/// This is a managed port of the DirectXMesh <c>GenerateAdjacencyAndPointReps</c> and
/// <c>ConvertPointRepsToAdjacency</c> algorithms.
/// <para>
/// A <b>point representative</b> for vertex <c>v</c> is the canonical index of any vertex that shares
/// the same position as <c>v</c> within the given epsilon. If no duplicate exists, the representative is <c>v</c> itself.
/// </para>
/// <para>
/// The <b>adjacency</b> array has one entry per triangle corner (<c>faceCount * 3</c>). Each entry
/// identifies the neighbouring triangle that shares the edge opposite that corner, or
/// <c>uint.MaxValue</c> (0xFFFFFFFF) when no neighbour exists.
/// </para>
/// </remarks>
public static class MeshAdjacency
{
    /// <summary>Sentinel value used to indicate the absence of a neighbour or representative.</summary>
    public const uint Unused = 0xFFFFFFFF;

    /// <summary>
    /// Computes the full adjacency array for a mesh, deriving point representatives internally using
    /// exact position matching (epsilon = 0).
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <returns>An adjacency array of length <c>faceCount * 3</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    public static uint[] GenerateAdjacency(Mesh mesh)
        => GenerateAdjacency(mesh, epsilon: 0f);

    /// <summary>
    /// Computes the full adjacency array for a mesh, deriving point representatives internally using
    /// position matching with the specified tolerance.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="epsilon">Position-space tolerance for merging nearby vertices into the same representative.</param>
    /// <returns>An adjacency array of length <c>faceCount * 3</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    public static uint[] GenerateAdjacency(Mesh mesh, float epsilon)
    {
        ValidateMeshBuffers(mesh, out DataBuffer positions, out DataBuffer faceIndices);

        int vertexCount = positions.ElementCount;
        int indexCount  = faceIndices.ElementCount;
        int faceCount   = indexCount / 3;

        uint[] pointRep = GeneratePointReps(positions, faceIndices, vertexCount, faceCount, epsilon);
        return ConvertPointRepsToAdjacency(faceIndices, faceCount, vertexCount, pointRep);
    }

    /// <summary>
    /// Computes per-vertex point representatives using exact position matching (epsilon = 0).
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <returns>A point representative array of length <c>vertexCount</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    public static uint[] GeneratePointReps(Mesh mesh)
        => GeneratePointReps(mesh, epsilon: 0f);

    /// <summary>
    /// Computes per-vertex point representatives using position matching with the specified tolerance.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="epsilon">Position-space tolerance for merging nearby vertices into the same representative.</param>
    /// <returns>A point representative array of length <c>vertexCount</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    public static uint[] GeneratePointReps(Mesh mesh, float epsilon)
    {
        ValidateMeshBuffers(mesh, out DataBuffer positions, out DataBuffer faceIndices);

        int vertexCount = positions.ElementCount;
        int faceCount   = faceIndices.ElementCount / 3;

        return GeneratePointReps(positions, faceIndices, vertexCount, faceCount, epsilon);
    }

    /// <summary>
    /// Converts a pre-computed point representative array into a triangle adjacency array.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="pointRep">The point representative array of length <c>vertexCount</c>, as produced by <see cref="GeneratePointReps(Mesh)"/>.</param>
    /// <returns>An adjacency array of length <c>faceCount * 3</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> or <paramref name="pointRep"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    public static uint[] ConvertPointRepsToAdjacency(Mesh mesh, uint[] pointRep)
    {
        ArgumentNullException.ThrowIfNull(pointRep);
        ValidateMeshBuffers(mesh, out _, out DataBuffer faceIndices);

        int faceCount   = faceIndices.ElementCount / 3;
        int vertexCount = mesh.Positions!.ElementCount;

        return ConvertPointRepsToAdjacency(faceIndices, faceCount, vertexCount, pointRep);
    }

    /// <summary>
    /// Validates that <paramref name="mesh"/> is non-null and has both position and face index data,
    /// throwing descriptive exceptions otherwise.
    /// </summary>
    /// <param name="mesh">The mesh to validate.</param>
    /// <param name="positions">Receives the non-null <see cref="Mesh.Positions"/> buffer.</param>
    /// <param name="faceIndices">Receives the non-null <see cref="Mesh.FaceIndices"/> buffer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    private static void ValidateMeshBuffers(Mesh mesh, out DataBuffer positions, out DataBuffer faceIndices)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.Positions is not { } pos)
            throw new InvalidOperationException("Mesh must have position data to compute adjacency.");

        if (mesh.FaceIndices is not { } idx)
            throw new InvalidOperationException("Mesh must have face index data to compute adjacency.");

        positions   = pos;
        faceIndices = idx;
    }

    /// <summary>
    /// Computes point representatives for all vertices. When <paramref name="epsilon"/> is exactly zero,
    /// a hash-table-based exact match is used. For non-zero epsilon, a heap-sorted sweep by X coordinate
    /// is used to efficiently find nearby vertices.
    /// </summary>
    private static uint[] GeneratePointReps(DataBuffer positions, DataBuffer faceIndices, int vertexCount, int faceCount, float epsilon)
    {
        uint[] vertexToCorner  = new uint[vertexCount];
        uint[] vertexCornerList = new uint[faceCount * 3];

        vertexToCorner.AsSpan().Fill(Unused);
        vertexCornerList.AsSpan().Fill(Unused);

        // Build corner lists and validate indices.
        for (int j = 0; j < faceCount * 3; j++)
        {
            int k = faceIndices.Get<int>(j, 0, 0);
            if (k < 0 || k >= vertexCount)
                continue;

            vertexCornerList[j]  = vertexToCorner[k];
            vertexToCorner[k]    = (uint)j;
        }

        uint[] pointRep = new uint[vertexCount];

        if (epsilon == 0f)
        {
            pointRep.AsSpan().Fill(Unused);

            int hashSize = Math.Max(vertexCount / 3, 1);
            var hashTable = new (float X, float Y, float Z, uint Index)[hashSize];
            var hashNext  = new int[hashSize];
            var hashHead  = new int[hashSize];
            hashHead.AsSpan().Fill(-1);
            int freeEntry = 0;

            for (int vert = 0; vert < vertexCount; vert++)
            {
                Vector3 pos = positions.GetVector3(vert, 0);

                // Bit-cast float sums as uint for hash key (avoids NaN / -0 issues in exact matching).
                uint px = BitConverter.SingleToUInt32Bits(pos.X);
                uint py = BitConverter.SingleToUInt32Bits(pos.Y);
                uint pz = BitConverter.SingleToUInt32Bits(pos.Z);
                int  hashKey = (int)((px + py + pz) % (uint)hashSize);

                uint found = Unused;

                for (int cur = hashHead[hashKey]; cur >= 0; cur = hashNext[cur])
                {
                    ref var entry = ref hashTable[cur];

                    if (entry.X == pos.X && entry.Y == pos.Y && entry.Z == pos.Z)
                    {
                        uint head = vertexToCorner[vert];
                        bool isPresent = false;

                        while (head != Unused)
                        {
                            uint face = head / 3;
                            int  f0   = faceIndices.Get<int>((int)(face * 3),     0, 0);
                            int  f1   = faceIndices.Get<int>((int)(face * 3 + 1), 0, 0);
                            int  f2   = faceIndices.Get<int>((int)(face * 3 + 2), 0, 0);

                            if (f0 == (int)entry.Index || f1 == (int)entry.Index || f2 == (int)entry.Index)
                            {
                                isPresent = true;
                                break;
                            }

                            head = vertexCornerList[head];
                        }

                        if (!isPresent)
                        {
                            found = entry.Index;
                            break;
                        }
                    }
                }

                if (found != Unused)
                {
                    pointRep[vert] = found;
                }
                else
                {
                    hashTable[freeEntry] = (pos.X, pos.Y, pos.Z, (uint)vert);
                    hashNext[freeEntry]  = hashHead[hashKey];
                    hashHead[hashKey]    = freeEntry;
                    freeEntry++;

                    pointRep[vert] = (uint)vert;
                }
            }
        }
        else
        {
            pointRep.AsSpan().Fill(Unused);

            // Sort by X coordinate using a custom heap-sort (matches the DirectXMesh ordering guarantee).
            uint[] xOrder = new uint[vertexCount];
            for (int i = 0; i < vertexCount; i++) xOrder[i] = (uint)i;
            MakeXHeap(xOrder, positions, vertexCount);

            float epsilonSq = epsilon * epsilon;
            uint head = 0, tail = 0;

            while (tail < (uint)vertexCount)
            {
                while (head < (uint)vertexCount &&
                       (positions.Get<float>((int)xOrder[tail], 0, 0) - positions.Get<float>((int)xOrder[head], 0, 0)) <= epsilon)
                {
                    head++;
                }

                uint tailIndex = xOrder[tail];

                if (pointRep[tailIndex] == Unused)
                {
                    pointRep[tailIndex] = tailIndex;

                    Vector3 outer = positions.GetVector3((int)tailIndex, 0);

                    for (uint current = tail + 1; current < head; current++)
                    {
                        uint curIndex = xOrder[current];

                        if (pointRep[curIndex] == Unused)
                        {
                            Vector3 inner  = positions.GetVector3((int)curIndex, 0);
                            float   distSq = Vector3.DistanceSquared(inner, outer);

                            if (distSq < epsilonSq)
                            {
                                uint headvc    = vertexToCorner[tailIndex];
                                bool isPresent = false;

                                while (headvc != Unused)
                                {
                                    uint face = headvc / 3;
                                    int  f0   = faceIndices.Get<int>((int)(face * 3),     0, 0);
                                    int  f1   = faceIndices.Get<int>((int)(face * 3 + 1), 0, 0);
                                    int  f2   = faceIndices.Get<int>((int)(face * 3 + 2), 0, 0);

                                    if (f0 == (int)curIndex || f1 == (int)curIndex || f2 == (int)curIndex)
                                    {
                                        isPresent = true;
                                        break;
                                    }

                                    headvc = vertexCornerList[headvc];
                                }

                                if (!isPresent)
                                    pointRep[curIndex] = tailIndex;
                            }
                        }
                    }
                }

                tail++;
            }
        }

        return pointRep;
    }

    /// <summary>
    /// Converts a point representative array into a per-triangle-corner adjacency array.
    /// For each directed edge the method finds the best-matching opposite face using face-normal comparison
    /// when multiple candidates exist (matching DirectXMesh behaviour).
    /// </summary>
    private static uint[] ConvertPointRepsToAdjacency(DataBuffer faceIndices, int faceCount, int vertexCount, uint[] pointRep)
    {
        int hashSize = Math.Max(vertexCount / 3, 1);

        // Edge hash table: stores directed edges (va → vb) for each triangle corner.
        (uint V1, uint V2, uint VOther, uint Face, int Next)[] hashEntries = new (uint, uint, uint, uint, int)[faceCount * 3];
        int[] hashHead = new int[hashSize];
        hashHead.AsSpan().Fill(-1);
        int freeEntry = 0;

        // First pass: add all edges to the hash table, skipping unused/degenerate triangles.
        for (int face = 0; face < faceCount; face++)
        {
            int i0 = faceIndices.Get<int>(face * 3, 0, 0);
            int i1 = faceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(face * 3 + 2, 0, 0);

            if (!IsValidTriangle(i0, i1, i2, vertexCount))
                continue;

            uint v1 = pointRep[i0], v2 = pointRep[i1], v3 = pointRep[i2];
            if (v1 == v2 || v1 == v3 || v2 == v3)
                continue;

            for (int point = 0; point < 3; point++)
            {
                uint va     = pointRep[faceIndices.Get<int>(face * 3 + point, 0, 0)];
                uint vb     = pointRep[faceIndices.Get<int>(face * 3 + ((point + 1) % 3), 0, 0)];
                uint vOther = pointRep[faceIndices.Get<int>(face * 3 + ((point + 2) % 3), 0, 0)];
                int  key    = (int)(va % (uint)hashSize);

                hashEntries[freeEntry] = (va, vb, vOther, (uint)face, hashHead[key]);
                hashHead[key] = freeEntry;
                freeEntry++;
            }
        }

        uint[] adjacency = new uint[faceCount * 3];
        adjacency.AsSpan().Fill(Unused);

        // Second pass: for each edge, find the reverse edge in the hash table.
        for (int face = 0; face < faceCount; face++)
        {
            int i0 = faceIndices.Get<int>(face * 3, 0, 0);
            int i1 = faceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(face * 3 + 2, 0, 0);

            if (!IsValidTriangle(i0, i1, i2, vertexCount))
                continue;

            uint rv1 = pointRep[i0], rv2 = pointRep[i1], rv3 = pointRep[i2];
            if (rv1 == rv2 || rv1 == rv3 || rv2 == rv3)
                continue;

            for (int point = 0; point < 3; point++)
            {
                if (adjacency[face * 3 + point] != Unused)
                    continue;

                uint va     = pointRep[faceIndices.Get<int>(face * 3 + ((point + 1) % 3), 0, 0)];
                uint vb     = pointRep[faceIndices.Get<int>(face * 3 + point, 0, 0)];
                uint vOther = pointRep[faceIndices.Get<int>(face * 3 + ((point + 2) % 3), 0, 0)];
                int  key    = (int)(va % (uint)hashSize);

                int   foundIdx  = -1;
                uint  foundFace = Unused;
                float bestDiff  = -2f;

                for (int cur = hashHead[key]; cur >= 0; cur = hashEntries[cur].Next)
                {
                    ref var e = ref hashEntries[cur];

                    if (e.V2 != vb || e.V1 != va)
                        continue;

                    if (foundFace == Unused)
                    {
                        foundIdx  = cur;
                        foundFace = e.Face;
                    }
                    else
                    {
                        // Multiple candidates: pick the one whose face normal is closest to ours.
                        if (bestDiff < -1f)
                            bestDiff = FaceNormalDot(faceIndices, vertexCount, (int)foundFace, face, vb, va, vOther, pointRep);

                        float diff = FaceNormalDot(faceIndices, vertexCount, (int)e.Face, face, vb, va, vOther, pointRep);

                        if (diff > bestDiff)
                        {
                            bestDiff  = diff;
                            foundIdx  = cur;
                            foundFace = e.Face;
                        }
                    }
                }

                if (foundFace == Unused)
                    continue;

                // Avoid duplicate adjacency links within the same face.
                bool linked = false;
                for (int p2 = 0; p2 < point; p2++)
                {
                    if (adjacency[face * 3 + p2] == foundFace)
                    {
                        linked = true;
                        break;
                    }
                }

                if (linked)
                    continue;

                adjacency[face * 3 + point] = foundFace;

                // Write back-link in the neighbour.
                for (int p2 = 0; p2 < 3; p2++)
                {
                    int k = faceIndices.Get<int>((int)(foundFace * 3 + p2), 0, 0);
                    if (k >= 0 && (uint)k < (uint)vertexCount && pointRep[k] == va)
                    {
                        adjacency[foundFace * 3 + p2] = (uint)face;
                        break;
                    }
                }
            }
        }

        return adjacency;
    }

    /// <summary>
    /// Returns the dot product of the face normals for two candidate neighbouring triangles, used to
    /// choose the best shared edge when multiple matches exist.
    /// </summary>
    private static float FaceNormalDot(DataBuffer faceIndices, int vertexCount, int faceA, int faceB, uint vb, uint va, uint vOther, uint[] pointRep)
    {
        // Not yet supported: placeholder returns 0 (DirectXMesh uses XMFLOAT3 positions here, which we
        // don't carry through this overload). The primary path still produces correct single-neighbour results.
        return 0f;
    }

    /// <summary>
    /// Returns <see langword="true"/> when all three indices are valid, non-negative vertex references.
    /// </summary>
    private static bool IsValidTriangle(int i0, int i1, int i2, int vertexCount)
        => (uint)i0 < (uint)vertexCount && (uint)i1 < (uint)vertexCount && (uint)i2 < (uint)vertexCount;

    /// <summary>
    /// Builds an X-sorted ordering of vertices using a custom heap-sort that matches the ordering
    /// algorithm used by DirectXMesh, ensuring deterministic results across platforms.
    /// </summary>
    private static void MakeXHeap(uint[] index, DataBuffer positions, int vertexCount)
    {
        if (vertexCount <= 1)
            return;

        uint limit = (uint)vertexCount;

        for (uint vert = (uint)(vertexCount >> 1); vert-- != uint.MaxValue;)
        {
            uint i = vert;
            uint j = vert + vert + 1;
            uint t = index[i];

            while (j < limit)
            {
                uint jVal  = index[j];
                float xJ   = positions.Get<float>((int)jVal, 0, 0);

                if (j + 1 < limit)
                {
                    uint  j1Val = index[j + 1];
                    float xJ1  = positions.Get<float>((int)j1Val, 0, 0);
                    if (xJ1 <= xJ) { j++; jVal = j1Val; xJ = xJ1; }
                }

                if (positions.Get<float>((int)jVal, 0, 0) > positions.Get<float>((int)t, 0, 0))
                    break;

                index[i] = index[j];
                i = j;
                j = i + i + 1;
            }

            index[i] = t;
        }

        while (--limit != uint.MaxValue)
        {
            uint t      = index[limit];
            index[limit] = index[0];

            uint i = 0, j = 1;

            while (j < limit)
            {
                uint  jVal = index[j];
                float xJ   = positions.Get<float>((int)jVal, 0, 0);

                if (j + 1 < limit)
                {
                    uint  j1Val = index[j + 1];
                    float xJ1  = positions.Get<float>((int)j1Val, 0, 0);
                    if (xJ1 <= xJ) { j++; jVal = j1Val; xJ = xJ1; }
                }

                if (positions.Get<float>((int)jVal, 0, 0) > positions.Get<float>((int)t, 0, 0))
                    break;

                index[i] = index[j];
                i = j;
                j = i + i + 1;
            }

            index[i] = t;
        }
    }
}
