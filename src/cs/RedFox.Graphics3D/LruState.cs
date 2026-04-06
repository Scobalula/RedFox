using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D;

/// <summary>
/// Mutable working state for a single Forsyth LRU vertex cache optimization pass in <see cref="MeshOptimizer"/>.
/// All arrays are allocated once per pass and updated in-place throughout the main loop.
/// </summary>
internal sealed class LruState
{
    /// <summary>
    /// Gets the number of vertices in the mesh being optimized.
    /// </summary>
    internal int VertexCount { get; }

    /// <summary>
    /// Gets the simulated post-transform vertex cache entry count.
    /// </summary>
    internal int CacheSize { get; }

    /// <summary>
    /// Gets the current LRU cache position of each vertex, or <see cref="int.MaxValue"/> when the
    /// vertex is not currently resident in the cache.
    /// </summary>
    internal int[] CachePosition { get; }

    /// <summary>
    /// Gets the remaining number of unprocessed (not yet output) faces that reference each vertex.
    /// Decremented by one for each vertex as its containing face is emitted.
    /// </summary>
    internal int[] ActiveFaceCount { get; }

    /// <summary>
    /// Gets the current Forsyth score for each vertex.
    /// </summary>
    internal float[] VertexScore { get; }

    /// <summary>
    /// Gets the current aggregate score for each face, computed as the sum of the Forsyth scores
    /// of its three corner vertices.
    /// </summary>
    internal float[] FaceScore { get; }

    /// <summary>
    /// Gets a flag that indicates whether each face has already been emitted to the output remap.
    /// </summary>
    internal bool[] FaceDone { get; }

    /// <summary>
    /// Gets the CSR flat face-index list used for vertex-to-face adjacency queries.
    /// </summary>
    internal int[] AdjFaces { get; }

    /// <summary>
    /// Gets the CSR row-start offsets into <see cref="AdjFaces"/>; row <c>v</c> spans <c>[AdjStart[v], AdjStart[v+1])</c>.
    /// </summary>
    internal int[] AdjStart { get; }

    /// <summary>
    /// Initialises a new <see cref="LruState"/> and marks all vertices as outside the cache.
    /// </summary>
    /// <param name="vertexCount">Total vertex count in the mesh.</param>
    /// <param name="cacheSize">Simulated cache entry count.</param>
    /// <param name="faceCount">Total triangle count in the mesh.</param>
    /// <param name="adjFaces">Pre-built CSR face list (shared; not copied).</param>
    /// <param name="adjStart">Pre-built CSR row-start offsets (shared; not copied).</param>
    internal LruState(int vertexCount, int cacheSize, int faceCount, int[] adjFaces, int[] adjStart)
    {
        VertexCount     = vertexCount;
        CacheSize       = cacheSize;
        CachePosition   = new int[vertexCount];
        ActiveFaceCount = new int[vertexCount];
        VertexScore     = new float[vertexCount];
        FaceScore       = new float[faceCount];
        FaceDone        = new bool[faceCount];
        AdjFaces        = adjFaces;
        AdjStart        = adjStart;
        CachePosition.AsSpan().Fill(int.MaxValue);
    }
}
