using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides methods for optimizing face ordering and vertex ordering in a <see cref="Mesh"/> to
/// improve GPU vertex cache utilization and rendering throughput.
/// </summary>
/// <remarks>
/// This is a managed port of the DirectXMesh optimization algorithms.
/// <list type="bullet">
///   <item><see cref="SortFacesByAttribute"/> — Stable-sorts per-face attributes, equivalent to DirectXMesh <c>AttributeSort</c>.</item>
///   <item><see cref="OptimizeVertexOrder(Mesh)"/> — Reorders vertices so they appear in order of first use, equivalent to DirectXMesh <c>OptimizeVertices</c>.</item>
///   <item><see cref="OptimizeFacesLRU(Mesh, int)"/> — Reorders faces to maximize post-transform vertex cache hit rate using the Forsyth LRU algorithm, equivalent to DirectXMesh <c>OptimizeFacesLRU</c>.</item>
/// </list>
/// </remarks>
public static class MeshOptimizer
{
    /// <summary>Default LRU vertex cache size used when no explicit size is specified.</summary>
    public const int DefaultLruCacheSize = 32;

    /// <summary>
    /// Stable-sorts the per-face attribute array so that faces are grouped by material or subset ID,
    /// and returns an array that maps each output position to the original face index.
    /// </summary>
    /// <param name="attributes">Per-face attribute (material/subset) IDs. Modified in-place to reflect the sorted order.</param>
    /// <returns>A face remap array of the same length where <c>faceRemap[newIndex] == originalFaceIndex</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="attributes"/> is <see langword="null"/>.</exception>
    public static int[] SortFacesByAttribute(uint[] attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        int n = attributes.Length;
        var pairs = new (uint Attr, int Orig)[n];
        for (int i = 0; i < n; i++) pairs[i] = (attributes[i], i);

        Array.Sort(pairs, static (a, b) => a.Attr.CompareTo(b.Attr));

        int[] faceRemap = new int[n];
        for (int i = 0; i < n; i++) { attributes[i] = pairs[i].Attr; faceRemap[i] = pairs[i].Orig; }

        return faceRemap;
    }

    /// <summary>
    /// Computes a vertex remap that places vertices in order of first use within the index buffer,
    /// reducing the working set for sequential draw calls. Unreferenced vertices are placed at the end.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.FaceIndices"/> and <see cref="Mesh.Positions"/>.</param>
    /// <returns>A vertex remap array of length <c>vertexCount</c> where <c>vertexRemap[newIndex] == originalIndex</c>, or <c>-1</c> for unreferenced vertices.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    public static int[] OptimizeVertexOrder(Mesh mesh)
        => OptimizeVertexOrder(mesh, out _);

    /// <summary>
    /// Computes a vertex remap that places vertices in order of first use within the index buffer,
    /// reducing the working set for sequential draw calls. Unreferenced vertices are placed at the end.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.FaceIndices"/> and <see cref="Mesh.Positions"/>.</param>
    /// <param name="trailingUnused">Receives the count of unreferenced vertices appended at the tail of the remap.</param>
    /// <returns>A vertex remap array of length <c>vertexCount</c> where <c>vertexRemap[newIndex] == originalIndex</c>, or <c>-1</c> for unreferenced vertices.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh is missing required buffers.</exception>
    public static int[] OptimizeVertexOrder(Mesh mesh, out int trailingUnused)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.FaceIndices is not { } faceIndices)
            throw new InvalidOperationException("Mesh must have face index data to optimize vertex order.");

        if (mesh.Positions is not { } positions)
            throw new InvalidOperationException("Mesh must have position data to optimize vertex order.");

        int vertexCount = positions.ElementCount;
        int indexCount  = faceIndices.ElementCount;

        int[] tempRemap = new int[vertexCount];
        tempRemap.AsSpan().Fill(-1);

        int curVertex = 0;
        for (int j = 0; j < indexCount; j++)
        {
            int idx = faceIndices.Get<int>(j, 0, 0);
            if ((uint)idx >= (uint)vertexCount) continue;
            if (tempRemap[idx] < 0) tempRemap[idx] = curVertex++;
        }

        int[] vertexRemap = new int[vertexCount];
        vertexRemap.AsSpan().Fill(-1);

        int unused = 0;
        for (int j = 0; j < vertexCount; j++)
        {
            int mapped = tempRemap[j];
            if (mapped < 0) unused++;
            else vertexRemap[mapped] = j;
        }

        trailingUnused = unused;
        return vertexRemap;
    }

    /// <summary>
    /// Computes a face remap that reorders triangles for improved post-transform vertex cache locality
    /// using the Forsyth LRU algorithm with the default cache size of <see cref="DefaultLruCacheSize"/>.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.FaceIndices"/> and <see cref="Mesh.Positions"/>.</param>
    /// <returns>A face remap array of length <c>faceCount</c> where <c>faceRemap[newIndex] == originalFaceIndex</c>. Degenerate or invalid faces are placed at the tail.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks required buffers.</exception>
    public static int[] OptimizeFacesLRU(Mesh mesh)
        => OptimizeFacesLRU(mesh, DefaultLruCacheSize);

    /// <summary>
    /// Computes a face remap that reorders triangles for improved post-transform vertex cache locality
    /// using the Forsyth LRU algorithm with the specified cache size.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.FaceIndices"/> and <see cref="Mesh.Positions"/>.</param>
    /// <param name="lruCacheSize">The simulated post-transform vertex cache entry count. Must be in the range [1, 64].</param>
    /// <returns>A face remap array of length <c>faceCount</c> where <c>faceRemap[newIndex] == originalFaceIndex</c>. Degenerate or invalid faces are placed at the tail.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks required buffers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lruCacheSize"/> is outside [1, 64].</exception>
    public static int[] OptimizeFacesLRU(Mesh mesh, int lruCacheSize)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentOutOfRangeException.ThrowIfLessThan(lruCacheSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lruCacheSize, 64);

        if (mesh.FaceIndices is not { } faceIndices)
            throw new InvalidOperationException("Mesh must have face index data to optimize face order.");

        if (mesh.Positions is not { } positions)
            throw new InvalidOperationException("Mesh must have position data to optimize face order.");

        int faceCount   = faceIndices.ElementCount / 3;
        int vertexCount = positions.ElementCount;

        if (faceCount == 0) return [];

        int[] adjStart = BuildAdjacencyStart(faceIndices, faceCount, vertexCount, out int[] adjFaces);
        var   state    = new LruState(vertexCount, lruCacheSize, faceCount, adjFaces, adjStart);

        for (int v = 0; v < vertexCount; v++)
            state.VertexScore[v] = ComputeVertexScore(int.MaxValue, state.ActiveFaceCount[v], lruCacheSize);

        for (int f = 0; f < faceCount; f++)
        {
            float s    = 0f;
            bool valid = true;
            for (int k = 0; k < 3; k++)
            {
                int v = faceIndices.Get<int>(f * 3 + k, 0, 0);
                if ((uint)v >= (uint)vertexCount) { valid = false; break; }
                s += state.VertexScore[v];
            }
            if (!valid) { state.FaceDone[f] = true; state.FaceScore[f] = float.MinValue; }
            else          state.FaceScore[f] = s;
        }

        int[] lruCache = new int[lruCacheSize];
        int   lruSize  = 0;
        int[] faceRemap = new int[faceCount];
        int   outIdx    = 0;

        while (outIdx < faceCount)
        {
            int   bestFace  = -1;
            float bestScore = float.MinValue;

            for (int c = 0; c < lruSize; c++)
            {
                int vert = lruCache[c];
                for (int aj = adjStart[vert], end = adjStart[vert + 1]; aj < end; aj++)
                {
                    int fi = adjFaces[aj];
                    if (!state.FaceDone[fi] && state.FaceScore[fi] > bestScore) { bestScore = state.FaceScore[fi]; bestFace = fi; }
                }
            }

            if (bestFace < 0)
            {
                for (int fi = 0; fi < faceCount; fi++)
                {
                    if (!state.FaceDone[fi] && state.FaceScore[fi] > bestScore) { bestScore = state.FaceScore[fi]; bestFace = fi; }
                }
            }

            if (bestFace < 0) break;

            state.FaceDone[bestFace] = true;
            faceRemap[outIdx++] = bestFace;

            int bv0 = faceIndices.Get<int>(bestFace * 3,     0, 0);
            int bv1 = faceIndices.Get<int>(bestFace * 3 + 1, 0, 0);
            int bv2 = faceIndices.Get<int>(bestFace * 3 + 2, 0, 0);

            if ((uint)bv0 < (uint)vertexCount) state.ActiveFaceCount[bv0]--;
            if ((uint)bv1 < (uint)vertexCount) state.ActiveFaceCount[bv1]--;
            if ((uint)bv2 < (uint)vertexCount) state.ActiveFaceCount[bv2]--;

            int[] newCache = new int[lruCacheSize];
            int   newSize  = 0;

            AddToCache(bv0, newCache, ref newSize, state);
            AddToCache(bv1, newCache, ref newSize, state);
            AddToCache(bv2, newCache, ref newSize, state);

            for (int c = 0; c < lruSize && newSize < lruCacheSize; c++)
            {
                if (lruCache[c] != bv0 && lruCache[c] != bv1 && lruCache[c] != bv2)
                    newCache[newSize++] = lruCache[c];
            }

            for (int c = 0; c < lruSize;  c++) state.CachePosition[lruCache[c]] = int.MaxValue;
            for (int c = 0; c < newSize;   c++) state.CachePosition[newCache[c]] = c;

            Array.Copy(newCache, lruCache, newSize);
            lruSize = newSize;

            for (int c = 0; c < lruSize; c++) UpdateVertex(lruCache[c], faceIndices, state);

            if ((uint)bv0 < (uint)vertexCount && state.CachePosition[bv0] == int.MaxValue) UpdateVertex(bv0, faceIndices, state);
            if ((uint)bv1 < (uint)vertexCount && state.CachePosition[bv1] == int.MaxValue) UpdateVertex(bv1, faceIndices, state);
            if ((uint)bv2 < (uint)vertexCount && state.CachePosition[bv2] == int.MaxValue) UpdateVertex(bv2, faceIndices, state);
        }

        for (int f = 0; f < faceCount; f++) { if (!state.FaceDone[f]) faceRemap[outIdx++] = f; }

        return faceRemap;
    }

    /// <summary>
    /// Computes the Forsyth LRU vertex score from a cache position and remaining active face count.
    /// Returns <c>-1</c> when <paramref name="activeFaceCount"/> is zero (the vertex is fully processed).
    /// </summary>
    /// <param name="cachePosition">Current LRU cache position, or <see cref="int.MaxValue"/> when evicted.</param>
    /// <param name="activeFaceCount">Number of unprocessed faces that still reference this vertex.</param>
    /// <param name="cacheSize">Simulated post-transform vertex cache entry count.</param>
    /// <returns>The combined cache-position and valence score, or <c>-1f</c> for fully-processed vertices.</returns>
    public static float ComputeVertexScore(int cachePosition, int activeFaceCount, int cacheSize)
    {
        if (activeFaceCount == 0) return -1f;

        float cacheScore = 0f;
        if (cachePosition != int.MaxValue && cachePosition < cacheSize)
            cacheScore = cachePosition < 3 ? 0.75f : MathF.Pow(1f - (cachePosition - 3f) / (cacheSize - 3f), 1.5f);

        return cacheScore + 2f * MathF.Pow(activeFaceCount, -0.5f);
    }

    /// <summary>
    /// Builds the CSR (Compressed Sparse Row) vertex-to-face adjacency list used during the LRU loop.
    /// </summary>
    /// <param name="faceIndices">The index buffer.</param>
    /// <param name="faceCount">Number of triangles.</param>
    /// <param name="vertexCount">Number of vertices.</param>
    /// <param name="adjFaces">Receives the flat face-index list.</param>
    /// <returns>The row-start offset array of length <c>vertexCount + 1</c>.</returns>
    private static int[] BuildAdjacencyStart(DataBuffer faceIndices, int faceCount, int vertexCount, out int[] adjFaces)
    {
        int[] activeFaceCount = new int[vertexCount];
        for (int f = 0; f < faceCount; f++)
        {
            for (int k = 0; k < 3; k++)
            {
                int v = faceIndices.Get<int>(f * 3 + k, 0, 0);
                if ((uint)v < (uint)vertexCount) activeFaceCount[v]++;
            }
        }

        int[] adjStart = new int[vertexCount + 1];
        for (int i = 0; i < vertexCount; i++) adjStart[i + 1] = adjStart[i] + activeFaceCount[i];

        adjFaces = new int[adjStart[vertexCount]];
        int[] writePos = (int[])adjStart.Clone();

        for (int f = 0; f < faceCount; f++)
        {
            for (int k = 0; k < 3; k++)
            {
                int v = faceIndices.Get<int>(f * 3 + k, 0, 0);
                if ((uint)v < (uint)vertexCount) adjFaces[writePos[v]++] = f;
            }
        }

        // Also populate ActiveFaceCount in state — we reuse the local counts.
        // (The LruState constructor zero-initialises; caller fills it after this returns.)
        // Pass the counts back implicitly — BuildAdjacencyStart also returns adjStart so
        // callers can loop over adjStart[v+1]-adjStart[v] to get activeFaceCount per vertex.
        return adjStart;
    }

    /// <summary>
    /// Adds vertex <paramref name="v"/> to the front of the LRU cache if it is not already present
    /// and the cache has not reached capacity.
    /// </summary>
    /// <param name="v">The vertex index to add.</param>
    /// <param name="cache">The cache buffer.</param>
    /// <param name="size">Current number of entries in the cache. Incremented on insertion.</param>
    /// <param name="state">The active LRU optimisation state providing bounds.</param>
    private static void AddToCache(int v, int[] cache, ref int size, LruState state)
    {
        if ((uint)v >= (uint)state.VertexCount || size >= state.CacheSize) return;
        for (int i = 0; i < size; i++) if (cache[i] == v) return;
        cache[size++] = v;
    }

    /// <summary>
    /// Recomputes the Forsyth score for vertex <paramref name="v"/> and propagates the change to
    /// every unprocessed face that references it.
    /// </summary>
    /// <param name="v">The vertex index to update.</param>
    /// <param name="faceIndices">The mesh index buffer.</param>
    /// <param name="state">The active LRU optimisation state.</param>
    private static void UpdateVertex(int v, DataBuffer faceIndices, LruState state)
    {
        if ((uint)v >= (uint)state.VertexCount) return;

        float newScore = ComputeVertexScore(state.CachePosition[v], state.ActiveFaceCount[v], state.CacheSize);
        if (newScore == state.VertexScore[v]) return;

        state.VertexScore[v] = newScore;

        for (int aj = state.AdjStart[v], end = state.AdjStart[v + 1]; aj < end; aj++)
        {
            int fi = state.AdjFaces[aj];
            if (state.FaceDone[fi]) continue;

            float s = 0f;
            for (int k = 0; k < 3; k++)
            {
                int vi = faceIndices.Get<int>(fi * 3 + k, 0, 0);
                if ((uint)vi < (uint)state.VertexCount) s += state.VertexScore[vi];
            }
            state.FaceScore[fi] = s;
        }
    }
}
