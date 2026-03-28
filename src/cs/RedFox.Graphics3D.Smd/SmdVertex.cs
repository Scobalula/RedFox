using System.Numerics;

namespace RedFox.Graphics3D.Smd;

/// <summary>
/// Represents one parsed vertex row from the SMD triangles section.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SmdVertex"/> struct.
/// </remarks>
/// <param name="position">The vertex position.</param>
/// <param name="normal">The vertex normal.</param>
/// <param name="uv">The vertex texture coordinates.</param>
/// <param name="parentBone">The rigid parent bone index.</param>
/// <param name="links">The weighted bone links.</param>
public readonly struct SmdVertex(Vector3 position, Vector3 normal, Vector2 uv, int parentBone, (int BoneIndex, float Weight)[]? links)
{
    /// <summary>
    /// Gets the vertex position.
    /// </summary>
    public Vector3 Position { get; } = position;

    /// <summary>
    /// Gets the vertex normal.
    /// </summary>
    public Vector3 Normal { get; } = normal;

    /// <summary>
    /// Gets the vertex texture coordinates.
    /// </summary>
    public Vector2 UV { get; } = uv;

    /// <summary>
    /// Gets the rigid parent bone index used when no weighted links are present.
    /// </summary>
    public int ParentBone { get; } = parentBone;

    /// <summary>
    /// Gets the smooth skinning links for this vertex.
    /// </summary>
    public (int BoneIndex, float Weight)[] Links { get; } = links ?? [];
}
