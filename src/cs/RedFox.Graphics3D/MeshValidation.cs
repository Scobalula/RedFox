using RedFox.Graphics3D.Buffers;
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
}
