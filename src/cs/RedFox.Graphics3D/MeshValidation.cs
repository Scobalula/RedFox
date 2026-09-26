using RedFox.Graphics3D.Buffers;
using System.Numerics;
using System.Text;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides methods for detecting common structural problems in a <see cref="Mesh"/>.
/// </summary>
/// <remarks>
/// This is a managed port of the DirectXMesh <c>Validate</c> algorithm. The checks are gated by
/// <see cref="MeshValidationFlags"/> so callers can trade thoroughness for performance.
/// </remarks>
public static class MeshValidation
{
    /// <summary>
    /// Validates the mesh for degenerate triangles and out-of-range indices.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.FaceIndices"/>.</param>
    /// <returns><see langword="true"/> if no problems were found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.FaceIndices"/>.</exception>
    public static bool Validate(Mesh mesh)
        => Validate(mesh, MeshValidationFlags.Degenerate, adjacency: null, messages: null);

    /// <summary>
    /// Validates the mesh using the specified checks, collecting diagnostic messages.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="flags">The set of checks to perform.</param>
    /// <param name="messages">When not <see langword="null"/>, receives a human-readable description of every problem found.</param>
    /// <returns><see langword="true"/> if no problems were found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.FaceIndices"/>.</exception>
    public static bool Validate(Mesh mesh, MeshValidationFlags flags, StringBuilder? messages)
        => Validate(mesh, flags, adjacency: null, messages: messages);

    /// <summary>
    /// Validates the mesh using the specified checks and pre-computed adjacency data, collecting diagnostic messages.
    /// </summary>
    /// <param name="mesh">The target mesh. Must have non-null <see cref="Mesh.FaceIndices"/>.</param>
    /// <param name="flags">The set of checks to perform.</param>
    /// <param name="adjacency">Optional adjacency array of length <c>faceCount * 3</c>. Required for
    /// <see cref="MeshValidationFlags.BackFacing"/>, <see cref="MeshValidationFlags.Bowties"/>, and
    /// <see cref="MeshValidationFlags.AsymmetricAdjacency"/> checks.</param>
    /// <param name="messages">When not <see langword="null"/>, receives a human-readable description of every problem found.</param>
    /// <returns><see langword="true"/> if no problems were found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the mesh lacks <see cref="Mesh.FaceIndices"/>.</exception>
    public static bool Validate(Mesh mesh, MeshValidationFlags flags, uint[]? adjacency, StringBuilder? messages)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.FaceIndices is not { } faceIndices)
            throw new InvalidOperationException("Mesh must have face index data to validate.");

        int vertexCount = mesh.Positions?.ElementCount ?? int.MaxValue;
        int faceCount   = faceIndices.ElementCount / 3;

        bool requiresAdjacency = (flags & (MeshValidationFlags.BackFacing | MeshValidationFlags.Bowties | MeshValidationFlags.AsymmetricAdjacency)) != 0;

        if (requiresAdjacency && adjacency is null)
        {
            messages?.AppendLine("Adjacency data is required for BackFacing, Bowties, and AsymmetricAdjacency checks.");
            return false;
        }

        bool valid = true;

        for (int face = 0; face < faceCount; face++)
        {
            int i0 = faceIndices.Get<int>(face * 3, 0, 0);
            int i1 = faceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(face * 3 + 2, 0, 0);

            // Index range validation (always performed).
            for (int point = 0; point < 3; point++)
            {
                int idx = faceIndices.Get<int>(face * 3 + point, 0, 0);
                if (idx < 0 || (uint)idx >= (uint)vertexCount)
                {
                    if (messages is null) return false;
                    valid = false;
                    messages.AppendLine($"An invalid index value ({idx}) was found on face {face}.");
                }

                if (adjacency is not null)
                {
                    uint neighbour = adjacency[face * 3 + point];
                    if (neighbour != MeshAdjacency.Unused && neighbour >= (uint)faceCount)
                    {
                        if (messages is null) return false;
                        valid = false;
                        messages.AppendLine($"An invalid neighbour index value ({neighbour}) was found on face {face}.");
                    }
                }
            }

            // Unused triangle check.
            bool isUnused = i0 < 0 || i1 < 0 || i2 < 0;

            if (isUnused)
            {
                if ((flags & MeshValidationFlags.Unused) != 0)
                {
                    if (i0 != i1 || i0 != i2)
                    {
                        if (messages is null) return false;
                        valid = false;
                        messages.AppendLine($"An unused face ({face}) contains non-uniform sentinel indices ({i0},{i1},{i2}).");
                    }

                    if (adjacency is not null)
                    {
                        for (int point = 0; point < 3; point++)
                        {
                            uint neighbour = adjacency[face * 3 + point];
                            if (neighbour != MeshAdjacency.Unused)
                            {
                                if (messages is null) return false;
                                valid = false;
                                messages.AppendLine($"An unused face ({face}) has a neighbour ({neighbour}).");
                            }
                        }
                    }
                }

                continue;
            }

            // Degenerate triangle check.
            if ((flags & MeshValidationFlags.Degenerate) != 0 && (i0 == i1 || i0 == i2 || i1 == i2))
            {
                if (messages is null) return false;
                valid = false;
                int bad = i0 == i1 ? i0 : (i1 == i2 ? i2 : i0);
                messages.AppendLine($"A point ({bad}) was referenced more than once in triangle {face}.");

                if (adjacency is not null)
                {
                    for (int point = 0; point < 3; point++)
                    {
                        uint neighbour = adjacency[face * 3 + point];
                        if (neighbour != MeshAdjacency.Unused)
                            messages.AppendLine($"A degenerate face ({face}) has a neighbour ({neighbour}).");
                    }
                }

                continue;
            }

            if (adjacency is null)
                continue;

            // Back-facing duplicate neighbour check.
            if ((flags & MeshValidationFlags.BackFacing) != 0)
            {
                uint j0 = adjacency[face * 3], j1 = adjacency[face * 3 + 1], j2 = adjacency[face * 3 + 2];

                if ((j0 != MeshAdjacency.Unused && (j0 == j1 || j0 == j2)) ||
                    (j1 != MeshAdjacency.Unused && j1 == j2))
                {
                    if (messages is null) return false;
                    valid = false;
                    uint bad = (j0 != MeshAdjacency.Unused && j0 == j1) ? j0
                             : (j0 != MeshAdjacency.Unused && j0 == j2) ? j0
                             : j1;
                    messages.AppendLine($"Neighbour triangle ({bad}) appears more than once on triangle {face} (possible back-facing duplicate).");
                }
            }

            if ((flags & MeshValidationFlags.AsymmetricAdjacency) != 0)
            {
                for (int point = 0; point < 3; point++)
                {
                    uint neighbour = adjacency[face * 3 + point];
                    if (neighbour == MeshAdjacency.Unused)
                        continue;

                    if (neighbour >= (uint)faceCount)
                    {
                        if (messages is null) return false;
                        valid = false;
                        messages.AppendLine($"Neighbour triangle ({neighbour}) referenced by face {face} is outside the adjacency range.");
                        continue;
                    }

                    bool found = false;
                    for (int p2 = 0; p2 < 3; p2++)
                    {
                        if (adjacency[neighbour * 3 + p2] == (uint)face)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        if (messages is null) return false;
                        valid = false;
                        messages.AppendLine($"Neighbour triangle ({neighbour}) does not reference back to face {face}.");
                    }
                }
            }
        }

        return valid;
    }

    /// <summary>
    /// Validates the structure of the mesh: vertex buffer sizes, index ranges, skin references and weights,
    /// and morph sizes.
    /// </summary>
    /// <param name="mesh">The target mesh.</param>
    /// <returns><see langword="true"/> if no problems were found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    public static bool ValidateStructure(Mesh mesh) => ValidateStructure(mesh, messages: null);

    /// <summary>
    /// Validates the structure of the mesh: vertex buffer sizes, index ranges, skin references and weights,
    /// and morph sizes. Intended for use at import and export boundaries.
    /// </summary>
    /// <param name="mesh">The target mesh.</param>
    /// <param name="messages">When not <see langword="null"/>, receives a human-readable description of every problem found.</param>
    /// <returns><see langword="true"/> if no problems were found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    public static bool ValidateStructure(Mesh mesh, StringBuilder? messages)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        List<string> problems = [];
        int vertexCount = mesh.VertexCount;

        if (mesh.Positions is null)
            problems.Add("has no Positions buffer.");

        CheckVertexBuffer(problems, nameof(Mesh.Positions), mesh.Positions, vertexCount, 3);
        CheckVertexBuffer(problems, nameof(Mesh.Normals), mesh.Normals, vertexCount, 3);
        CheckVertexBuffer(problems, nameof(Mesh.Tangents), mesh.Tangents, vertexCount, 3);
        CheckVertexBuffer(problems, nameof(Mesh.BiTangents), mesh.BiTangents, vertexCount, 3);
        CheckVertexBuffer(problems, nameof(Mesh.ColorLayers), mesh.ColorLayers, vertexCount, 3);
        CheckVertexBuffer(problems, nameof(Mesh.UVLayers), mesh.UVLayers, vertexCount, 2);
        CheckFaceIndices(problems, mesh.FaceIndices, vertexCount);

        if (mesh.Skin is { } skin)
            CheckSkin(problems, mesh, skin, vertexCount);

        if (mesh.Morph is { } morph && morph.VertexCount != vertexCount)
            problems.Add($"has a morph covering {morph.VertexCount} vertices but the mesh has {vertexCount}.");

        foreach (string problem in problems)
            messages?.AppendLine($"Mesh '{mesh.Name}' {problem}");

        return problems.Count == 0;
    }

    private static void CheckVertexBuffer(List<string> problems, string name, DataBuffer? buffer, int vertexCount, int minimumComponents)
    {
        if (buffer is null)
            return;

        if (buffer.ElementCount != vertexCount)
            problems.Add($"{name} has {buffer.ElementCount} elements but the mesh has {vertexCount} vertices.");

        if (buffer.ComponentCount < minimumComponents)
            problems.Add($"{name} has {buffer.ComponentCount} components per value but at least {minimumComponents} are required.");

        for (int vertex = 0; vertex < buffer.ElementCount; vertex++)
        {
            for (int value = 0; value < buffer.ValueCount; value++)
            {
                for (int component = 0; component < buffer.ComponentCount; component++)
                {
                    if (!float.IsFinite(buffer.Get<float>(vertex, value, component)))
                    {
                        problems.Add($"{name} contains a non-finite value at vertex {vertex}.");
                        return;
                    }
                }
            }
        }
    }

    private static void CheckFaceIndices(List<string> problems, DataBuffer? faceIndices, int vertexCount)
    {
        if (faceIndices is null)
            return;

        if (faceIndices.ValueCount != 1 || faceIndices.ComponentCount != 1)
            problems.Add($"FaceIndices must store one index per element but stores {faceIndices.ValueCount}x{faceIndices.ComponentCount}.");

        if (faceIndices.ElementCount % 3 != 0)
            problems.Add($"FaceIndices has {faceIndices.ElementCount} indices, which is not a multiple of 3.");

        int invalidCount = 0;
        long firstInvalid = 0;

        for (int i = 0; i < faceIndices.ElementCount; i++)
        {
            long index = faceIndices.Get<long>(i, 0, 0);

            if (index >= 0 && index < vertexCount)
                continue;

            if (invalidCount++ == 0)
                firstInvalid = index;
        }

        if (invalidCount > 0)
            problems.Add($"FaceIndices has {invalidCount} indices outside [0, {vertexCount}), first value {firstInvalid}.");
    }

    private static void CheckSkin(List<string> problems, Mesh mesh, Skin skin, int vertexCount)
    {
        DataBuffer boneIndices = skin.BoneIndices;
        DataBuffer boneWeights = skin.BoneWeights;

        if (boneIndices.ElementCount != vertexCount || boneWeights.ElementCount != vertexCount)
            problems.Add($"has skin buffers covering {boneIndices.ElementCount} indices and {boneWeights.ElementCount} weights but the mesh has {vertexCount} vertices.");

        if (boneIndices.ValueCount != boneWeights.ValueCount)
            problems.Add($"has {boneIndices.ValueCount} bone indices but {boneWeights.ValueCount} bone weights per vertex.");

        SceneNode meshRoot = mesh.GetRoot();
        HashSet<string> boneNames = new(StringComparer.Ordinal);

        for (int i = 0; i < skin.Bones.Count; i++)
        {
            SkeletonBone bone = skin.Bones[i];

            if (!ReferenceEquals(bone.GetRoot(), meshRoot))
                problems.Add($"is skinned to bone '{bone.Name}', which is not in the mesh's hierarchy.");

            if (!boneNames.Add(bone.Name))
                problems.Add($"is skinned to more than one bone named '{bone.Name}'.");

            if (skin.InverseBindMatrices is { } inverseBindMatrices && !Matrix4x4.Invert(inverseBindMatrices[i], out _))
                problems.Add($"has a non-invertible inverse bind matrix for bone '{bone.Name}'.");
        }

        CheckSkinWeights(problems, skin, Math.Min(vertexCount, Math.Min(boneIndices.ElementCount, boneWeights.ElementCount)));
    }

    private static void CheckSkinWeights(List<string> problems, Skin skin, int vertexCount)
    {
        int invalidIndexCount = 0;
        int invalidWeightCount = 0;
        int unnormalizedCount = 0;

        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            float totalWeight = 0f;

            for (int influence = 0; influence < skin.InfluenceCount; influence++)
            {
                float weight = skin.BoneWeights.Get<float>(vertex, influence, 0);

                if (!float.IsFinite(weight) || weight < 0f)
                {
                    invalidWeightCount++;
                    continue;
                }

                if (weight > 0f && (uint)skin.BoneIndices.Get<int>(vertex, influence, 0) >= (uint)skin.Bones.Count)
                    invalidIndexCount++;

                totalWeight += weight;
            }

            if (MathF.Abs(totalWeight - 1f) > 1e-3f)
                unnormalizedCount++;
        }

        if (invalidIndexCount > 0)
            problems.Add($"has {invalidIndexCount} weighted influences that reference a bone index outside the skin's {skin.Bones.Count} bones.");

        if (invalidWeightCount > 0)
            problems.Add($"has {invalidWeightCount} negative or non-finite bone weights.");

        if (unnormalizedCount > 0)
            problems.Add($"has {unnormalizedCount} vertices whose bone weights do not sum to 1.");
    }
}
