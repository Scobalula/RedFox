using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides methods for applying face-order and vertex-order remappings to a <see cref="Mesh"/>,
/// corresponding to DirectXMesh <c>ReorderIB</c> and <c>FinalizeIB</c>.
/// </summary>
/// <remarks>
/// The remap arrays consumed here are typically produced by <see cref="MeshOptimizer"/>:
/// <list type="bullet">
///   <item><see cref="ReorderFaces"/> applies a face remap (from <see cref="MeshOptimizer.OptimizeFacesLRU(Mesh)"/>)
///   to reorder triangle groups in the index buffer.</item>
///   <item><see cref="RemapVertices"/> applies a vertex remap (from <see cref="MeshOptimizer.OptimizeVertexOrder(Mesh)"/>)
///   to translate every index in the buffer to the new compact vertex numbering.</item>
/// </list>
/// Both methods replace <see cref="Mesh.FaceIndices"/> with a new <see cref="DataBuffer{T}"/> and leave
/// vertex attribute buffers unchanged; callers are responsible for reordering vertex data to match.
/// </remarks>
public static class MeshRemap
{
    /// <summary>
    /// Reorders the triangles in <see cref="Mesh.FaceIndices"/> according to the provided face remap.
    /// The triple at <c>faceRemap[newIndex]</c> is placed at <c>newIndex</c> in the output.
    /// </summary>
    /// <param name="mesh">The target mesh. <see cref="Mesh.FaceIndices"/> is replaced with the reordered buffer.</param>
    /// <param name="faceRemap">
    /// A face remap array of length <c>faceCount</c> where <c>faceRemap[newIndex] == originalFaceIndex</c>,
    /// as returned by <see cref="MeshOptimizer.OptimizeFacesLRU(Mesh)"/> or <see cref="MeshOptimizer.SortFacesByAttribute"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> or <paramref name="faceRemap"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.FaceIndices"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="faceRemap"/> length does not equal the mesh face count.</exception>
    public static void ReorderFaces(Mesh mesh, int[] faceRemap)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faceRemap);

        if (mesh.FaceIndices is not { } faceIndices)
            throw new InvalidOperationException("Mesh must have face index data to reorder faces.");

        int faceCount  = faceIndices.ElementCount / 3;
        int valueCount = faceIndices.ValueCount;
        int compCount  = faceIndices.ComponentCount;

        if (faceRemap.Length != faceCount)
            throw new ArgumentException(
                $"faceRemap length ({faceRemap.Length}) must equal the face count ({faceCount}).",
                nameof(faceRemap));

        float[] output = GC.AllocateUninitializedArray<float>(faceCount * 3);

        for (int newFace = 0; newFace < faceCount; newFace++)
        {
            int srcBase = faceRemap[newFace] * 3;
            int dstBase = newFace * 3;
            output[dstBase]     = faceIndices.Get<float>(srcBase,     0, 0);
            output[dstBase + 1] = faceIndices.Get<float>(srcBase + 1, 0, 0);
            output[dstBase + 2] = faceIndices.Get<float>(srcBase + 2, 0, 0);
        }

        mesh.FaceIndices = new DataBuffer<float>(output, valueCount, compCount);
    }

    /// <summary>
    /// Translates every vertex index in <see cref="Mesh.FaceIndices"/> through the provided vertex remap,
    /// updating the index buffer to reference the new compact vertex numbering.
    /// </summary>
    /// <param name="mesh">The target mesh. <see cref="Mesh.FaceIndices"/> is replaced with the remapped buffer.</param>
    /// <param name="vertexRemap">
    /// A vertex remap of length <c>vertexCount</c> where <c>vertexRemap[newIndex] == originalIndex</c>,
    /// as returned by <see cref="MeshOptimizer.OptimizeVertexOrder(Mesh)"/>.
    /// Entries with value <c>-1</c> indicate unreferenced vertex slots.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> or <paramref name="vertexRemap"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.FaceIndices"/>.</exception>
    public static void RemapVertices(Mesh mesh, int[] vertexRemap)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertexRemap);

        if (mesh.FaceIndices is not { } faceIndices)
            throw new InvalidOperationException("Mesh must have face index data to remap vertices.");

        int indexCount = faceIndices.ElementCount;
        int valueCount = faceIndices.ValueCount;
        int compCount  = faceIndices.ComponentCount;

        // Build inverse: original → new compact index.
        int[] inverse = new int[vertexRemap.Length];
        inverse.AsSpan().Fill(-1);
        for (int newIdx = 0; newIdx < vertexRemap.Length; newIdx++)
        {
            int orig = vertexRemap[newIdx];
            if ((uint)orig < (uint)vertexRemap.Length)
                inverse[orig] = newIdx;
        }

        float[] output = GC.AllocateUninitializedArray<float>(indexCount);
        for (int j = 0; j < indexCount; j++)
        {
            int orig = faceIndices.Get<int>(j, 0, 0);
            if ((uint)orig < (uint)inverse.Length && inverse[orig] >= 0)
                output[j] = inverse[orig];
            else
                output[j] = orig; // preserve unmapped (e.g. sentinel -1) values
        }

        mesh.FaceIndices = new DataBuffer<float>(output, valueCount, compCount);
    }
}
