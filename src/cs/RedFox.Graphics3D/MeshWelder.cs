using RedFox.Graphics3D.Buffers;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Merges duplicate vertices of a <see cref="Mesh"/> and rewrites its face indices to match.
/// </summary>
/// <remarks>
/// Every per-vertex buffer is compacted, including normals, tangents, bitangents, colors, UVs, skin influences,
/// and morph deltas. Buffers keep their storage type; face indices are rewritten as 32-bit integers.
/// </remarks>
public static class MeshWelder
{
    /// <summary>
    /// Welds vertices whose positions and attributes are equal within <paramref name="tolerance"/>.
    /// </summary>
    /// <param name="mesh">The mesh to weld. Must have <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="tolerance">The maximum position distance and per-component attribute difference. Use 0 for exact matches.</param>
    /// <returns>The number of vertices removed.</returns>
    public static int Weld(Mesh mesh, float tolerance) => Weld(mesh, tolerance, tolerance);

    /// <summary>
    /// Welds vertices whose positions lie within <paramref name="positionTolerance"/> of each other and whose other attributes
    /// differ by no more than <paramref name="attributeTolerance"/> per component. Bone indices must match exactly.
    /// </summary>
    /// <param name="mesh">The mesh to weld. Must have <see cref="Mesh.Positions"/> and <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="positionTolerance">The maximum distance between welded positions. Use 0 for exact matches.</param>
    /// <param name="attributeTolerance">The maximum per-component difference between welded attributes. Use 0 for exact matches.</param>
    /// <returns>The number of vertices removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A tolerance is negative.</exception>
    /// <exception cref="InvalidOperationException">The mesh has no positions or face indices.</exception>
    public static int Weld(Mesh mesh, float positionTolerance, float attributeTolerance)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentOutOfRangeException.ThrowIfNegative(positionTolerance);
        ArgumentOutOfRangeException.ThrowIfNegative(attributeTolerance);

        if (mesh.Positions is not { } positions)
            throw new InvalidOperationException($"Mesh '{mesh.Name}' has no {nameof(Mesh.Positions)} buffer.");

        if (mesh.FaceIndices is null)
            throw new InvalidOperationException($"Mesh '{mesh.Name}' has no {nameof(Mesh.FaceIndices)} buffer.");

        (DataBuffer Buffer, float Tolerance)[] attributes = GetAttributes(mesh, attributeTolerance);
        int[] remap = new int[positions.ElementCount];
        List<int> kept = [];
        Dictionary<(int, int, int), List<int>> cells = [];
        float cellSize = positionTolerance > 0f ? positionTolerance : 1f;

        for (int vertex = 0; vertex < positions.ElementCount; vertex++)
        {
            Vector3 position = positions.GetVector3(vertex, 0);
            (int X, int Y, int Z) cell = ((int)MathF.Floor(position.X / cellSize), (int)MathF.Floor(position.Y / cellSize), (int)MathF.Floor(position.Z / cellSize));
            int match = FindMatch(cells, cell, kept, positions, position, vertex, positionTolerance, attributes);

            if (match < 0)
            {
                match = kept.Count;
                kept.Add(vertex);

                if (!cells.TryGetValue(cell, out List<int>? members))
                    cells[cell] = members = [];

                members.Add(match);
            }

            remap[vertex] = match;
        }

        if (kept.Count == positions.ElementCount)
            return 0;

        Apply(mesh, [.. kept], remap);
        return positions.ElementCount - kept.Count;
    }

    private static (DataBuffer Buffer, float Tolerance)[] GetAttributes(Mesh mesh, float tolerance)
    {
        DataBuffer?[] buffers = [mesh.Normals, mesh.Tangents, mesh.BiTangents, mesh.ColorLayers, mesh.UVLayers, mesh.Skin?.BoneWeights, mesh.Morph?.DeltaPositions, mesh.Morph?.DeltaNormals, mesh.Morph?.DeltaTangents];
        List<(DataBuffer, float)> attributes = [];

        foreach (DataBuffer? buffer in buffers)
        {
            if (buffer is not null)
                attributes.Add((buffer, tolerance));
        }

        if (mesh.Skin is { } skin)
            attributes.Add((skin.BoneIndices, 0f));

        return [.. attributes];
    }

    private static int FindMatch(Dictionary<(int, int, int), List<int>> cells, (int X, int Y, int Z) cell, List<int> kept, DataBuffer positions, Vector3 position, int vertex, float positionTolerance, (DataBuffer Buffer, float Tolerance)[] attributes)
    {
        float toleranceSquared = positionTolerance * positionTolerance;

        for (int x = cell.X - 1; x <= cell.X + 1; x++)
        {
            for (int y = cell.Y - 1; y <= cell.Y + 1; y++)
            {
                for (int z = cell.Z - 1; z <= cell.Z + 1; z++)
                {
                    if (!cells.TryGetValue((x, y, z), out List<int>? members))
                        continue;

                    foreach (int keptIndex in members)
                    {
                        int candidate = kept[keptIndex];

                        if (Vector3.DistanceSquared(position, positions.GetVector3(candidate, 0)) <= toleranceSquared && AttributesMatch(attributes, vertex, candidate))
                            return keptIndex;
                    }
                }
            }
        }

        return -1;
    }

    private static bool AttributesMatch((DataBuffer Buffer, float Tolerance)[] attributes, int first, int second)
    {
        foreach ((DataBuffer buffer, float tolerance) in attributes)
        {
            for (int value = 0; value < buffer.ValueCount; value++)
            {
                for (int component = 0; component < buffer.ComponentCount; component++)
                {
                    if (MathF.Abs(buffer.Get<float>(first, value, component) - buffer.Get<float>(second, value, component)) > tolerance)
                        return false;
                }
            }
        }

        return true;
    }

    private static void Apply(Mesh mesh, int[] kept, int[] remap)
    {
        mesh.Positions = mesh.Positions!.Gather(kept);
        mesh.Normals = mesh.Normals?.Gather(kept);
        mesh.Tangents = mesh.Tangents?.Gather(kept);
        mesh.BiTangents = mesh.BiTangents?.Gather(kept);
        mesh.ColorLayers = mesh.ColorLayers?.Gather(kept);
        mesh.UVLayers = mesh.UVLayers?.Gather(kept);

        if (mesh.Skin is { } skin)
            mesh.Skin = new Skin(skin.Bones, skin.BoneIndices.Gather(kept), skin.BoneWeights.Gather(kept)) { Name = skin.Name, SkinningMode = skin.SkinningMode, InverseBindMatrices = skin.InverseBindMatrices };

        if (mesh.Morph is { } morph)
        {
            Morph welded = new(morph.TargetNames, morph.DeltaPositions?.Gather(kept), morph.DeltaNormals?.Gather(kept), morph.DeltaTangents?.Gather(kept)) { Name = morph.Name };
            morph.Weights.CopyTo(welded.Weights);
            mesh.Morph = welded;
        }

        DataBuffer faceIndices = mesh.FaceIndices!;
        int[] indices = new int[faceIndices.ElementCount];

        for (int i = 0; i < indices.Length; i++)
            indices[i] = remap[faceIndices.Get<int>(i, 0, 0)];

        mesh.FaceIndices = new DataBuffer<int>(indices, 1, 1);
    }
}
